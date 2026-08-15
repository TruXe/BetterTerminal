using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using BetterTerminal.Interop;
using BetterTerminal.Shell.ViewModels;
using BetterTerminal.Shell.Views;
using BetterTerminal.Terminal;

namespace BetterTerminal.Shell.Services
{
    /// <summary>
    /// Everything the designed shell needs to be a real terminal: it owns the tab and pane tree,
    /// starts and tears down sessions, implements the MainViewModel commands, feeds the command
    /// palette, applies settings to every live surface, and persists the layout.
    /// </summary>
    public sealed class TerminalWorkspace : IDockHost
    {
        private const TerminalBackend Backend = TerminalBackend.Automatic;

        // Icon-font code point, built from its number: this file stays plain ASCII, because the
        // compiler only reads it as UTF-8 when it carries a byte order mark.
        private static readonly string ProjectCommandGlyph = ((char)0xE756).ToString();
        private static readonly string LinkCommandGlyph = ((char)0xE71B).ToString();

        private readonly MainViewModel _model;
        private readonly Window _owner;
        private readonly CommandPalette _palette;
        private readonly SettingsViewModel _settings = new SettingsViewModel();
        private readonly TerminalLinkOptions _linkOptions = new TerminalLinkOptions();
        private readonly TerminalLinkOpener _linkOpener;

        private SettingsWindow _settingsWindow;
        private FilesPanel _filesPanel;
        private string _projectDirectory;
        private PersistedProject _project;

        private DockController _docking;
        private FrameworkElement _paneHost;
        private DockOverlay _overlay;
        private WebServer _webServer;

        public TerminalWorkspace(MainViewModel model, Window owner, CommandPalette palette)
        {
            _model = model;
            _owner = owner;
            _palette = palette;

            BuildProfiles();
            BuildSchemes();
            BuildSettingsPages();

            _model.NewTabCommand = new ShellCommand(NewTab);
            _model.SplitRightCommand = new ShellCommand(SplitRight);
            _model.SplitDownCommand = new ShellCommand(SplitDown);
            _model.ClosePaneCommand = new ShellCommand(CloseActivePane);
            _model.OpenPaletteCommand = new ShellCommand(OpenPalette);
            _model.OpenSettingsCommand = new ShellCommand(OpenSettings);
            _model.OpenProfileFlyoutCommand = new ShellCommand(OpenProfilePicker);
            _model.FocusNextPaneCommand = new ShellCommand(delegate { FocusNextPane(1); });
            _model.FocusPreviousPaneCommand = new ShellCommand(delegate { FocusNextPane(-1); });
            _model.OpenConnectionsCommand = new ShellCommand(OpenConnections);
            _model.OpenWorkspaceSetupCommand = new ShellCommand(OpenWorkspaceSetup);
            _model.OpenFilesCommand = new ShellCommand(OpenFiles);

            _settings.Changed += OnSettingsChanged;
            ThemeService.Current.ThemeChanged += OnThemeChanged;

            _linkOpener = new TerminalLinkOpener(null,
                delegate(TerminalLinkPrompt prompt) { return LinkConfirmation.Ask(_owner, prompt); });
            RefreshLinkOptions();

            _palette.InputRequested += OnPaletteInput;
        }

        public void Restore()
        {
            PersistedWorkspace workspace = SessionStore.Load();
            if (workspace != null)
            {
                _settings.ApplyStored(workspace.Theme, workspace.Scheme, workspace.FontFamily,
                    workspace.FontSize, workspace.CursorShape, workspace.BlinkCursor);
                _settings.SplitUsesActiveProfile = workspace.SplitUsesActiveProfile;
                _settings.ApplyStoredLinks(workspace.DetectLinks, workspace.LinkActivation,
                    workspace.LinkSchemes, workspace.ConfirmLinks);
                RefreshLinkOptions();
                RestorePlacement(workspace);
            }

            // A launch from a folder is about that folder: its own settings decide the shell and
            // the first line to run, and the stored tab layout of the last plain launch is left
            // alone rather than reopened on top of it.
            if (StartupOptions.Current.HasProject)
            {
                RestoreProject(StartupOptions.Current.ProjectDirectory);
                return;
            }

            if (workspace != null && workspace.Tabs != null && workspace.Tabs.Count > 0)
            {
                foreach (PersistedTab persisted in workspace.Tabs)
                {
                    object root = RestoreNode(persisted.Root);
                    if (root == null)
                    {
                        continue;
                    }

                    TabViewModel tab = CreateTab(root);
                    tab.Title = string.IsNullOrEmpty(persisted.Header) ? "Terminal" : persisted.Header;
                    tab.FullTitle = tab.Title;
                    _model.Tabs.Add(tab);
                }

                RestoreFloating(workspace.Floating);

                if (_model.Tabs.Count > 0)
                {
                    int index = Math.Max(0, Math.Min(_model.Tabs.Count - 1, workspace.SelectedTab));
                    _model.SelectedTab = _model.Tabs[index];
                    FocusFirstPane(_model.SelectedTab);
                    return;
                }

                // Every tab was torn off into a window of its own; that is a legitimate layout and
                // must not be papered over with a fresh tab.
                if (_docking != null && _docking.FloatingWindows.Count > 0)
                {
                    return;
                }
            }

            NewTab();
        }

        public void Save()
        {
            PersistedWorkspace workspace = new PersistedWorkspace();
            workspace.Backend = Backend.ToString();
            workspace.SelectedTab = _model.SelectedTab == null ? 0 : _model.Tabs.IndexOf(_model.SelectedTab);
            workspace.Theme = _settings.Theme.ToString();
            workspace.Scheme = _settings.SelectedScheme == null ? null : _settings.SelectedScheme.DictionaryName;
            workspace.FontFamily = _settings.SelectedFont;
            workspace.FontSize = _settings.FontSize;
            workspace.CursorShape = _settings.CursorShapeName;
            workspace.BlinkCursor = _settings.BlinkCursor;
            workspace.SplitUsesActiveProfile = _settings.SplitUsesActiveProfile;
            workspace.DetectLinks = _settings.DetectLinks;
            workspace.LinkActivation = _settings.LinkActivation.ToString();
            workspace.LinkSchemes = _settings.LinkSchemes;
            workspace.ConfirmLinks = _settings.ConfirmLinks;
            CapturePlacement(workspace);
            workspace.Tabs = new List<PersistedTab>();

            foreach (TabViewModel tab in _model.Tabs)
            {
                PersistedNode root = CaptureNode(tab.RootPane);
                if (root == null)
                {
                    continue;
                }

                PersistedTab persisted = new PersistedTab();
                persisted.Header = tab.Title;
                persisted.Root = root;
                workspace.Tabs.Add(persisted);
            }

            workspace.Floating = CaptureFloating();
            SessionStore.Save(workspace);
        }

        public void CloseAllSessions()
        {
            StopLocalServer();

            foreach (TabViewModel tab in _model.Tabs)
            {
                foreach (PaneViewModel pane in Panes(tab.RootPane))
                {
                    if (pane.Surface != null)
                    {
                        pane.Surface.CloseSession();
                    }
                }
            }
        }

