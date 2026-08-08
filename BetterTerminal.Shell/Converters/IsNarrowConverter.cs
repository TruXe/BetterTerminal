using System;
using System.Globalization;
using System.Windows.Data;

namespace BetterTerminal.Shell.Converters
{
    /// <summary>
    /// True once a width has dropped below the threshold, so a label can step out of the way while
    /// its icon and tooltip carry on saying what the control does. The alternative - letting the
    /// text clip - hides the meaning without admitting it.
    /// </summary>
    public sealed class IsNarrowConverter : IValueConverter
    {
        /// <summary>Below this many device-independent pixels the wide form no longer fits.</summary>
        public double Threshold { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is double))
            {
                return false;
            }

            double width = (double)value;
            double limit = Threshold > 0 ? Threshold : 240;

            // A binding may want its own threshold without a second converter instance.
            double given;
            if (parameter != null && double.TryParse(
                    parameter.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out given)
                && given > 0)
            {
                limit = given;
            }

            // Zero means the element has not been measured yet; treating that as narrow would hide
            // the label on the first frame and flash it back a moment later.
            return width > 0 && width < limit;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("Bt.IsNarrow is a one-way converter.");
        }
    }
}
