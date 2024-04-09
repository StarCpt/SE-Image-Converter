using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace ImageConverterPlus.Converters
{
    public class InequalityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool equals = Equals(value, parameter);
            if (targetType == typeof(Visibility))
            {
                return !equals ? Visibility.Visible : Visibility.Collapsed;
            }
            return !equals;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
