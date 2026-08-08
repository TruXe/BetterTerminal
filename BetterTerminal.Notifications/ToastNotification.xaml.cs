using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace BetterTerminal.Notifications
{
    /// <summary>
    /// Windows 11 style toast notification. Geometry and colour are measured from the reference
    /// screenshot (364 x 157 DIP); the surface is the DWM acrylic backdrop with a solid tinted
    /// fallback on builds that do not expose it.
    ///
    /// It is drawn by the application itself rather than routed through the notification centre, so
    /// it shows whether or not the account has Windows notifications turned on - the case the update
    /// notice has to survive when the service raises it with nothing of ours on the desktop.
    /// </summary>
    public partial class ToastNotification : Window
    {
        private const double ScreenGap = 12d;
        private const double StackGap = 12d;

        private static readonly List<ToastNotification> Open = new List<ToastNotification>();

        private readonly ObservableCollection<ToastAction> _actions = new ObservableCollection<ToastAction>();
        private DispatcherTimer _dismissTimer;
        private bool _closing;

        public ToastNotification()
        {
            InitializeComponent();

            ActionsHost.ItemsSource = _actions;
            CloseButton.Click += delegate { DismissAsync(); };
            MoreButton.Click += delegate
            {
                EventHandler more = MoreRequested;
                if (more != null)
                {
                    more(this, EventArgs.Empty);
                }
            };

            MouseEnter += delegate { if (_dismissTimer != null) { _dismissTimer.Stop(); } };
            MouseLeave += delegate { if (_dismissTimer != null && !_closing) { _dismissTimer.Start(); } };

            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        /// <summary>Raised when the "..." button is clicked.</summary>
        public event EventHandler MoreRequested;

        /// <summary>Raised with the clicked action; fires before the toast dismisses.</summary>
        public event EventHandler<ToastAction> ActionInvoked;

        public string AppName
        {
            get { return AppNameText.Text; }
            set { AppNameText.Text = value; }
        }

        // Deliberately shadows Window.Title: this is the toast's bold first line, not the caption of
        // a frameless window that never shows one.
        public new string Title
        {
            get { return TitleText.Text; }

            set
            {
                TitleText.Text = value;
                TitleText.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public string Message
        {
            get { return MessageText.Text; }

            set
            {
                MessageText.Text = value;
                MessageText.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        /// <summary>How long the toast stays on screen. <see cref="TimeSpan.Zero"/> keeps it open.</summary>
        public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(7);

        public IList<ToastAction> Actions
        {
            get { return _actions; }
        }

        // ---- Acrylic backdrop -------------------------------------------------

        private const int DwmwaUseImmersiveDarkMode = 20;
        private const int DwmwaWindowCornerPreference = 33;
        private const int DwmwaSystemBackdropType = 38;

        private const int DwmwcpRound = 2;
        private const int DwmsbtTransientWindow = 3; // acrylic

        [System.Runtime.InteropServices.DllImport("dwmapi.dll", SetLastError = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            ApplyAcrylicBackdrop();
        }

        /// <summary>
        /// Asks DWM for the transient-window acrylic backdrop and Windows 11 rounded corners. The
        /// calls are harmless on builds that do not support them - they return an error and the solid
        /// ToastFallbackBrush that XAML already applied stays. The backdrop needs build 22621+ and the
        /// corner preference 22000+.
        /// </summary>
        private void ApplyAcrylicBackdrop()
        {
            HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
            if (source == null)
            {
                return;
            }

            IntPtr hwnd = source.Handle;

            int dark = 1;
            DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));

            int corner = DwmwcpRound;
            DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref corner, sizeof(int));

            int backdrop = DwmsbtTransientWindow;
            if (DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref backdrop, sizeof(int)) != 0)
            {
                return;
            }

            // DWM now paints the surface, so the WPF layers above it must not.
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
            Root.Background = Brushes.Transparent;
        }

        // ---- Placement, intro, dismissal --------------------------------------

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Open.Add(this);
            Reposition();
            PlayIntro();

            if (Duration > TimeSpan.Zero)
            {
                _dismissTimer = new DispatcherTimer { Interval = Duration };
                _dismissTimer.Tick += delegate { DismissAsync(); };
                _dismissTimer.Start();
            }
        }

        private void OnClosed(object sender, EventArgs e)
        {
            if (_dismissTimer != null)
            {
                _dismissTimer.Stop();
                _dismissTimer = null;
            }

            Open.Remove(this);

            foreach (ToastNotification toast in Open)
            {
                toast.Reposition();
            }
        }

        /// <summary>Bottom right of the work area, newest at the bottom, older ones stacked above.</summary>
        private void Reposition()
        {
            Rect work = SystemParameters.WorkArea;
            int index = Open.IndexOf(this);
            double offset = 0d;

            for (int i = Open.Count - 1; i > index; i--)
            {
                offset += Open[i].ActualHeight + StackGap;
            }

            Left = work.Right - Width - ScreenGap;
            Top = work.Bottom - ActualHeight - ScreenGap - offset;
        }

        private void PlayIntro()
        {
            SlideTransform.X = 48;
            Opacity = 0;

            CubicEase ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            SlideTransform.BeginAnimation(
                TranslateTransform.XProperty,
                new DoubleAnimation(48, 0, TimeSpan.FromMilliseconds(320)) { EasingFunction = ease });
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        }

        /// <summary>Fades out, then closes.</summary>
        public void DismissAsync()
        {
            if (_closing)
            {
                return;
            }

            _closing = true;
            if (_dismissTimer != null)
            {
                _dismissTimer.Stop();
            }

            DoubleAnimation fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(160));
            fade.Completed += delegate { Close(); };
            BeginAnimation(OpacityProperty, fade);
        }

        private void OnActionClick(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            ToastAction action = button != null ? button.Tag as ToastAction : null;
            if (action == null)
            {
                return;
            }

            EventHandler<ToastAction> invoked = ActionInvoked;
            if (invoked != null)
            {
                invoked(this, action);
            }

            if (action.Invoke != null)
            {
                action.Invoke(this);
            }

            if (!action.KeepOpen)
            {
                DismissAsync();
            }
        }
    }
}
