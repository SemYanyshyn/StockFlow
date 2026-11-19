using System.Globalization;
using System.Windows.Data;

namespace StockFlow.UI.Converters
{
    public class ActiveSectionConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is not null && parameter is Type section && value.GetType() == section;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