        /// <summary>
        /// Puts the window back where it was, but only if that rectangle is still on a screen -
        /// a monitor that was unplugged since the last run must not park the window off-desktop.
        /// </summary>
        private void RestorePlacement(PersistedWorkspace workspace)
        {
            if (workspace.WindowWidth < _owner.MinWidth || workspace.WindowHeight < _owner.MinHeight)
            {
                return;
            }

            double left = workspace.WindowLeft;
            double top = workspace.WindowTop;
            bool onScreen = left + workspace.WindowWidth > SystemParameters.VirtualScreenLeft
                && top + workspace.WindowHeight > SystemParameters.VirtualScreenTop
                && left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth
                && top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;

            _owner.Width = workspace.WindowWidth;
            _owner.Height = workspace.WindowHeight;

            if (onScreen)
            {
                _owner.Left = left;
                _owner.Top = top;
            }

            if (workspace.WindowMaximized)
            {
                _owner.WindowState = WindowState.Maximized;
            }
        }

        private void CapturePlacement(PersistedWorkspace workspace)
        {
            workspace.WindowMaximized = _owner.WindowState == WindowState.Maximized;

            // RestoreBounds is the un-maximized rectangle, which is the one worth restoring.
            Rect bounds = _owner.WindowState == WindowState.Normal
                ? new Rect(_owner.Left, _owner.Top, _owner.Width, _owner.Height)
                : _owner.RestoreBounds;

            if (bounds.IsEmpty)
            {
                return;
            }

            workspace.WindowLeft = bounds.Left;
            workspace.WindowTop = bounds.Top;
            workspace.WindowWidth = bounds.Width;
            workspace.WindowHeight = bounds.Height;
        }

        // ===== project folders =====

        private void RestoreProject(string directory)
        {
            _projectDirectory = directory;
            _project = ProjectStore.Load(directory);

            bool isNew = _project == null;
            if (isNew)
            {
                // The folder is created here rather than when the setup is saved, so a project
                // opened once and then dismissed still has its settings folder.
                _project = DefaultProject(directory);
                ProjectStore.Save(directory, _project);
            }

            UpdateProjectName();

            PaneViewModel pane = CreatePane(FindShell(_project.Shell), directory, _project.StartupCommand);
            TabViewModel tab = CreateTab(pane);
            tab.Title = ProjectDisplayName();
            tab.FullTitle = directory;

            _model.Tabs.Add(tab);
            _model.SelectedTab = tab;
            SetActivePane(pane);
            FocusLater(pane);

            // Deferred, not called here: this runs inside the window's Loaded handler, and a
            // dialog opened from there would sit in front of a window that has not finished
            // showing - with the splash still on top of it.
            if (isNew || _project.ShowSetupOnOpen)
            {
                _owner.Dispatcher.BeginInvoke(new Action(OpenWorkspaceSetup), DispatcherPriority.Background);
            }

            ApplyLocalServer();
        }

        private static PersistedProject DefaultProject(string directory)
        {
            PersistedProject project = new PersistedProject();
            project.Name = new DirectoryInfo(directory).Name;
            project.Shell = ShellProfile.CommandPrompt.Name;
            project.ShowSetupOnOpen = true;
            project.Commands = new List<PersistedCommand>();
            project.Values = new List<PersistedValue>();
            return project;
        }

        /// <summary>
        /// The status strip and every shell started from here name the same project. The shells
        /// read it out of the environment, which they inherit at the moment they are started.
        /// </summary>
        private void UpdateProjectName()
        {
            _model.ProjectName = ProjectDisplayName();
            ShellPresentation.SetProject(_model.ProjectName, _projectDirectory);
        }

        private string ProjectDisplayName()
        {
            if (_project != null && !string.IsNullOrEmpty(_project.Name))
            {
                return _project.Name;
            }

            return string.IsNullOrEmpty(_projectDirectory)
                ? string.Empty
                : new DirectoryInfo(_projectDirectory).Name;
        }

        /// <summary>
        /// Opens the setup for the current project. A window that was not launched from a folder
        /// has no project yet, so the focused session's directory becomes one.
        /// </summary>
        private void OpenWorkspaceSetup()
        {
            if (string.IsNullOrEmpty(_projectDirectory))
            {
                _projectDirectory = WorkingDirectoryForNewSession();
            }

            if (string.IsNullOrEmpty(_projectDirectory) || !Directory.Exists(_projectDirectory))
            {
                return;
            }

            if (_project == null)
            {
                _project = ProjectStore.Load(_projectDirectory) ?? DefaultProject(_projectDirectory);
            }

            WorkspaceSetupViewModel model = BuildSetupModel();

            WorkspaceSetupWindow window = new WorkspaceSetupWindow();
            window.Owner = _owner;
            window.DataContext = model;

            if (window.ShowDialog() == true)
            {
                ApplySetup(model);
            }
        }

        private WorkspaceSetupViewModel BuildSetupModel()
        {
            WorkspaceSetupViewModel model = new WorkspaceSetupViewModel();
            model.Directory = _projectDirectory;
            model.Name = ProjectDisplayName();
            model.StartupCommand = _project.StartupCommand;
            model.ShowSetupOnOpen = _project.ShowSetupOnOpen;
            model.LocalServer = _project.LocalServer;
            model.LocalServerPort = (_project.LocalServerPort > 0
                ? _project.LocalServerPort
                : WorkspaceSetupViewModel.DefaultPort).ToString();

            foreach (ProfileViewModel profile in _model.Profiles)
            {
                model.Shells.Add(profile.Name);
            }

            model.SelectedShell = model.Shells.Contains(_project.Shell)
                ? _project.Shell
                : (model.Shells.Count > 0 ? model.Shells[0] : null);

            if (_project.Commands != null)
            {
                foreach (PersistedCommand command in _project.Commands)
                {
                    model.AddCommand(command.Name, command.Text);
                }
            }

            if (_project.Values != null)
            {
                foreach (PersistedValue value in _project.Values)
                {
                    model.AddValue(value.Key, value.Value);
                }
            }

            return model;
        }

        private void ApplySetup(WorkspaceSetupViewModel model)
        {
            _project.Name = string.IsNullOrEmpty(model.Name)
                ? new DirectoryInfo(_projectDirectory).Name
                : model.Name;
            _project.Shell = model.SelectedShell;
            _project.StartupCommand = model.StartupCommand;
            _project.ShowSetupOnOpen = model.ShowSetupOnOpen;
            _project.LocalServer = model.LocalServer;
            _project.LocalServerPort = model.ResolvedPort;

            _project.Commands = new List<PersistedCommand>();
            foreach (CommandEntryViewModel entry in model.Commands)
            {
                PersistedCommand command = new PersistedCommand();
                command.Name = entry.Name;
                command.Text = entry.Text;
                _project.Commands.Add(command);
            }

            _project.Values = new List<PersistedValue>();
            foreach (ValueEntryViewModel entry in model.Values)
            {
                PersistedValue value = new PersistedValue();
                value.Key = entry.Key;
                value.Value = entry.Value;
                _project.Values.Add(value);
            }

            ProjectStore.Save(_projectDirectory, _project);
            UpdateProjectName();
            ApplyLocalServer();
        }

