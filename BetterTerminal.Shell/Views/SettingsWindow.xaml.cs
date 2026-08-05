using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace BetterTerminal.Shell.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// The size box commits when it is left, so Enter has to say "I am done" and the arrows
        /// have to step the value themselves - a box that only reacts to losing the focus feels
        /// broken otherwise.
        /// </summary>
        private void OnFontSizeKeyDown(object sender, KeyEventArgs e)
        {
            BindingExpression binding = FontSizeBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty);
            if (binding == null)
            {
                return;
            }

            if (e.Key == Key.Enter)
            {
                binding.UpdateSource();
                e.Handled = true;
                return;
            }

            if (e.Key != Key.Up && e.Key != Key.Down)
            {
                return;
            }

            int size;
            if (int.TryParse(FontSizeBox.Text, out size))
            {
                FontSizeBox.Text = (size + (e.Key == Key.Up ? 1 : -1)).ToString();
                binding.UpdateSource();
                FontSizeBox.CaretIndex = FontSizeBox.Text.Length;
            }

            e.Handled = true;
        }

        private void OnMinimize(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void OnToggleMaximize(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
