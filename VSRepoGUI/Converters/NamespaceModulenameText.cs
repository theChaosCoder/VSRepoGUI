using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VSRepoGUI.Converters
{
    public class NamespaceModulenameText : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ((Package)value).Namespace ?? ((Package)value).Modulename;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}