        /// <summary>
        /// Starts or stops the local web server to match the project setting. The server serves
        /// whichever pane is focused, so the browser follows the desktop; it is handed a delegate
        /// rather than a session so it always reads the current one.
        /// </summary>
        private void ApplyLocalServer()
        {
            bool wanted = _project != null && _project.LocalServer;
            int port = _project != null && _project.LocalServerPort > 0
                ? _project.LocalServerPort
                : WorkspaceSetupViewModel.DefaultPort;

            if (!wanted)
            {
                StopLocalServer();
                return;
            }

            if (_webServer != null && _webServer.Port == port)
            {
                return;
            }

            StopLocalServer();

            _webServer = new WebServer(port, delegate
            {
                PaneViewModel active = _model.ActivePane;
                return active != null && active.Surface != null ? active.Surface.PseudoConsole : null;
            });

            string error;
            if (_webServer.TryStart(out error))
            {
                _model.StatusMessage = "Local server on " + _webServer.Url + " - open it in a browser.";
            }
            else
            {
                _model.StatusMessage = "The local server could not start on port " + port + ": " + error;
                _webServer = null;
            }
        }

        private void StopLocalServer()
        {
            if (_webServer != null)
            {
                _webServer.Stop();
                _webServer = null;
            }
        }

        // ===== saved connections =====

        private void OpenConnections()
        {
            // Opened as a floating pane, not as a window of its own kind: that is the same window a
            // torn-off pane gets, so its header docks by being dragged and the connection list needs
            // to be taught nothing about docking. It is modeless for the same reason - a modal
            // dialog owns the pointer, and there would be no drag to track.
            ToolPaneViewModel leaf = CreateTool(ToolPaneViewModel.ConnectionsKind);
            _docking.Open(leaf, 820, 520);
        }

        /// <summary>
        /// Puts a tool panel into the pane grid beside the focused pane. The panel is the instance
        /// the window was showing - its open folder, its half-typed entries and its reachability
        /// checks all carry over, which is the whole point of moving rather than rebuilding.
        /// </summary>
        private void DockTool(string kind, string header, FrameworkElement panel)
        {
            if (panel == null)
            {
                return;
            }

            DockController.Detach(panel);

            ToolPaneViewModel leaf = new ToolPaneViewModel(kind, header, panel);
            leaf.CloseCommand = new ShellCommand(delegate { CloseTool(leaf); });

            IDockHost host = this;
            DockLeafViewModel target = null;
            foreach (DockLeafViewModel visible in host.VisibleLeaves)
            {
                target = visible;
                break;
            }

            if (target == null)
            {
                host.InsertAtEdge(leaf, DockSide.Right);
            }
            else
            {
                host.InsertBeside(leaf, target, DockSide.Right);
            }

            _model.StatusMessage = header + " is now a pane. Drag its header to pull it out again.";
        }

        /// <summary>
        /// Builds a docked tool from the saved layout. The tool comes back empty and reads the world
        /// afresh; only its place in the grid was saved, because what it was showing belonged to the
        /// session that ended.
        /// </summary>
        private ToolPaneViewModel CreateTool(string kind)
        {
            FrameworkElement panel;
            string header;

            if (kind == ToolPaneViewModel.FilesKind)
            {
                FileExplorerViewModel files = new FileExplorerViewModel();
                files.OpenRequested += delegate { OpenFile(files); };
                files.SaveRequested += delegate { SaveFile(files); };

                FilesPanel view = new FilesPanel();
                view.DataContext = files;
                _filesPanel = view;

                panel = view;
                header = "Files";
                ShowFolderIn(files);
            }
            else
            {
                ConnectionsViewModel connections = new ConnectionsViewModel();
                foreach (PersistedConnection saved in ConnectionStore.Load())
                {
                    connections.Add(saved.UserName, saved.Host);
                }

                connections.Changed += delegate { SaveConnections(connections); };
                connections.RefreshRequested += delegate { CheckReachability(connections); };
                connections.ConnectRequested += delegate(object sender, ConnectRequestedEventArgs e)
                {
                    if (e.Connection != null)
                    {
                        Connect(e.Connection, e.SeparateWindow);
                    }
                };

                ConnectionsPanel view = new ConnectionsPanel();
                view.DataContext = connections;

                panel = view;
                header = "Saved connections";
                CheckReachability(connections);
            }

            ToolPaneViewModel leaf = new ToolPaneViewModel(kind, header, panel);
            leaf.CloseCommand = new ShellCommand(delegate { CloseTool(leaf); });
            return leaf;
        }

        /// <summary>Points a file explorer at the project folder, or at the focused session's.</summary>
        private void ShowFolderIn(FileExplorerViewModel model)
        {
            string directory = string.IsNullOrEmpty(_projectDirectory)
                ? WorkingDirectoryForNewSession()
                : _projectDirectory;

            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                model.Message = "There is no folder to show yet.";
                return;
            }

            model.RootPath = directory;
            model.Message = "Reading " + directory;

            WorkspaceFiles.Scan(directory, delegate(FileNodeViewModel root)
            {
                model.SetRoot(root);
                model.Message = root == null
                    ? "That folder could not be read."
                    : "Pick a file on the left to open it.";
            });
        }

        private void CloseTool(ToolPaneViewModel leaf)
        {
            IDockHost host = this;
            host.RemoveLeaf(leaf);

            if (leaf.Kind == ToolPaneViewModel.FilesKind)
            {
                _filesPanel = null;
            }
        }

        private static void SaveConnections(ConnectionsViewModel model)
        {
            List<PersistedConnection> connections = new List<PersistedConnection>();
            foreach (ConnectionViewModel connection in model.Connections)
            {
                PersistedConnection saved = new PersistedConnection();
                saved.UserName = connection.UserName;
                saved.Host = connection.Host;
                connections.Add(saved);
            }

            ConnectionStore.Save(connections);
        }

        private void CheckReachability(ConnectionsViewModel model)
        {
            foreach (ConnectionViewModel connection in model.Connections)
            {
                ConnectionViewModel captured = connection;
                captured.Status = ConnectionStatus.Checking;
                HostReachability.Probe(_owner.Dispatcher, captured.Host, delegate(bool reachable)
                {
                    captured.Status = reachable ? ConnectionStatus.Reachable : ConnectionStatus.Unreachable;
                });
            }
        }

        /// <summary>
        /// Opens a session and types the connection line into it. The line is input, never part
        /// of the command line the child process is started with.
        /// </summary>
        private void Connect(ConnectionViewModel connection, bool separateWindow)
        {
            ShellProfile shell = SelectedShell();
            string directory = WorkingDirectoryForNewSession();

            if (!separateWindow)
            {
                AddPane(shell, directory, connection.CommandLine, false);
                return;
            }

            TerminalSurface surface = new TerminalSurface(shell, directory, Backend);
            surface.StartupCommand = connection.CommandLine;
            surface.Loaded += delegate { ApplySettingsTo(surface); };

            SessionWindow window = new SessionWindow();
            window.Owner = _owner;
            window.Attach(surface, connection.Display);
            window.Show();
        }

