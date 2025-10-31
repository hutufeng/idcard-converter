using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace IDCardOCR.WPF
{
    public class FieldStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is FieldStatus status)
            {
                return status switch
                {
                    FieldStatus.Success => new SolidColorBrush(Colors.Transparent),
                    FieldStatus.LowQuality => new SolidColorBrush(Colors.Red),
                    FieldStatus.NotFound => new SolidColorBrush(Colors.Transparent),
                    FieldStatus.Failed => new SolidColorBrush(Colors.Red),
                    _ => new SolidColorBrush(Colors.Transparent),
                };
            }
            return new SolidColorBrush(Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}