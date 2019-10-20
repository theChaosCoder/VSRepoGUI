using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace VSRepoGUI
{

    public partial class SettingsWindow : Window, INotifyPropertyChanged
    {
        public AvsRegistryHelper avsregistry = new AvsRegistryHelper();
        public List<string> Avs32Plugins = new List<string>();
        public List<string> Avs64Plugins = new List<string>();

        public PluginPaths Path32 = new PluginPaths();
        public PluginPaths Path64 = new PluginPaths();

        public bool IsCustomPluginPath32 { get; set; } = false;
        public bool IsCustomPluginPath64 { get; set; } = false;
        public bool IsCustomScriptPath32 { get; set; } = false;
        public bool IsCustomScriptPath64 { get; set; } = false;
        public event PropertyChangedEventHandler PropertyChanged;

        public SettingsWindow()
        {
            InitializeComponent();
            InitAvs();
            
        }

        private void InitAvs()
        {
            avsregistry.ReadAllAvsRegistryInfos();

            if (avsregistry.Registry.ContainsKey("avs32"))
            {
                Avs32Plugins.Add(avsregistry.Registry["avs32"].PluginDir2_5);
                Avs32Plugins.Add(avsregistry.Registry["avs32"].PluginDirplus);
                comboBoxavs32.ItemsSource = comboBoxavs32_script.ItemsSource = Avs32Plugins;
                comboBoxavs32.SelectedIndex = comboBoxavs32_script.SelectedIndex = 0;
            } else
            {
                NotInstalled32.Visibility = Visibility.Visible;
            }

            if (avsregistry.Registry.ContainsKey("avs64"))
            {
                Avs64Plugins.Add(avsregistry.Registry["avs64"].PluginDir2_5);
                Avs64Plugins.Add(avsregistry.Registry["avs64"].PluginDirplus);
                comboBoxavs64.ItemsSource = comboBoxavs64_script.ItemsSource = Avs64Plugins;
                comboBoxavs64.SelectedIndex = comboBoxavs64_script.SelectedIndex = 0;
            } else
            {
                NotInstalled64.Visibility = Visibility.Visible;
            }

        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //32 bit
            if (comboBoxavs32.Items.Count > 0)
                Path32.Plugin = comboBoxavs32.SelectedItem.ToString();
            if (IsCustomPluginPath32)
                Path32.Plugin = textbox32.Text;

            if (comboBoxavs32_script.Items.Count > 0)
                Path32.Script = comboBoxavs32_script.SelectedItem.ToString();
            if (IsCustomScriptPath32)
                Path32.Script = textbox32_script.Text;

            //64 bit
            if (comboBoxavs64.Items.Count > 0)
                Path64.Plugin = comboBoxavs64.SelectedItem.ToString();
            if (IsCustomPluginPath64)
                Path64.Plugin = textbox64.Text;

            if (comboBoxavs64_script.Items.Count > 0)
                Path64.Script = comboBoxavs64_script.SelectedItem.ToString();
            if (IsCustomScriptPath64)
                Path64.Script = textbox64_script.Text;
        }

        public string FolderDialog()
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                if(dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    return dialog.SelectedPath;
                }
            }
            return "";
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var path = FolderDialog();
            var button = sender as System.Windows.Controls.Button;
            switch (button.Tag)
            {
                case "avs32": textbox32.Text = path; break;
                case "avs32script": textbox32_script.Text = path; break;
                case "avs64": textbox64.Text = path; break;
                case "avs64script": textbox64_script.Text = path; break;
            }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(e.Uri.AbsoluteUri);
        }

        public class PluginPaths
        {
            public string Plugin;
            public string Script;
        }
    }
}