        private void BuildProfiles()
        {
            _model.Profiles.Add(new ProfileViewModel
            {
                Name = ShellProfile.CommandPrompt.Name,
                CommandLine = ShellPresentation.Apply(ShellProfile.CommandPrompt).BuildCommandLine(),
                Source = "built-in",
                Accelerator = "Ctrl+Shift+1",
                ScrollbackLines = TerminalSessionFactory.DefaultScrollbackLines,
                Shell = ShellProfile.CommandPrompt
            });

            _model.Profiles.Add(new ProfileViewModel
            {
                Name = ShellProfile.WindowsPowerShell.Name,
                CommandLine = ShellPresentation.Apply(ShellProfile.WindowsPowerShell).BuildCommandLine(),
                Source = "built-in",
                Accelerator = "Ctrl+Shift+2",
                ScrollbackLines = TerminalSessionFactory.DefaultScrollbackLines,
                Shell = ShellProfile.WindowsPowerShell
            });

            _model.Profiles.Add(new ProfileViewModel
            {
                Name = ShellProfile.CliAiWizard.Name,
                CommandLine = ShellPresentation.Apply(ShellProfile.CliAiWizard).BuildCommandLine(),
                Source = "built-in",
                Accelerator = "Ctrl+Shift+3",
                ScrollbackLines = TerminalSessionFactory.DefaultScrollbackLines,
                Shell = ShellProfile.CliAiWizard
            });

            _model.DefaultProfile = _model.Profiles[0];
        }

        private void BuildSchemes()
        {
            _settings.Schemes.Add(SampleData.Scheme("Campbell", "Campbell", "#FF0C0C0C", "#FFCCCCCC",
                new[] { "#FF0C0C0C", "#FFC50F1F", "#FF13A10E", "#FFC19C00", "#FF0037DA", "#FF881798", "#FF3A96DD", "#FFCCCCCC" }));
            _settings.Schemes.Add(SampleData.Scheme("One Half Dark", "OneHalfDark", "#FF282C34", "#FFDCDFE4",
                new[] { "#FF282C34", "#FFE06C75", "#FF98C379", "#FFE5C07B", "#FF61AFEF", "#FFC678DD", "#FF56B6C2", "#FFDCDFE4" }));
            _settings.Schemes.Add(SampleData.Scheme("Solarized Dark", "SolarizedDark", "#FF002B36", "#FF839496",
                new[] { "#FF002B36", "#FFDC322F", "#FF859900", "#FFB58900", "#FF268BD2", "#FFD33682", "#FF2AA198", "#FFEEE8D5" }));

            _settings.SelectedScheme = _settings.Schemes[0];
            _settings.OpenSettingsFileCommand = new ShellCommand(OpenWorkspaceFile);
        }

        private void BuildSettingsPages()
        {
            foreach (ProfileViewModel profile in _model.Profiles)
            {
                _settings.Profiles.Add(profile);
            }

            // Every binding listed here is one the shell really installs; nothing aspirational.
            _settings.Shortcuts.Add(new ShortcutViewModel("New tab", "Ctrl+Shift+T", "window"));
            _settings.Shortcuts.Add(new ShortcutViewModel("Close the focused pane", "Ctrl+Shift+W", "window"));
            _settings.Shortcuts.Add(new ShortcutViewModel("Open the command palette", "Ctrl+Shift+P", "window"));
            _settings.Shortcuts.Add(new ShortcutViewModel("Split pane right", "Alt+Shift+Plus", "window"));
            _settings.Shortcuts.Add(new ShortcutViewModel("Split pane down", "Alt+Shift+Minus", "window"));
            _settings.Shortcuts.Add(new ShortcutViewModel("Open settings", "Ctrl+comma", "window"));
            _settings.Shortcuts.Add(new ShortcutViewModel("Copy the selection", "Ctrl+Shift+C", "terminal"));
            _settings.Shortcuts.Add(new ShortcutViewModel("Paste", "Ctrl+Shift+V or Shift+Insert", "terminal"));
            _settings.Shortcuts.Add(new ShortcutViewModel("Scroll the buffer", "Shift+PageUp or Shift+PageDown", "terminal"));
            _settings.Shortcuts.Add(new ShortcutViewModel("Zoom the terminal font", "Ctrl+mouse wheel", "terminal"));
            _settings.Shortcuts.Add(new ShortcutViewModel("Move focus to the next pane", "Alt+Right", "window"));
            _settings.Shortcuts.Add(new ShortcutViewModel("Move focus to the previous pane", "Alt+Left", "window"));
            _settings.Shortcuts.Add(new ShortcutViewModel("Send a line to the shell", "type > in the palette", "palette"));

            _settings.About = CreateAbout();
        }

        private AboutViewModel CreateAbout()
        {
            AboutViewModel about = new AboutViewModel();
            about.VersionLine = AboutViewModel.AssemblyVersion();
            about.Runtime = ".NET Framework 4.8, x64";
            about.Backend = TerminalSessionFactory.Resolve(Backend) == TerminalBackend.PseudoConsole
                ? "Virtual terminal"
                : "Hosted console window";
            about.HostOs = Environment.OSVersion.VersionString;
            about.SettingsPath = SessionStore.FilePath;
            about.CopyDetailsCommand = new ShellCommand(delegate { Clipboard.SetText(about.ToDetails()); });
            about.OpenNoticesCommand = new ShellCommand(OpenWorkspaceFile);
            about.OpenReleaseNotesCommand = new ShellCommand(OpenDocumentation);
            return about;
        }

        private ShellProfile SelectedShell()
        {
            ProfileViewModel profile = _model.DefaultProfile;
            return profile == null || profile.Shell == null ? ShellProfile.CommandPrompt : profile.Shell;
        }

        private PaneViewModel CreatePane(ShellProfile shell, string workingDirectory)
        {
            return CreatePane(shell, workingDirectory, null);
        }

        private PaneViewModel CreatePane(ShellProfile shell, string workingDirectory, string startupCommand)
        {
            // The shell is started the way this application presents it - no banner, its own
            // prompt - which is a property of the session, not of the stored profile.
            TerminalSurface surface = new TerminalSurface(
                ShellPresentation.Apply(shell), workingDirectory, Backend);
            surface.StartupCommand = startupCommand;

            PaneViewModel pane = new PaneViewModel(surface);
            pane.CloseCommand = new ShellCommand(delegate { ClosePane(pane); });

            surface.AddHandler(UIElement.GotKeyboardFocusEvent,
                new KeyboardFocusChangedEventHandler(delegate { SetActivePane(pane); }), true);
            surface.Exited += delegate { OnPaneExited(pane); };

            surface.DropTargetChanged += delegate { pane.IsDropTarget = surface.IsDropTarget; };
            surface.DropReported += delegate(object sender, PaneDropEventArgs e)
            {
                _model.StatusMessage = e.Message;
            };

            surface.LinkReported += delegate(object sender, TerminalLinkMessageEventArgs e)
            {
                _model.StatusMessage = e.Message;
            };

            // The font and the colours only reach a renderer that exists, and the renderer is
            // built when the surface loads, so the settings are pushed again from there.
            surface.Loaded += delegate { ApplySettingsTo(pane); };
            return pane;
        }

