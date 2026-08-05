using System;
using System.Windows;
using System.Windows.Media.Animation;
using BetterTerminal.Shell.Services;
using BetterTerminal.Shell.ViewModels;

namespace BetterTerminal.Shell.Views
{
    public partial class SplashWindow : Window
    {
        private Storyboard _blink;
        private Storyboard _sweep;

        public SplashWindow()
        {
            InitializeComponent();
            VersionLine.Text = AboutViewModel.AssemblyVersion() + "  net48";
            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        public void Report(string status)
        {
            Status.Text = status;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Reduced motion: the splash still shows and still reports progress, it just
            // does not animate (MO-08). Nothing about the state itself is skipped.
            if (!MotionPolicy.Enabled)
            {
                return;
            }

            _blink = BuildBlink();
            _blink.Begin(this, HandoffBehavior.SnapshotAndReplace, true);

            _sweep = BuildSweep();
            _sweep.Begin(this, HandoffBehavior.SnapshotAndReplace, true);
        }

        private Storyboard BuildBlink()
        {
            DoubleAnimationUsingKeyFrames animation = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromSeconds(1.06)),
                RepeatBehavior = RepeatBehavior.Forever
            };
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromPercent(0)));
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromPercent(0.5)));

            Storyboard.SetTarget(animation, CaretBlock);
            Storyboard.SetTargetProperty(animation, new PropertyPath("Opacity"));

            Storyboard storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            return storyboard;
        }

        private Storyboard BuildSweep()
        {
            // Linear, ~1.2 s period - the indeterminate-progress convention (MO-07).
            DoubleAnimation animation = new DoubleAnimation
            {
                From = -170,
                To = ActualWidth,
                Duration = new Duration(TimeSpan.FromSeconds(1.2)),
                RepeatBehavior = RepeatBehavior.Forever
            };

            Storyboard.SetTarget(animation, SweepShift);
            Storyboard.SetTargetProperty(animation, new PropertyPath("X"));

            Storyboard storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            return storyboard;
        }

        private void OnClosed(object sender, EventArgs e)
        {
            if (_blink != null)
            {
                _blink.Stop(this);
            }

            if (_sweep != null)
            {
                _sweep.Stop(this);
            }
        }
    }
}
