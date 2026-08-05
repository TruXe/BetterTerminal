---
updated: 2026-08-05
scope: Where every part of BetterTerminal lives and where new code belongs after the design-system import; it does not explain how to build, run or debug (see WORKFLOWS.md) and does not restate coding rules (see RULES.md).
stability: evolving
sources: [BetterTerminal.sln, BetterTerminal.Shell/BetterTerminal.Shell.csproj, BetterTerminal.Terminal/BetterTerminal.Terminal.csproj, BetterTerminal.Interop/BetterTerminal.Interop.csproj, BetterTerminal.Shell/App.xaml(.cs), BetterTerminal.Shell/Views/MainWindow.xaml(.cs), BetterTerminal.Shell/Views/TerminalSurface.cs, BetterTerminal.Shell/Services/TerminalWorkspace.cs, BetterTerminal.Shell/Services/ThemeService.cs, BetterTerminal.Shell/Services/SystemPreference.cs, BetterTerminal.Shell/ViewModels/*.cs, BetterTerminal.Shell/Themes/*.xaml, BetterTerminal.Shell/PersistedWorkspace.cs, BetterTerminal.Shell/SessionStore.cs, BetterTerminal.Terminal/VtParser.cs, BetterTerminal.Terminal/VtKeyEncoder.cs, BetterTerminal.Terminal/CaretShape.cs, BetterTerminal.Interop/NativeMethods.cs]
owner_agent: structure-agent
---

# STRUCTURE

Five classic (non-SDK) C# projects, .NET Framework 4.8, C# 7.3, x64 only, no NuGet packages.
Solution: `BetterTerminal.sln`. The app assembly is `BetterTerminal.exe`, built from
`BetterTerminal.Shell` (root namespace `BetterTerminal.Shell`); `BetterTerminal.Wrap` is a separate
console program, `beterm-wrap.exe`, and nothing in the application depends on it.
`BetterTerminal.Banner` builds `beterm-banner.exe`, which the Shell references only so that it lands
in its output and can be installed beside the `beterm` command - no code here calls it, the shells do. The Shell was restructured on
2026-08-04 into MVVM plus a design-system token stack; `Theme.xaml`, `MainWindow` at the project
root, `TerminalPane`, `SplitPane` and `PaletteCommand` no longer exist.

## Directory map

```
.                                   repository root (path contains "Multi Terminál Window" - non-ASCII)
├── BetterTerminal.sln              Debug|x64 and Release|x64 only; no x86, no AnyCPU
├── .gitignore                      bin/, obj/, .vs/, *.user, *.suo, docs/_archive/*/flood.txt
├── CLAUDE.md RULES.md STRUCTURE.md WORKFLOWS.md MEMORY.md TIPS.md DOCS.md AGENTS.md README.md
├── BetterTerminal.Interop/         class library - every P/Invoke, SafeHandle and Win32 struct
│   ├── NativeMethods.cs            all DllImports (kernel32, user32, ntdll) + Win32 constants
│   ├── Safe*.cs                    SafeKernelHandle, SafePseudoConsoleHandle, SafeProcThreadAttributeList
│   ├── Coord/StartupInfo/StartupInfoEx/ProcessInformation/SecurityAttributes/IoCounters/
│   │   JobObject*Information/OsVersionInfo.cs   blittable Win32 structs
│   ├── ConsoleWindowStyles.cs EnumWindowsProc.cs Win32Error.cs
│   └── bin/ obj/                   GENERATED
├── BetterTerminal.Terminal/        class library - session contract, both backends, VT, renderer
│   ├── ITerminalSession.cs         the one contract both backends implement
│   ├── ConPtySession.cs            pseudo-console backend (default on OS build 17763+)
│   ├── HwndConsoleSession.cs ConsoleHwndHost.cs   fallback backend and its HwndHost
│   ├── TerminalSessionFactory.cs   Resolve()/Create(); DefaultScrollbackLines = 5000
│   ├── VtParser.cs VtKeyEncoder.cs CellGrid.cs TerminalCell.cs CellFlags.cs TerminalPalette.cs
│   ├── TerminalRenderer.cs         FrameworkElement; SetFontFamily, Redraw, CaretShape, CaretBlinks
│   ├── CaretShape.cs               enum Block | Bar | Underline
│   ├── TerminalBackend.cs ShellProfile.cs ProcessJob.cs Terminal*EventArgs.cs
│   └── bin/ obj/                   GENERATED
├── BetterTerminal.Shell/           WinExe - WPF app, MVVM
│   ├── App.xaml(.cs)               merges the 8 theme dictionaries; OnStartup shows splash + main
│   ├── Themes/                     design-system dictionaries, merged in tier order by App.xaml
│   │   ├── Primitives.xaml         raw palette, Bt.Color.* (59 keys) - no semantics
│   │   ├── Tokens.Dark.xaml / Tokens.Light.xaml / Tokens.HighContrast.xaml   semantic Bt.*Brush
│   │   ├── Typography.xaml Motion.xaml Brand.xaml Converters.xaml Controls.xaml
│   │   └── Schemes/                terminal colour schemes: Campbell, OneHalfDark, SolarizedDark
│   ├── Views/                      windows and the one custom control
│   │   ├── MainWindow.xaml(.cs)    chrome, tab strip, KeyBindings, the pane-tree DataTemplates
│   │   ├── CommandPalette.xaml(.cs) PaletteInputEventArgs.cs
│   │   ├── SettingsWindow.xaml(.cs) AboutWindow.xaml(.cs) SplashWindow.xaml(.cs)
│   │   ├── ConnectionsWindow.xaml(.cs)   saved connections: list, add form, reachability hearts
│   │   ├── WorkspaceSetupWindow.xaml(.cs) project settings, commands and values
│   │   ├── SessionWindow.xaml(.cs) one session outside the pane grid
│   │   └── TerminalSurface.cs      ContentControl owning one session; no chrome of its own
│   ├── ViewModels/                 MainViewModel, TabViewModel, PaneViewModel, SplitViewModel +
│   │                               ColumnSplitViewModel / RowSplitViewModel, CommandPaletteViewModel,
│   │                               CommandItemViewModel, SettingsViewModel, SettingsPageViewModel,
│   │                               SchemeViewModel, ProfileViewModel, AboutViewModel,
│   │                               ObservableObject (INotifyPropertyChanged base), SampleData
│   ├── Services/                   TerminalWorkspace.cs (the wiring core), ThemeService.cs,
│   │                               MotionPolicy.cs, SystemPreference.cs, AppTheme.cs,
│   │                               StartupOptions.cs (command line), SelfInstall.cs (the copy
│   │                               under %LOCALAPPDATA%), CommandRegistration.cs (the beterm
│   │                               shim), HostReachability.cs (port 22 probe)
│   ├── Converters/                 BoolToVisibilityConverter.cs
│   ├── Assets/BetterTerminal.ico   Resource item and <ApplicationIcon>
│   ├── SessionStore.cs Persisted{Workspace,Tab,Node}.cs ShellCommand.cs   kept at the root
│   ├── JsonFile.cs                 the one JSON read/write used by all three stores
│   ├── ProjectStore.cs PersistedProject.cs        <project>\.beterm\project.json
│   ├── ConnectionStore.cs PersistedConnections.cs %APPDATA%\...\connections.json
│   ├── app.manifest                PerMonitorV2 DPI, supportedOS Windows 8.1/10/11
│   ├── Properties/AssemblyInfo.cs
│   └── bin/ obj/                   GENERATED - bin\x64\<Config>\BetterTerminal.exe
├── BetterTerminal.Banner/          console Exe - beterm-banner.exe, what a session prints when it
│   │                               opens; Palette/AnsiWriter/Spinner are LINKED in from Wrap
│   ├── Program.cs SessionBanner.cs the mark, the facts, the typing and the spinner
│   └── bin/ obj/                   GENERATED
├── BetterTerminal.Wrap/            console Exe - beterm-wrap.exe, a front end for tools\*.ps1
│   ├── Program.cs WrapApplication.cs   entry, the redraw and key loop
│   ├── TerminalMode.cs AnsiWriter.cs   console mode, encodings, alternate screen, frame building
│   ├── Palette.cs                  the dark theme's colours, the only place one is written down
│   ├── InputField.cs               the editable line: caret, scrolling, its own drawing
│   ├── ScriptCatalog.cs ScriptEntry.cs ScriptParameter.cs RunRequest.cs   what a script is
│   ├── ChildProcess.cs OutputLog.cs    streamed and pass-through runs, scrollback
│   ├── Screen.cs Picker/Argument/Output/ResultScreen.cs   the four screens
│   └── bin/ obj/                   GENERATED
├── tools/                          capture-window.ps1 ui-smoke.ps1 flood-benchmark.ps1 session-cycle.ps1
├── docs/_archive/2026-08-04/       superseded markdown + ARCHIVE-INDEX.md
└── .vs/                            GENERATED - Visual Studio local state
```

No test project, no build script, no CI directory.

## Entry points

| entry | file | what triggers it |
| --- | --- | --- |
| Application startup | `BetterTerminal.Shell/App.xaml.cs` `OnStartup` | launching `BetterTerminal.exe`. There is no `StartupUri`; `ShutdownMode="OnMainWindowClose"`. It parses the command line into `StartupOptions.Current`, calls `CommandRegistration.Ensure()`, then `ThemeService.Current.Initialize(Resources)`, shows `SplashWindow`, then `MainWindow`, closing the splash on its `Loaded` |
| Self-install | `Services/SelfInstall.cs` `Ensure` | every start: copies `BetterTerminal.exe` and its two DLLs into `%LOCALAPPDATA%\BetterTerminal\app`, refreshing files the running build is newer than. A process already running from there does nothing |
| Launch from a shell prompt | `%LOCALAPPDATA%\BetterTerminal\bin\beterm.cmd` -> `%~dp0..\app\BetterTerminal.exe --project "%CD%"` | typing `beterm` at a command prompt. The shim and the search-path entry are written by `CommandRegistration.Ensure()` on every start; the shim reaches the installed copy **relative to itself**, so it holds no absolute path |
| Project restore | `TerminalWorkspace.RestoreProject` | `Restore()` when `StartupOptions.Current.HasProject`; reads or creates `<project>\.beterm\project.json`, opens one tab in that folder and posts the setup window |
| Saved connections | `TerminalWorkspace.OpenConnections` | the header SSH button or the palette; probes each host with `HostReachability`, and a chosen connection opens a pane or a `SessionWindow` with the line typed into it |
| Resource stack | `BetterTerminal.Shell/App.xaml` | 8 merged dictionaries in tier order: Primitives, Tokens.Dark, Typography, Motion, Brand, Converters, Controls, Schemes/Campbell |
| Window construction | `Views/MainWindow.xaml.cs` ctor | creates `MainViewModel` and `new TerminalWorkspace(_model, this, Palette)`, which assigns every `ICommand` on the view model |
| Workspace restore | `MainWindow.OnLoaded` -> `TerminalWorkspace.Restore()` | window `Loaded`; reads `workspace.json` or opens one default tab |
| Save + teardown | `MainWindow.OnClosing` -> `Save()` then `CloseAllSessions()` | window `Closing` |
| Keyboard shortcuts | `Views/MainWindow.xaml` `Window.InputBindings` | Ctrl+Shift+T new tab, Ctrl+Shift+W close pane, Ctrl+Shift+P palette, Alt+Shift+OemPlus split right, Alt+Shift+OemMinus split down, Ctrl+OemComma settings - all bound to `MainViewModel` commands |
| Command palette | `TerminalWorkspace.OpenPalette` -> `CommandPalette.Show(PaletteCommands())` | Ctrl+Shift+P or the palette button |
| Theme / scheme switch | `Services/ThemeService.cs` `Theme` and `SchemeName` setters | the settings window, or `SystemParameters.StaticPropertyChanged` for high contrast |
| Session creation | `Views/TerminalSurface.cs` `StartSession` via `TerminalSessionFactory.Create` | the surface's `Loaded` event |
| Backend choice | `TerminalSessionFactory.Resolve` + `ConPtySession.IsSupported` | first session; `IsSupported` asks ntdll `RtlGetVersion`, not `Environment.OSVersion` |
| Build | `BetterTerminal.sln` | MSBuild with `/p:Configuration=<Debug\|Release> /p:Platform=x64` |

## Data flow

**Output (shell -> screen), ConPTY backend.** `ConPtySession` creates two pipes and a pseudo
console, launches the shell with `EXTENDED_STARTUPINFO_PRESENT`, and a reader thread decodes UTF-8
incrementally and feeds `VtParser`. The parser mutates `CellGrid` **on that reader thread**, holding
`CellGrid.SyncRoot`. `TerminalRenderer` repaints on a 16 ms `DispatcherTimer`, only rows whose
per-line version stamp changed. `OSC 0/2` raises `TitleChanged`, which `TerminalSurface` forwards to
`PaneViewModel.SessionTitle` and from there to the pane chrome in the DataTemplate.

**Input (keys -> shell).** `TerminalRenderer` receives WPF key events, `VtKeyEncoder.Encode` turns
them into VT bytes, and the session's `Write(string)` enqueues on a `BlockingCollection<byte[]>`
drained by a writer thread, so a full pipe never blocks the UI. `VtKeyEncoder` returns null for
Ctrl+Shift+letter so those fall through to the window's `InputBindings`. Parser answerbacks (DSR,
DA) travel the same way through `ResponseWriter`.

**Layout (a view-model tree, not a visual tree).** `MainViewModel.Tabs` holds `TabViewModel`s; each
`TabViewModel.RootPane` is either a `PaneViewModel` or a `SplitViewModel`. `Views/MainWindow.xaml`
renders that tree with implicit `DataTemplate`s keyed by type inside one `ContentControl`:
`PaneViewModel` draws the pane chrome and hosts the `TerminalSurface`, while `ColumnSplitViewModel`
and `RowSplitViewModel` each draw a `Grid` with two nested `ContentControl`s and a `GridSplitter`.
Two concrete split classes exist because WPF cannot select a template on a property.
`SplitViewModel` has a real `Replace(oldChild, newChild)`: view models have no logical parent, so
the detach-before-attach dance of the old `SplitPane` is gone.

**Settings and theming.** `SettingsViewModel` raises `Changed`; `TerminalWorkspace.OnSettingsChanged`
pushes font, caret shape and blink onto every live `TerminalSurface` through `ApplyFont`/`ApplyCaret`
and sets `ThemeService.Theme` / `ThemeService.SchemeName`. `ThemeService` swaps
`Application.Resources.MergedDictionaries[1]` (theme tokens) and `[7]` (terminal scheme) and raises
`ThemeChanged`; `TerminalWorkspace.OnThemeChanged` calls `ApplyColors` on each surface, which reads
the `Bt.Scheme.*` colours out of the application resources.

**Opening a folder as a project.** `beterm.cmd` passes `--project "%CD%"`; `StartupOptions.Parse`
turns that into `ProjectDirectory` (it also accepts a bare path, so a shortcut behaves the same).
`Restore()` then takes the project branch: `ProjectStore.Load` reads `<project>\.beterm\project.json`
- creating and hiding the folder on first open - one tab opens in that directory with the project's
shell and startup line, and the setup window is posted to the dispatcher afterwards when the project
is new or asks for it. The project's own commands are prepended to the command palette under the
group "Workspace" and are sent to the focused session as typed lines.

**Saved connections.** `ConnectionStore` holds a user name and an address per entry in
`%APPDATA%\BetterTerminal\connections.json` - per user, never per project. `ConnectionsViewModel` is
state plus three events (`Changed`, `RefreshRequested`, `ConnectRequested`); `TerminalWorkspace` does
the file writing, the probing through `HostReachability` and the connecting. The status glyph and its
colour are chosen in `Views\ConnectionsWindow.xaml` by `DataTrigger` on `ConnectionStatus`, not in
the view model.

**Persistence.** `TerminalWorkspace.CaptureNode` walks the view-model tree into
`PersistedWorkspace -> PersistedTab -> PersistedNode` (kind `"pane"`/`"split"`, shell name, working
directory, orientation, `firstRatio`) and `SessionStore.Save` writes
`%APPDATA%\BetterTerminal\workspace.json` with `DataContractJsonSerializer`. The workspace record
also carries `theme`, `scheme`, `fontFamily`, `fontSize`, `cursorShape` and `blinkCursor`.

**Fallback backend.** When `ConPtySession.IsSupported` is false, `HwndConsoleSession` launches
`conhost.exe "<shell>"`, finds the `ConsoleWindowClass` window by PID via `EnumWindows`, and
`ConsoleHwndHost` reparents it with `SetParent`. That path has no grid, parser or renderer; its
`Write` throws `NotSupportedException` and `OutputReceived` never fires.

## Where to add things

| task | destination path | copy this as the pattern |
| --- | --- | --- |
| A new VT escape sequence (CSI final byte) | `BetterTerminal.Terminal/VtParser.cs`, a `case` in `Dispatch(char final)` | the `case 'L'` / `case 'M'` insert-and-delete-line arms; unknown sequences stay dropped silently, never printed |
| A new DEC private mode (`?h`/`?l`) | `BetterTerminal.Terminal/VtParser.cs`, `SetMode(bool enabled)` | the existing arms for 1, 7, 25, 1049, 2004 |
| A new SGR attribute | `BetterTerminal.Terminal/VtParser.cs`, `ApplySimpleRendition(int code)` | the bold/underline arms; the bit itself goes in `CellFlags.cs` |
| A new key-to-escape mapping | `BetterTerminal.Terminal/VtKeyEncoder.cs`, `Encode` | `case Key.F5:`; return null when the key must reach the window shortcuts instead |
| A new shell profile | `BetterTerminal.Terminal/ShellProfile.cs` as a static property, then register it in `Services/TerminalWorkspace.cs` `BuildProfiles()` | `ShellProfile.WindowsPowerShell` - resolve the exe under `Environment.SpecialFolder.System`, never a hardcoded `C:\`; the profile `Name` is what `workspace.json` stores. To give it the shell presentation as well, add an arm to `Services/ShellPresentation.cs` keyed on its executable name |
| A change to how a shell looks when it starts | `Services/ShellPresentation.cs` | the `cmd.exe` arm: banner cleared with `cls`, path coloured through `PROMPT $E`; the PowerShell arm passes its script `-EncodedCommand` so no layer in between reinterprets the quoting. User text never goes on the command line - the project name travels in an environment variable and is reduced to harmless characters first |
| A new P/Invoke | `BetterTerminal.Interop/NativeMethods.cs` only | the `CreatePseudoConsole` / `CreateWindowExW` entries: explicit `SetLastError`, explicit `CharSet.Unicode` on W functions, `EntryPoint` when names differ |
| A new Win32 struct or handle type | its own file in `BetterTerminal.Interop/` | `StartupInfoEx.cs` (explicit `Pack`, IntPtr-sized pointer fields); `SafePseudoConsoleHandle.cs` for handles |
| A new theme token | the raw colour in `Themes/Primitives.xaml` (`Bt.Color.*`), then the semantic brush in **all three** of `Tokens.Dark.xaml`, `Tokens.Light.xaml`, `Tokens.HighContrast.xaml` | the `Bt.AccentFillDefaultBrush` chain; a token missing from one tier breaks only in that theme, at runtime |
| A new terminal colour scheme | new `Themes/Schemes/<Name>.xaml` + a `<Page>` item in the csproj + a row in `TerminalWorkspace.BuildSchemes()` | `Themes/Schemes/OneHalfDark.xaml` - all 21 `Bt.Scheme.*` keys must be present; `SchemeViewModel.DictionaryName` is the file name without extension and is what `ThemeService.SchemeName` loads |
| A new control style | `Themes/Controls.xaml` | `Bt.Button.Secondary` (`BasedOn="{StaticResource Bt.Button.Base}"`); never a literal colour, always a `Bt.*Brush` token |
| A new command-palette entry | `Services/TerminalWorkspace.cs`, the array returned by `PaletteCommands()` | `Command("Close pane", "Panes", "\uE89F", "Ctrl+Shift+W", CloseActivePane)` - name, group, Segoe Fluent glyph, key display, action |
| A new keyboard shortcut | `Views/MainWindow.xaml` `Window.InputBindings`, bound to an `ICommand` on `MainViewModel` | `<KeyBinding Modifiers="Alt+Shift" Key="OemPlus" Command="{Binding SplitRightCommand}"/>`; declare the property in `MainViewModel`, assign it in the `TerminalWorkspace` constructor, and mirror the key text in the palette entry |
| A new pane / tab command | `Services/TerminalWorkspace.cs` next to `SplitRight`, `ClosePane`, `FocusNextPane` | `Split(bool stacked)` - it builds a `ColumnSplitViewModel` or `RowSplitViewModel` and calls `SplitViewModel.Replace`; tree helpers `FindParent`, `Panes`, `FirstPane` already exist |
| A new settings control | property + `Changed` on `ViewModels/SettingsViewModel.cs`, UI in `Views/SettingsWindow.xaml`, application in `TerminalWorkspace.ApplySettingsTo` | the caret group: `IsCursorBar`/`IsCursorBlock`/`IsCursorUnderline` + `CursorShapeName`, rendered with `Bt.Segment`, applied through `TerminalSurface.ApplyCaret` |
| A new settings page | the `Pages` collection in `ViewModels/SettingsViewModel.cs` + a panel in `Views/SettingsWindow.xaml` | the existing `SettingsPageViewModel { Title, Glyph }` entries bound to the `Bt.NavItem` list |
| A new per-project setting | a `[DataMember]` on `PersistedProject.cs`, a property on `ViewModels/WorkspaceSetupViewModel.cs`, a control in `Views/WorkspaceSetupWindow.xaml`, read in `TerminalWorkspace.BuildSetupModel` and written in `ApplySetup` | `[DataMember(Name = "startupCommand")]`; the file is `<project>\.beterm\project.json` and a user may commit it, so nothing secret belongs there |
| A new persisted setting | a `[DataMember]` on `PersistedWorkspace.cs` (or `PersistedTab`/`PersistedNode`), written in `TerminalWorkspace.Save`, read in `Restore`, pushed through `SettingsViewModel.ApplyStored` | `[DataMember(Name = "cursorShape")] public string CursorShape` - explicit lowercase JSON name; `SessionStore.Load` turns a `SerializationException` into a fresh layout, so additions are backwards compatible |
| A new window | `Views/<Name>Window.xaml(.cs)` + a `<Page>` and a `<Compile DependentUpon>` item + a view model in `ViewModels/` + an opener in `TerminalWorkspace` | `Views/AboutWindow.xaml` with `TerminalWorkspace.OpenAbout()` (set `Owner`, bind a view model, wire its `ShellCommand`s) |
| A new value converter | `Converters/<Name>Converter.cs` + an instance in `Themes/Converters.xaml` | `BoolToVisibilityConverter`, exposed as `Bt.BoolToVisibility` |
| A new terminal colour index | `BetterTerminal.Terminal/TerminalPalette.cs`, inside `BuildXterm256()` | the Campbell 16 + 6x6x6 cube + 24 greys construction; `Get(int)` and `FromRgb` are the only public surface |
| A new source or XAML file in any project | add `<Compile Include="Folder\File.cs" />` or `<Page Include="Folder\File.xaml" />` to that project's `.csproj` | the `<Compile Include="Services\ThemeService.cs" />` line. **Classic csproj files do not glob.** With five folders under the Shell this is the most common way to add a file that silently never compiles - and a XAML file with no `<Page>` item fails at runtime, not at build time |
| A new verification script | `tools/` | `tools/ui-smoke.ps1` - parameterised paths, writes a `RESULT:` line to a log; session-driving scripts must run via `Start-Process powershell -WindowStyle Hidden -Wait` (see TIPS.md #environment-quirks) |

## Boundaries

**`BetterTerminal.Wrap` invokes the scripts and never touches them.** It reads no `.ps1`, `.bat` or
`.cmd` file and writes none; `ScriptCatalog` is a transcription of what each script declares, which
is why a change to a script's parameters has to be repeated there by hand. Its one dependency on the
rest of the repository is `ProcessJob`, so a cancelled or crashed run takes the child tree with it.
A script marked `TakesOverTerminal` is run with nothing redirected and with the interface withdrawn -
both the scripts that start their own shell and anything interactive need a real console, not pipes.

**Dependency direction is one-way: `Interop <- Terminal <- Shell`, and `Interop, Terminal <- Wrap`.** `BetterTerminal.Interop`
references only `System` and `System.Core`, with no project reference and no WPF reference.
`BetterTerminal.Terminal` project-references `Interop` and adds `PresentationCore`,
`PresentationFramework`, `System.Xaml`, `WindowsBase`. `BetterTerminal.Shell` project-references
both and adds `System.Runtime.Serialization` and `System.Xml`.

**Interop lives only in `BetterTerminal.Interop`.** Every `DllImport` in the repository is in
`NativeMethods.cs`, `SafeKernelHandle.cs` or `SafePseudoConsoleHandle.cs` - three files in that one
project, unchanged by the restructure. No `DllImport`, Win32 struct or raw handle arithmetic may
appear in `Terminal` or `Shell`; only `ConPtySession.cs`, `HwndConsoleSession.cs`,
`ConsoleHwndHost.cs` and `ProcessJob.cs` carry `using BetterTerminal.Interop`.

**The Shell still does not touch interop.** No file under `BetterTerminal.Shell` has a
`using BetterTerminal.Interop`, despite the project reference. Its one OS-preference read is
`Services/SystemPreference.cs` through `Microsoft.Win32.Registry` - a managed API, not P/Invoke. A
UI file that needs a Win32 call means the call belongs behind a `Terminal` type instead.

**`ThemeService` is the only code allowed to touch `MergedDictionaries`.** Verified: the two
assignments in `Services/ThemeService.cs` are the only ones in the codebase. Slot 1 is the theme
tier and slot 7 the terminal scheme, so **inserting a dictionary into `App.xaml` shifts those
indices and breaks theming silently** - if you add a tier, append it and update the constants.

**`TerminalWorkspace` is the only class that mutates the pane tree.** View models are state only:
they raise `PropertyChanged` and expose `ICommand` properties that the workspace fills in. A view
model must not create a session, open a window or read `workspace.json`.

**`Views/TerminalSurface.cs` is the only UI type that touches a session or branches on backend.** It
holds the `ITerminalSession` and chooses between `TerminalRenderer` and `ConsoleHwndHost`. No XAML,
no view model and no other view may name a concrete backend type.

**`CellGrid.SyncRoot` is a hard boundary, not a convention.** Every mutating member requires the
caller to hold it: the parser runs on the reader thread, the renderer on the UI thread.

**Colour, font, spacing and motion come only from `Themes/`.** Markup uses `Bt.*` keys;
`Primitives.xaml` holds raw colours with no semantics, the `Tokens.*` tier holds the semantics, and
no literal colour belongs in C# or in a view.

**Names of external APIs and packages are internal.** ConPTY, conhost, WPF and Win32 may appear in
code comments and in these documents, never in window chrome, pane headers, status bars or any
user-visible message (see RULES.md #never-do).

> ❓ Unverified: the palette advertises `Alt+Right` / `Alt+Left` for "Move focus to the next/previous
> pane", but `Views/MainWindow.xaml` registers no `KeyBinding` for either and no `ModifierKeys.Alt`
> handler for them exists in the Shell - that shortcut text may be aspirational.

> ❓ Unverified: `tools/ui-smoke.ps1` drives buttons by automation name, but the names in
> `Views/MainWindow.xaml` are now "Split pane right", "Split pane down", "Close the focused pane"
> and "Open the command palette" - the script's own name list was not re-read this run.

---
[← CLAUDE.md](CLAUDE.md) · [STRUCTURE](STRUCTURE.md) · [WORKFLOWS](WORKFLOWS.md) · [MEMORY](MEMORY.md)