        private TabViewModel CreateTab(object root)
        {
            TabViewModel tab = new TabViewModel();
            tab.RootPane = root;
            tab.FocusedPane = FirstPane(root);
            tab.Title = tab.FocusedPane == null ? "Terminal" : tab.FocusedPane.ShellDescription;
            tab.FullTitle = tab.Title;
            tab.CloseCommand = new ShellCommand(delegate { CloseTab(tab); });
            return tab;
        }

        private void NewTab()
        {
            ShellProfile shell = SelectedShell();
            PaneViewModel pane = CreatePane(shell, WorkingDirectoryForNewSession());

            TabViewModel tab = CreateTab(pane);
            _model.Tabs.Add(tab);
            _model.SelectedTab = tab;
            SetActivePane(pane);
            FocusLater(pane);
        }

        private string WorkingDirectoryForNewSession()
        {
            PaneViewModel active = _model.ActivePane;
            if (active != null && active.Surface != null)
            {
                return active.Surface.WorkingDirectory;
            }

            return _projectDirectory;
        }

        private void SplitRight()
        {
            Split(false);
        }

        private void SplitDown()
        {
            Split(true);
        }

        private void Split(bool stacked)
        {
            PaneViewModel active = _model.ActivePane;

            // The picker is labelled "profile for new sessions", so a split honours it too. The
            // opposite behaviour - inherit the pane you split from - is a setting, not a surprise.
            ShellProfile shell = _settings.SplitUsesActiveProfile && active != null && active.Surface != null
                ? active.Surface.Shell
                : SelectedShell();

            AddPane(shell, WorkingDirectoryForNewSession(), null, stacked);
        }

        /// <summary>
        /// Splits the focused pane, or opens a tab when there is nothing to split. Used by the
        /// split commands and by a connection that opens in the grid.
        /// </summary>
        private void AddPane(ShellProfile shell, string workingDirectory, string startupCommand, bool stacked)
        {
            TabViewModel tab = _model.SelectedTab;
            PaneViewModel active = _model.ActivePane;
            if (tab == null || active == null)
            {
                NewTabWith(shell, workingDirectory, startupCommand);
                return;
            }

            PaneViewModel created = CreatePane(shell, workingDirectory, startupCommand);

            SplitViewModel split = stacked
                ? (SplitViewModel)new RowSplitViewModel()
                : new ColumnSplitViewModel();
            split.First = active;
            split.Second = created;

            SplitViewModel parent = FindParent(tab.RootPane, active);
            if (parent == null)
            {
                tab.RootPane = split;
            }
            else
            {
                parent.Replace(active, split);
            }

            SetActivePane(created);
            FocusLater(created);
        }

        private List<PersistedFloating> CaptureFloating()
        {
            List<PersistedFloating> windows = new List<PersistedFloating>();
            if (_docking == null)
            {
                return windows;
            }

            foreach (FloatingPaneWindow window in _docking.FloatingWindows)
            {
                PersistedNode node = CaptureNode(window.Leaf);
                if (node == null)
                {
                    continue;
                }

                Rect32 bounds = window.Bounds;
                windows.Add(new PersistedFloating
                {
                    Left = bounds.Left,
                    Top = bounds.Top,
                    Width = bounds.Width,
                    Height = bounds.Height,
                    Node = node,
                });
            }

            return windows;
        }

        private void RestoreFloating(List<PersistedFloating> windows)
        {
            if (windows == null || _docking == null)
            {
                return;
            }

            foreach (PersistedFloating persisted in windows)
            {
                DockLeafViewModel leaf = RestoreNode(persisted.Node) as DockLeafViewModel;
                if (leaf == null)
                {
                    continue;
                }

                _docking.Restore(leaf, new Rect32
                {
                    Left = persisted.Left,
                    Top = persisted.Top,
                    Right = persisted.Left + persisted.Width,
                    Bottom = persisted.Top + persisted.Height,
                });
            }
        }

        // ===== docking: the pane tree seen as something leaves can be moved in and out of =====

        /// <summary>
        /// Told about the elements docking draws on once the window has built them. The workspace is
        /// constructed before the window's tree exists, so this cannot be a constructor argument.
        /// </summary>
        public void AttachDocking(FrameworkElement paneHost, DockOverlay overlay)
        {
            _paneHost = paneHost;
            _overlay = overlay;
            _docking = new DockController(this);
        }

        public DockController Docking
        {
            get { return _docking; }
        }

        FrameworkElement IDockHost.PaneHost
        {
            get { return _paneHost; }
        }

        DockOverlay IDockHost.Overlay
        {
            get { return _overlay; }
        }

        Window IDockHost.Owner
        {
            get { return _owner; }
        }

        IEnumerable<DockLeafViewModel> IDockHost.VisibleLeaves
        {
            get
            {
                return _model.SelectedTab == null
                    ? new List<DockLeafViewModel>()
                    : Leaves(_model.SelectedTab.RootPane);
            }
        }

        void IDockHost.Report(string message)
        {
            _model.StatusMessage = message;
        }

        void IDockHost.FocusLeaf(DockLeafViewModel leaf)
        {
            PaneViewModel pane = leaf as PaneViewModel;
            if (pane == null)
            {
                return;
            }

            SetActivePane(pane);
            FocusLater(pane);
        }

        void IDockHost.CloseLeaf(DockLeafViewModel leaf)
        {
            PaneViewModel pane = leaf as PaneViewModel;
            if (pane != null && pane.Surface != null)
            {
                pane.Surface.CloseSession();
            }
        }

        /// <summary>
        /// Lifts a leaf out of the tree, leaving whatever shared its split in its place. Nothing is
        /// closed: the caller is moving the leaf, not ending it.
        /// </summary>
        void IDockHost.RemoveLeaf(DockLeafViewModel leaf)
        {
            TabViewModel tab = FindTabOf(leaf);
            if (tab == null)
            {
                return;
            }

            SplitViewModel parent = FindParent(tab.RootPane, leaf);
            if (parent == null)
            {
                // It was the whole tab. The tab goes with it - an empty one would be a tab that
                // shows nothing and cannot be split back into use.
                tab.RootPane = null;
                DropTab(tab);
                return;
            }

            object sibling = parent.Other(leaf);
            SplitViewModel grandparent = FindParent(tab.RootPane, parent);

            if (grandparent == null)
            {
                tab.RootPane = sibling;
            }
            else
            {
                grandparent.Replace(parent, sibling);
            }

            PaneViewModel next = FirstPane(sibling);
            if (next != null)
            {
                SetActivePane(next);
            }
        }

        void IDockHost.InsertBeside(DockLeafViewModel leaf, DockLeafViewModel target, DockSide side)
        {
            TabViewModel tab = FindTabOf(target);
            if (tab == null)
            {
                ((IDockHost)this).InsertAtEdge(leaf, side);
                return;
            }

            SplitViewModel split = Branch(leaf, target, side);
            SplitViewModel parent = FindParent(tab.RootPane, target);

            if (parent == null)
            {
                tab.RootPane = split;
            }
            else
            {
                parent.Replace(target, split);
            }

            _model.SelectedTab = tab;
        }

