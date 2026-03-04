using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CalledAssistant.Converters
{
    /// <summary>
    /// bool 取反后转 Visibility：false → Visible，true → Collapsed
    /// </summary>
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
