using System;
using System.Windows.Data;
using System.Windows.Media;

namespace VSRepoGUI.Converters
{

    public class ButtonStatusConverter : IValueConverter
    {
        //public static Style style = "MaterialDesignRaisedButton";

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if ((VsApi.PluginStatus)value == VsApi.PluginStatus.Installed)
            {
                return new SolidColorBrush(Colors.OrangeRed);
            }
            if ((VsApi.PluginStatus)value == VsApi.PluginStatus.InstalledUnknown)
            {
                return new SolidColorBrush(Colors.Red);
            }
            if ((VsApi.PluginStatus)value == VsApi.PluginStatus.NotInstalled)
            {
                return new SolidColorBrush(Colors.Green);
            }
            return new SolidColorBrush(Colors.LimeGreen);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    } 
}
