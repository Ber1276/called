using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CalledAssistant.Converters
{
    /// <summary>
    /// 将字符串转换为 Visibility：非空/非空白 → Visible，否则 Collapsed
    /// </summary>
    [ValueConversion(typeof(string), typeof(Visibility))]
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !string.IsNullOrWhiteSpace(value as string) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
