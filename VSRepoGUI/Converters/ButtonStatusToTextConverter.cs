using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace VSRepoGUI.Converters
{
    public class ButtonStatusToTextConverter : IValueConverter
    {
        //public static Style style = "MaterialDesignRaisedButton";

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {

            if ((VsApi.PluginStatus)value == VsApi.PluginStatus.Installed)
            {
                return "Uninstall";

            }
            if ((VsApi.PluginStatus)value == VsApi.PluginStatus.InstalledUnknown)
            {
                return "Force Upgrade";
            }
            if ((VsApi.PluginStatus)value == VsApi.PluginStatus.NotInstalled)
            {
                return "Install";
            }
            return "Update";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
}
