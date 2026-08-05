using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BetterTerminal.Shell.ViewModels;

namespace BetterTerminal.Shell.Views
{
    /// <summary>
    /// Overlay host for the command palette. The bundle shipped the markup only, so the keyboard
    /// contract advertised in its footer - arrows navigate, Enter runs, Esc dismisses, a leading
    /// "&gt;" sends the rest of the line to the shell - is implemented here.
    /// </summary>
    public partial class CommandPalette : UserControl
    {
        private const string ShellInputPrefix = ">";

        private readonly CommandPaletteViewModel _model = new CommandPaletteViewModel();

        public CommandPalette()
        {
            InitializeComponent();
            DataContext = _model;
            Visibility = Visibility.Collapsed;
        }

        public event EventHandler Dismissed;

        /// <summary>Raised when the query was a shell line rather than a command search.</summary>
        public event EventHandler<PaletteInputEventArgs> InputRequested;

        public void Show(IEnumerable<CommandItemViewModel> commands)
        {
            _model.Reset(commands);
            Visibility = Visibility.Visible;
            Query.Focus();
            Query.SelectAll();
        }

        public void Hide()
        {
            if (Visibility != Visibility.Visible)
            {
                return;
            }

            Visibility = Visibility.Collapsed;

            EventHandler handler = Dismissed;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void OnScrimClick(object sender, MouseButtonEventArgs e)
        {
            Hide();
        }

        private void OnQueryKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    Hide();
                    e.Handled = true;
                    break;

                case Key.Enter:
                    Activate();
                    e.Handled = true;
                    break;

                case Key.Down:
                case Key.Up:
                    MoveSelection(e.Key == Key.Down ? 1 : -1);
                    e.Handled = true;
                    break;
            }
        }

        private void OnResultActivated(object sender, MouseButtonEventArgs e)
        {
            Activate();
        }

        private void MoveSelection(int delta)
        {
            int count = ResultList.Items.Count;
            if (count == 0)
            {
                return;
            }

            int index = Math.Max(0, Math.Min(count - 1, ResultList.SelectedIndex + delta));
            ResultList.SelectedIndex = index;
            ResultList.ScrollIntoView(ResultList.SelectedItem);
        }

        private void Activate()
        {
            string query = _model.Query;

            if (!string.IsNullOrEmpty(query) && query.StartsWith(ShellInputPrefix, StringComparison.Ordinal))
            {
                string line = query.Substring(ShellInputPrefix.Length).TrimStart();
                Hide();

                EventHandler<PaletteInputEventArgs> input = InputRequested;
                if (input != null && line.Length > 0)
                {
                    input(this, new PaletteInputEventArgs(line));
                }

                return;
            }

            CommandItemViewModel selected = _model.SelectedResult;
            Hide();

            if (selected != null && selected.Run != null)
            {
                selected.Run();
            }
        }
    }
}
