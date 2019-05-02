using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace VSRepoGUI
{

    public partial class SettingsWindow : Window
    {
        RegistryKey localKey;

        public SettingsWindow()
        {
            InitializeComponent();
            Init();
        }

        private void Init()
        {
            if (Environment.Is64BitOperatingSystem)
                localKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            else
                localKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);


            //object reg_value = null;
            try
            {
                var reg_value = localKey.OpenSubKey("SOFTWARE\\Avisynth").GetValueNames();
                
            }
            catch
            {
                Console.WriteLine("Failed to read reg key");

                System.Environment.Exit(1);
            }
        }
    }
}
