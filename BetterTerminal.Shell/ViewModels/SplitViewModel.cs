using System.Windows;

namespace BetterTerminal.Shell.ViewModels
{
    /// <summary>
    /// A branch in the pane tree. Lengths are two-way so a splitter drag persists into the
    /// saved workspace. The concrete subclasses exist so the implicit DataTemplates in
    /// MainWindow.xaml can dispatch on type: WPF cannot switch a template on a property.
    /// </summary>
    public abstract class SplitViewModel : ObservableObject
    {
        private object _first;
        private object _second;
        private GridLength _firstLength = new GridLength(1, GridUnitType.Star);
        private GridLength _secondLength = new GridLength(1, GridUnitType.Star);

        public object First
        {
            get { return _first; }
            set { Set(ref _first, value); }
        }

        public object Second
        {
            get { return _second; }
            set { Set(ref _second, value); }
        }

        public GridLength FirstLength
        {
            get { return _firstLength; }
            set { Set(ref _firstLength, value); }
        }

        public GridLength SecondLength
        {
            get { return _secondLength; }
            set { Set(ref _secondLength, value); }
        }

        public double FirstRatio
        {
            get
            {
                double total = _firstLength.Value + _secondLength.Value;
                return total <= 0 ? 0.5 : _firstLength.Value / total;
            }

            set
            {
                double ratio = value <= 0 || value >= 1 ? 0.5 : value;
                FirstLength = new GridLength(ratio, GridUnitType.Star);
                SecondLength = new GridLength(1 - ratio, GridUnitType.Star);
            }
        }

        public object Other(object child)
        {
            return ReferenceEquals(child, First) ? Second : First;
        }

        public void Replace(object oldChild, object newChild)
        {
            if (ReferenceEquals(oldChild, First))
            {
                First = newChild;
            }
            else if (ReferenceEquals(oldChild, Second))
            {
                Second = newChild;
            }
        }
    }
}