        void IDockHost.InsertAtEdge(DockLeafViewModel leaf, DockSide side)
        {
            TabViewModel tab = _model.SelectedTab;
            if (tab == null)
            {
                tab = new TabViewModel();
                tab.Title = leaf.HeaderText;
                tab.FullTitle = leaf.HeaderText;
                _model.Tabs.Add(tab);
                _model.SelectedTab = tab;
            }

            if (tab.RootPane == null)
            {
                tab.RootPane = leaf;
                return;
            }

            SplitViewModel split = Branch(leaf, tab.RootPane, side);
            tab.RootPane = split;
        }

        void IDockHost.Replace(DockLeafViewModel leaf, DockLeafViewModel target)
        {
            TabViewModel tab = FindTabOf(target);
            if (tab == null)
            {
                ((IDockHost)this).InsertAtEdge(leaf, DockSide.Right);
                return;
            }

            SplitViewModel parent = FindParent(tab.RootPane, target);
            if (parent == null)
            {
                tab.RootPane = leaf;
            }
            else
            {
                parent.Replace(target, leaf);
            }

            _model.SelectedTab = tab;
        }

        /// <summary>
        /// The split that puts <paramref name="leaf"/> on the given side of <paramref name="other"/>.
        /// Left and Top mean the leaf goes first; the split's orientation follows from the axis.
        /// </summary>
        private static SplitViewModel Branch(DockLeafViewModel leaf, object other, DockSide side)
        {
            bool stacked = side == DockSide.Top || side == DockSide.Bottom;
            bool leafFirst = side == DockSide.Left || side == DockSide.Top;

            SplitViewModel split = stacked
                ? (SplitViewModel)new RowSplitViewModel()
                : new ColumnSplitViewModel();

            split.First = leafFirst ? leaf : other;
            split.Second = leafFirst ? other : leaf;
            return split;
        }

        private TabViewModel FindTabOf(DockLeafViewModel leaf)
        {
            foreach (TabViewModel tab in _model.Tabs)
            {
                if (Leaves(tab.RootPane).Contains(leaf))
                {
                    return tab;
                }
            }

            return null;
        }

        /// <summary>Removes an emptied tab without touching any session.</summary>
        private void DropTab(TabViewModel tab)
        {
            int index = _model.Tabs.IndexOf(tab);
            _model.Tabs.Remove(tab);

            if (_model.Tabs.Count == 0)
            {
                _model.SelectedTab = null;
                _model.RaiseActivePaneChanged();
                return;
            }

            _model.SelectedTab = _model.Tabs[Math.Max(0, Math.Min(_model.Tabs.Count - 1, index))];
        }

        private static List<DockLeafViewModel> Leaves(object node)
        {
            List<DockLeafViewModel> leaves = new List<DockLeafViewModel>();
            CollectLeaves(node, leaves);
            return leaves;
        }

        private static void CollectLeaves(object node, List<DockLeafViewModel> leaves)
        {
            DockLeafViewModel leaf = node as DockLeafViewModel;
            if (leaf != null)
            {
                leaves.Add(leaf);
                return;
            }

            SplitViewModel split = node as SplitViewModel;
            if (split == null)
            {
                return;
            }

            CollectLeaves(split.First, leaves);
            CollectLeaves(split.Second, leaves);
        }

        private void CloseActivePane()
        {
            PaneViewModel active = _model.ActivePane;
            if (active != null)
            {
                ClosePane(active);
            }
        }

        private void ClosePane(PaneViewModel pane)
        {
            TabViewModel tab = FindTab(pane);
            if (tab == null)
            {
                return;
            }

            if (pane.Surface != null)
            {
                pane.Surface.CloseSession();
            }

            SplitViewModel parent = FindParent(tab.RootPane, pane);
            if (parent == null)
            {
                CloseTab(tab);
                return;
            }

            object sibling = parent.Other(pane);
            SplitViewModel grandparent = FindParent(tab.RootPane, parent);

            if (grandparent == null)
            {
                tab.RootPane = sibling;
            }
            else
            {
                grandparent.Replace(parent, sibling);
            }

            PaneViewModel next = FirstPane(sibling);
            if (next != null)
            {
                SetActivePane(next);
                FocusLater(next);
            }
        }

        private void CloseTab(TabViewModel tab)
        {
            foreach (PaneViewModel pane in Panes(tab.RootPane))
            {
                if (pane.Surface != null)
                {
                    pane.Surface.CloseSession();
                }
            }

            int index = _model.Tabs.IndexOf(tab);
            _model.Tabs.Remove(tab);

            if (_model.Tabs.Count == 0)
            {
                _model.SelectedTab = null;
                _model.RaiseActivePaneChanged();
                return;
            }

            _model.SelectedTab = _model.Tabs[Math.Max(0, Math.Min(_model.Tabs.Count - 1, index))];
            FocusFirstPane(_model.SelectedTab);
        }

        private void OnPaneExited(PaneViewModel pane)
        {
            TabViewModel tab = FindTab(pane);
            if (tab == null)
            {
                return;
            }

            tab.HasFailed = pane.LastExitCode != 0;
            tab.LastExitDescription = "Last session exited with code " + pane.LastExitCode;
            _model.RaiseActivePaneChanged();
        }

        private void SetActivePane(PaneViewModel pane)
        {
            TabViewModel tab = FindTab(pane);
            if (tab == null)
            {
                return;
            }

            foreach (PaneViewModel other in Panes(tab.RootPane))
            {
                other.IsFocused = ReferenceEquals(other, pane);
            }

            tab.FocusedPane = pane;
            tab.Title = pane.ShellDescription;
            tab.FullTitle = pane.ShellDescription + " - " + pane.WorkingDirectory;

            if (ReferenceEquals(tab, _model.SelectedTab))
            {
                _model.RaiseActivePaneChanged();
            }
        }

        private void FocusFirstPane(TabViewModel tab)
        {
            if (tab == null)
            {
                return;
            }

            PaneViewModel pane = FirstPane(tab.RootPane);
            if (pane != null)
            {
                SetActivePane(pane);
                FocusLater(pane);
            }
        }

        private void FocusLater(PaneViewModel pane)
        {
            if (pane.Surface == null)
            {
                return;
            }

            _owner.Dispatcher.BeginInvoke(new Action(pane.Surface.FocusTerminal));
        }

        private void FocusNextPane(int direction)
        {
            TabViewModel tab = _model.SelectedTab;
            if (tab == null)
            {
                return;
            }

            List<PaneViewModel> panes = Panes(tab.RootPane);
            if (panes.Count == 0)
            {
                return;
            }

            int index = _model.ActivePane == null ? -1 : panes.IndexOf(_model.ActivePane);
            int next = ((index + direction) % panes.Count + panes.Count) % panes.Count;
            SetActivePane(panes[next]);
            FocusLater(panes[next]);
        }

        private void OpenPalette()
        {
            _palette.Show(PaletteCommands());
        }

        private IEnumerable<CommandItemViewModel> PaletteCommands()
        {
            List<CommandItemViewModel> commands = new List<CommandItemViewModel>(BuiltInCommands());

            // A project's own commands come first: they are the ones this folder is about.
            if (_project != null && _project.Commands != null)
            {
                List<CommandItemViewModel> project = new List<CommandItemViewModel>();
                foreach (PersistedCommand entry in _project.Commands)
                {
                    if (string.IsNullOrEmpty(entry.Name) || string.IsNullOrEmpty(entry.Text))
                    {
                        continue;
                    }

                    string line = entry.Text;
                    project.Add(Command(entry.Name, "Workspace", ProjectCommandGlyph, "",
                        delegate { SendToActivePane(line); }));
                }

                project.AddRange(commands);
                return project;
            }

            return commands;
        }

        private void SendToActivePane(string line)
        {
            PaneViewModel active = _model.ActivePane;
            if (active != null && active.Surface != null)
            {
                active.Surface.Write(line + "\r");
                FocusLater(active);
            }
        }

        private IEnumerable<CommandItemViewModel> BuiltInCommands()
        {
            return new[]
            {
                Command("New tab", "Tabs", "\uE710", "Ctrl+Shift+T", NewTab),
                Command("Split pane right", "Panes", "\uEE3F", "Alt+Shift+Plus", SplitRight),
                Command("Split pane down", "Panes", "\uEE40", "Alt+Shift+Minus", SplitDown),
                Command("Close pane", "Panes", "\uE89F", "Ctrl+Shift+W", CloseActivePane),
                Command("Move focus to the next pane", "Navigation", "\uE72A", "Alt+Right",
                    delegate { FocusNextPane(1); }),
                Command("Move focus to the previous pane", "Navigation", "\uE72B", "Alt+Left",
                    delegate { FocusNextPane(-1); }),
                Command("Restart the session in this pane", "Panes", "\uE72C", "", RestartActivePane),
                Command("New tab with Command Prompt", "Tabs", "\uE756", "",
                    delegate { NewTabWith(ShellProfile.CommandPrompt); }),
                Command("New tab with Windows PowerShell", "Tabs", "\uE756", "",
                    delegate { NewTabWith(ShellProfile.WindowsPowerShell); }),
                Command("New tab with CLI-AI Wizard", "Tabs", "\uE756", "",
                    delegate { NewTabWith(ShellProfile.CliAiWizard); }),
                Command("Open settings", "Application", "\uE713", "Ctrl+comma", OpenSettings),
                Command("Saved connections", "Application", "\uE8AF", "", OpenConnections),
                Command("Workspace setup", "Application", "\uE8B7", "", OpenWorkspaceSetup),
                Command("Files", "Application", "\uE8DA", "", OpenFiles),
                Command("About BetterTerminal", "Application", "\uE946", "", OpenAbout),
                Command("Open a link shown in this pane", "Panes", LinkCommandGlyph, "", OpenLinkPicker),
                Command("Open workspace folder", "Application", "\uE8E5", "", OpenWorkspaceFile)
            };
        }

        private static CommandItemViewModel Command(string name, string group, string glyph, string keys, Action run)
        {
            return new CommandItemViewModel
            {
                Name = name,
                Group = group,
                Glyph = glyph,
                KeysDisplay = string.IsNullOrEmpty(keys) ? "-" : keys,
                Source = "built-in",
                Run = run
            };
        }

        private void NewTabWith(ShellProfile shell)
        {
            NewTabWith(shell, WorkingDirectoryForNewSession(), null);
        }

        private void NewTabWith(ShellProfile shell, string workingDirectory, string startupCommand)
        {
            PaneViewModel pane = CreatePane(shell, workingDirectory, startupCommand);
            TabViewModel tab = CreateTab(pane);
            _model.Tabs.Add(tab);
            _model.SelectedTab = tab;
            SetActivePane(pane);
            FocusLater(pane);
        }

        private void RestartActivePane()
        {
            PaneViewModel active = _model.ActivePane;
            if (active != null && active.Surface != null)
            {
                active.Surface.Restart();
            }
        }

        private void OnPaletteInput(object sender, PaletteInputEventArgs e)
        {
            PaneViewModel active = _model.ActivePane;
            if (active != null && active.Surface != null)
            {
                active.Surface.Write(e.Line + "\r");
                FocusLater(active);
            }
        }

        private void OpenProfilePicker()
        {
            _palette.Show(ProfileCommands());
        }

        private void OpenLinkPicker()
        {
            _palette.Show(LinkCommands());
        }

        private IEnumerable<CommandItemViewModel> LinkCommands()
        {
            List<CommandItemViewModel> commands = new List<CommandItemViewModel>();

            PaneViewModel active = _model.ActivePane;
            TerminalSurface surface = active == null ? null : active.Surface;
            IList<TerminalLink> links = surface == null
                ? new List<TerminalLink>()
                : surface.VisibleLinks();

            for (int index = 0; index < links.Count; index++)
            {
                TerminalLink link = links[index];
                TerminalSurface pane = surface;
                commands.Add(Command(
                    index + 1 + ". " + TerminalLinkPolicy.Elide(link.Uri, 80),
                    "Links",
                    LinkCommandGlyph,
                    string.Empty,
                    delegate { pane.OpenLink(link); }));
            }

            if (commands.Count == 0)
            {
                commands.Add(Command("No link is on screen in this pane", "Links", LinkCommandGlyph,
                    string.Empty, delegate { }));
            }

            return commands;
        }

        private IEnumerable<CommandItemViewModel> ProfileCommands()
        {
            List<CommandItemViewModel> commands = new List<CommandItemViewModel>();
            foreach (ProfileViewModel profile in _model.Profiles)
            {
                ProfileViewModel captured = profile;
                commands.Add(Command("New tab: " + profile.Name, "Profiles", "\uE756", profile.Accelerator,
                    delegate { NewTabWith(captured.Shell); }));
            }

            return commands;
        }

        private void OpenSettings()
        {
            if (_settingsWindow != null)
            {
                _settingsWindow.Activate();
                return;
            }

            _settingsWindow = new SettingsWindow();
            _settingsWindow.Owner = _owner;
            _settingsWindow.DataContext = _settings;
            _settingsWindow.Closed += delegate { _settingsWindow = null; };
            _settingsWindow.Show();
        }

        private void OpenAbout()
        {
            AboutViewModel about = CreateAbout();

            AboutWindow window = new AboutWindow();
            window.Owner = _owner;
            window.DataContext = about;
            window.ShowDialog();
        }

        /// <summary>
        /// The files of the folder this window was opened in - the project when there is one, and
        /// otherwise the directory the focused session is sitting in.
        /// </summary>
        private void OpenFiles()
        {
            // A floating pane, the same window a torn-off pane gets, so dragging its header docks it.
            // Only one explorer at a time: a second would scan the same folder and the two would
            // disagree about what is saved.
            if (_filesPanel != null)
            {
                FloatingPaneWindow existing = _docking.WindowFor(_filesPanel);
                if (existing != null)
                {
                    existing.Activate();
                    return;
                }
            }

            ToolPaneViewModel leaf = CreateTool(ToolPaneViewModel.FilesKind);
            _docking.Open(leaf, 1020, 660);
        }

        private void OpenFile(FileExplorerViewModel model)
        {
            FileNodeViewModel node = model.SelectedNode;
            if (node == null || node.IsDirectory)
            {
                return;
            }

            FileDocumentViewModel already = model.Find(node.FullPath);
            if (already != null)
            {
                model.SelectedDocument = already;
                return;
            }

            FileDocumentViewModel document = new FileDocumentViewModel(node.FullPath);
            document.CloseCommand = new ShellCommand(delegate { model.Close(document); });
            model.Documents.Add(document);
            model.SelectedDocument = document;
            model.Message = "Opening " + node.Name;

            WorkspaceFiles.Open(node.FullPath,
                delegate(OpenedFile opened)
                {
                    document.Show(opened);
                    model.Message = document.FullPath;
                },
                delegate(string error)
                {
                    // The tab was opened optimistically; a file that will not load must not
                    // leave an empty one behind that a save could then write over it.
                    model.Close(document);
                    model.Message = error;
                });
        }

        private void SaveFile(FileExplorerViewModel model)
        {
            FileDocumentViewModel document = model.SelectedDocument;
            if (document == null || !document.IsDirty || document.IsReadOnly)
            {
                return;
            }

            model.Message = "Saving " + document.Name;

            WorkspaceFiles.Write(document.FullPath, document.Text, document.Encoding,
                delegate
                {
                    document.MarkSaved();
                    model.Message = "Saved " + document.FullPath;
                },
                delegate(string error) { model.Message = error; });
        }

        private void OpenWorkspaceFile()
        {
            string folder = Path.GetDirectoryName(SessionStore.FilePath);
            if (Directory.Exists(folder))
            {
                Process.Start("explorer.exe", folder);
            }
        }

        private void OpenDocumentation()
        {
            string readme = Path.Combine(
                Path.GetDirectoryName(typeof(TerminalWorkspace).Assembly.Location), "..\\..\\..\\README.md");

            if (File.Exists(readme))
            {
                Process.Start("explorer.exe", "/select,\"" + Path.GetFullPath(readme) + "\"");
            }
        }

        private void OnSettingsChanged(object sender, EventArgs e)
        {
            RefreshLinkOptions();

            foreach (TabViewModel tab in _model.Tabs)
            {
                foreach (PaneViewModel pane in Panes(tab.RootPane))
                {
                    ApplySettingsTo(pane);
                }
            }

            // Settings are written when they change, not only when the window closes: a session
            // that ends in a hard kill must not take the last change with it.
            Save();
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            foreach (TabViewModel tab in _model.Tabs)
            {
                tab.RefreshBrushes();
                foreach (PaneViewModel pane in Panes(tab.RootPane))
                {
                    pane.RefreshBrushes();
                }
            }
        }

        private void ApplySettingsTo(PaneViewModel pane)
        {
            if (pane.Surface != null)
            {
                ApplySettingsTo(pane.Surface);
            }
        }

        private void RefreshLinkOptions()
        {
            _linkOptions.DetectionEnabled = _settings.DetectLinks;
            _linkOptions.ConfirmMisleading = _settings.ConfirmLinks;
            _linkOptions.Activation = _settings.LinkActivation;
            _linkOptions.SetSchemes(TerminalLinkOptions.ParseSchemes(_settings.LinkSchemes));
        }

        private void ApplySettingsTo(TerminalSurface surface)
        {
            surface.ApplyFont(_settings.SelectedFont, _settings.FontSize);
            surface.ApplyCaret(ShapeFromName(_settings.CursorShapeName), _settings.BlinkCursor);
            surface.ApplyLinks(_linkOpener, _linkOptions);

            SchemeViewModel scheme = _settings.SelectedScheme;
            if (scheme != null)
            {
                surface.ApplyColors(scheme.Background, scheme.Foreground, scheme.Cursor, scheme.Selection);
            }
        }

        private static CaretShape ShapeFromName(string name)
        {
            if (name == "Bar")
            {
                return CaretShape.Bar;
            }

            return name == "Underline" ? CaretShape.Underline : CaretShape.Block;
        }

        private TabViewModel FindTab(PaneViewModel pane)
        {
            foreach (TabViewModel tab in _model.Tabs)
            {
                if (Panes(tab.RootPane).Contains(pane))
                {
                    return tab;
                }
            }

            return null;
        }

        private static SplitViewModel FindParent(object node, object child)
        {
            SplitViewModel split = node as SplitViewModel;
            if (split == null)
            {
                return null;
            }

            if (ReferenceEquals(split.First, child) || ReferenceEquals(split.Second, child))
            {
                return split;
            }

            return FindParent(split.First, child) ?? FindParent(split.Second, child);
        }

        private static PaneViewModel FirstPane(object node)
        {
            List<PaneViewModel> panes = Panes(node);
            return panes.Count == 0 ? null : panes[0];
        }

        private static List<PaneViewModel> Panes(object node)
        {
            List<PaneViewModel> panes = new List<PaneViewModel>();
            Collect(node, panes);
            return panes;
        }

        private static void Collect(object node, List<PaneViewModel> panes)
        {
            PaneViewModel pane = node as PaneViewModel;
            if (pane != null)
            {
                panes.Add(pane);
                return;
            }

            SplitViewModel split = node as SplitViewModel;
            if (split == null)
            {
                return;
            }

            Collect(split.First, panes);
            Collect(split.Second, panes);
        }

        private PersistedNode CaptureNode(object node)
        {
            PaneViewModel pane = node as PaneViewModel;
            if (pane != null && pane.Surface != null)
            {
                PersistedNode leaf = new PersistedNode();
                leaf.Kind = PersistedNode.PaneKind;
                leaf.ShellName = pane.Surface.Shell.Name;
                leaf.WorkingDirectory = pane.Surface.WorkingDirectory;
                return leaf;
            }

            ToolPaneViewModel tool = node as ToolPaneViewModel;
            if (tool != null)
            {
                PersistedNode leaf = new PersistedNode();
                leaf.Kind = PersistedNode.ToolKind;
                leaf.Tool = tool.Kind;
                return leaf;
            }

            SplitViewModel split = node as SplitViewModel;
            if (split == null)
            {
                return null;
            }

            PersistedNode branch = new PersistedNode();
            branch.Kind = PersistedNode.SplitKind;
            branch.Orientation = split is RowSplitViewModel ? "Row" : "Column";
            branch.FirstRatio = split.FirstRatio;
            branch.First = CaptureNode(split.First);
            branch.Second = CaptureNode(split.Second);
            return branch;
        }

        private object RestoreNode(PersistedNode node)
        {
            if (node == null)
            {
                return null;
            }

            if (node.Kind == PersistedNode.PaneKind)
            {
                return CreatePane(FindShell(node.ShellName), node.WorkingDirectory);
            }

            if (node.Kind == PersistedNode.ToolKind)
            {
                return CreateTool(node.Tool);
            }

            object first = RestoreNode(node.First);
            object second = RestoreNode(node.Second);
            if (first == null || second == null)
            {
                return first ?? second;
            }

            SplitViewModel split = node.Orientation == "Row"
                ? (SplitViewModel)new RowSplitViewModel()
                : new ColumnSplitViewModel();
            split.First = first;
            split.Second = second;

            if (node.FirstRatio > 0 && node.FirstRatio < 1)
            {
                split.FirstRatio = node.FirstRatio;
            }

            return split;
        }

        private ShellProfile FindShell(string name)
        {
            foreach (ProfileViewModel profile in _model.Profiles)
            {
                if (profile.Name == name && profile.Shell != null)
                {
                    return profile.Shell;
                }
            }

            return ShellProfile.CommandPrompt;
        }
    }
}
