using Microsoft.Win32;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace VSRepoGUI
{
    /// <summary>
    /// MainWindow.xaml
    /// </summary>
    ///
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public string vspackages_file; // = @"..\vspackages.json";
        public VsPlugins Plugins { get; set; }
        public VsApi vsrepo;
        public bool IsNotWorking { get; set; } = true;
        public event PropertyChangedEventHandler PropertyChanged;
        public bool HideInstalled { get; set; }
        public string consolestd { get; set; }
        public List<string> consolestdL = new List<string>();

        RegistryKey localKey;

        public string version = "v0.6a";
        public bool IsVsrepo { get; set; } = true; // else AVSRepo for Avisynth
        public string AppTitle { get; set; }

        private bool _win64;
        public bool Win64
        {
            get { return _win64; }
            set { _win64 = value; vsrepo.SetArch(_win64); }
        }

        public class VsPlugins : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;

            public Package[] UpdateAvailable { get; set; }
            public Package[] Installed { get; set; }
            public Package[] NotInstalled { get; set; }
            public Package[] Unknown { get; set; }

            [AlsoNotifyFor("All")]
            public Package[] Full { get; set; }

            // Quick and dirty props for Tab Header (stringFormat doesn't work for Header)
            public string TabInstalled { get; set; }
            public string TabNotInstalled { get; set; }
            public string TabUpdateAvailable { get; set; }
            public string TabInstalledUnknown { get; set; }
            public string TabAll { get; set; }

            private Package[] _all;
            public Package[] All
            {
                get { return _all; }
                set
                {
                    UpdateAvailable = Array.FindAll(value, c => c.Status == VsApi.PluginStatus.UpdateAvailable);
                    Installed =       Array.FindAll(value, c => c.Status == VsApi.PluginStatus.Installed);
                    NotInstalled =    Array.FindAll(value, c => c.Status == VsApi.PluginStatus.NotInstalled);
                    Unknown =         Array.FindAll(value, c => c.Status == VsApi.PluginStatus.InstalledUnknown);
                    //Full =            value;
                    _all = value;

                    TabUpdateAvailable =  string.Format("Updates ({0})", UpdateAvailable.Count());
                    TabInstalled =        string.Format("Installed ({0})", Installed.Count());
                    TabNotInstalled =     string.Format("Not Installed ({0})", NotInstalled.Count());
                    TabInstalledUnknown = string.Format("Unknown Version ({0})", Unknown.Count());
                    TabAll =              string.Format("Full List ({0})", _all.Count());
                }
            }
        }

        public MainWindow()
        {
            vsrepo = new VsApi(IsVsrepo);
            Plugins = new VsPlugins();
            

            //High dpi 288 fix so it won't cut off the title bar on start
            if (Height > SystemParameters.WorkArea.Height)
            {
                Height = SystemParameters.WorkArea.Height;
                Top = 2;
            }
            InitializeComponent();

            //TabablzControl doesn't support hiding or collapsing Tabitems.
            if (!IsVsrepo)
                TabablzControl.Items.RemoveAt(TabablzControl.Items.Count - 1);

            if (IsVsrepo)
                AppTitle = "VSRepoGUI - A simple plugin manager for VapourSynth | " + version;
            else
                AppTitle = "AVSRepoGUI - A simple plugin manager for AviSynth | " + version;
                
            AddChatter(vsrepo);
            if (IsVsrepo)
                Init();
            else
                InitAvs();
            //var wizardDialog = new SettingsWindow().ShowDialog();
        }

        private void InitAvs()
        {
            ImageHeader.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/avsrepo_logo.png"));
            Link_avsdoom9.Visibility = Visibility.Visible;
            Link_vsdoom9.Visibility = Visibility.Collapsed;

            var settings = new Settings().LoadLocalFile("avsrepogui.json");
            var vsrepo_file = "avsrepo.exe";
            if (File.Exists(vsrepo_file))
            {
                vsrepo.python_bin = vsrepo_file;
            }
            else
            {
                MessageBox.Show("Can't find avsrepo.exe");
                System.Environment.Exit(1);
            }

            if (settings is null)
            {
                AppIsWorking(true);
                vsrepo.SetArch(Environment.Is64BitOperatingSystem);
                vspackages_file = vsrepo.GetPaths(Environment.Is64BitOperatingSystem).Definitions;
                Win64 = Environment.Is64BitOperatingSystem;
            }
            else // Portable mode, valid vsrepogui.json found
            {
                LabelPortable.Visibility = Visibility.Visible;
                vsrepo.SetPortableMode(true);
                vsrepo.python_bin = settings.Bin;
                vspackages_file = Path.GetDirectoryName(settings.Bin) + "\\avspackages.json";

                // Set paths manually and DONT trigger Win64 onPropertyChanged yet
                vsrepo.SetPaths(true, new Paths() { Binaries = settings.Win64.Binaries, Scripts = settings.Win64.Scripts, Definitions = vspackages_file });
                vsrepo.SetPaths(false, new Paths() { Binaries = settings.Win32.Binaries, Scripts = settings.Win32.Scripts, Definitions = vspackages_file });

                // Triggering  Win64 is now safe
                Win64 = Environment.Is64BitOperatingSystem;
            }

            try
            {
                Plugins.All = LoadLocalVspackage();
                //Check OnStart online for new definitions.
                DateTime dt = File.GetLastWriteTime(vspackages_file);
                if (dt < dt.AddDays(1))
                {
                    vsrepo.Update();
                }
            }
            catch
            {
                MessageBox.Show("Could not read (or download) avspackages.json.");
                System.Environment.Exit(1);
            }

            var plugins_installed = vsrepo.GetInstalled();

            // Set Plugin status (installed, not installed, update available etc.)
            foreach (var plugin in plugins_installed)
            {
                var index = Array.FindIndex(Plugins.All, row => row.Identifier == plugin.Key);
                if (index >= 0) //-1 if not found
                {
                    Plugins.All[index].Status = plugin.Value.Value;
                    Plugins.All[index].Releases[0].VersionLocal = plugin.Value.Key;
                }
            }
            FilterPlugins(Plugins.Full);
            AppIsWorking(false);
        }


        private void Init()
        {
            var settings = new Settings().LoadLocalFile();

            var args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                vsrepo.python_bin = args[1];
            }


            if (Environment.Is64BitOperatingSystem)
                localKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            else
                localKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);


            //check if python works
            if (!PythonIsAvailable())
            {
                MessageBox.Show(@"It seems that Python is not installed or not set in your PATH variable. Add Python to PATH or call like this 'VSRepoGui.exe path\to\python.exe'");
                System.Environment.Exit(1);
            }
            if(settings is null)
            {
                string reg_value = null;
                try
                {
                    reg_value = (string)localKey.OpenSubKey("SOFTWARE\\VapourSynth").GetValue("Path");
                }
                catch
                {
                    Console.WriteLine("Failed to read reg key");
                    // MessageBox.Show("Can not find your VapourSynth installation. You can create a vsrepogui.json file for portable mode.");
                    MessageBox.Show("Can not find your VapourSynth installation.");
                    System.Environment.Exit(1);
                }

                if (String.IsNullOrEmpty(reg_value))
                {
                    MessageBox.Show(@"Path entry is empty or null in HKEY_LOCAL_MACHINE\SOFTWARE\VapourSynth. Your VS installation is broken or incomplete.");
                    System.Environment.Exit(1);
                }

                var vsrepo_file = reg_value + "\\vsrepo\\vsrepo.py";


                Console.WriteLine("vsrepo_file: " + vsrepo_file);

                if (File.Exists(vsrepo_file))
                {
                    vsrepo.SetVsrepoPath(vsrepo_file);
                }
                else
                {
                    MessageBox.Show("Found VS installation in " + reg_value + " but no vsrepo.py file in " + vsrepo_file);
                    System.Environment.Exit(1);
                }
                AppIsWorking(true);
                vsrepo.SetArch(Environment.Is64BitOperatingSystem);
                vspackages_file = vsrepo.GetPaths(Environment.Is64BitOperatingSystem).Definitions;
                Win64 = Environment.Is64BitOperatingSystem;
                Console.WriteLine("vspackages_file: " + vsrepo_file);
            }
            else // Portable mode, valid vsrepogui.json found
            {
                LabelPortable.Visibility = Visibility.Visible;
                vsrepo.SetPortableMode(true);
                vsrepo.SetVsrepoPath(settings.Bin);
                vspackages_file = Path.GetDirectoryName(settings.Bin) + "\\vspackages.json";

                // Set paths manually and DONT trigger Win64 onPropertyChanged yet
                vsrepo.SetPaths(true, new Paths()  { Binaries = settings.Win64.Binaries, Scripts = settings.Win64.Scripts, Definitions = vspackages_file });
                vsrepo.SetPaths(false, new Paths() { Binaries = settings.Win32.Binaries, Scripts = settings.Win32.Scripts, Definitions = vspackages_file });

                // Triggering  Win64 is now safe
                Win64 = Environment.Is64BitOperatingSystem;
            }

            
            try
            {
                Plugins.All = LoadLocalVspackage();
                //Check OnStart online for new definitions.
                DateTime dt = File.GetLastWriteTime(vspackages_file);
                if (dt < dt.AddDays(1))
                {
                    vsrepo.Update();
                }
            }
            catch
            {
                MessageBox.Show("Could not read (or download) vspackages.json.");
                System.Environment.Exit(1);
            }

            var plugins_installed = vsrepo.GetInstalled();

            // Set Plugin status (installed, not installed, update available etc.)
            foreach (var plugin in plugins_installed)
            {
                var index = Array.FindIndex(Plugins.All, row => row.Identifier == plugin.Key);
                if (index >= 0) //-1 if not found
                {
                    Plugins.All[index].Status = plugin.Value.Value;
                    Plugins.All[index].Releases[0].VersionLocal = plugin.Value.Key;
                }
            }
            FilterPlugins(Plugins.Full);
            AppIsWorking(false);
        }

        private bool PythonIsAvailable()
        {

            //In PythonPath we believe
            string reg_value = null;
            try
            {
                reg_value = (string)localKey.OpenSubKey("SOFTWARE\\VapourSynth").GetValue("PythonPath");
            }
            catch
            {
                Console.WriteLine("Failed to read reg key");
            }
            if (!String.IsNullOrEmpty(reg_value))
            {
                vsrepo.python_bin = reg_value + @"\python.exe";
                return true;
            }

            try
            {
                Process p = new Process();
                p.StartInfo.FileName = vsrepo.python_bin;
                p.StartInfo.Arguments = "-V";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.Start();
                p.WaitForExit();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void AddChatter(VsApi chatter)
        {
            //vsrepo.Add(chatter);
            chatter.PropertyChanged += chatter_PropertyChanged;
        }

        private void chatter_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Console.WriteLine("A property has changed: " + e.PropertyName);
            var s = sender as VsApi;
            Console.WriteLine("||| " + s.consolestd);
            consolestd += s.consolestd + "\n";
            //consolestd.Add(s.consolestd);
            //ConsoleBox.Text = String.Join(Environment.NewLine, consolestd);
            //ConsoleBox.Text =  s.consolestd + "\n";
        }

        private Package[] LoadLocalVspackage()
        {
            if (!File.Exists(vspackages_file))
            {
                vsrepo.Update();
            }

            //Load vspackages.json
            var jsonString = File.ReadAllText(vspackages_file);
            var packages = Vspackage.FromJson(jsonString);
            //var packages = JsonConvert.DeserializeObject<Vspackage>(jsonString);
            //return (packages.FileFormat, packages.Packages);
            return packages.Packages;
        }

        public void AppIsWorking(bool status)
        {
            Progressbar.IsIndeterminate = status;
            IsNotWorking = !status;
        }


        public void FilterPlugins(Package[] plugins)
        {
            if(plugins != null)
            {
                string search = searchBox.Text;
                if (search.Length > 0 && search != "Search")
                {
                    plugins = Array.FindAll(plugins, c => c.Name.ToLower().Contains(search) || (c.Namespace?.ToLower().Contains(search) ?? c.Modulename.ToLower().Contains(search)));
                }
                if (HideInstalled)
                {
                    plugins = Array.FindAll(plugins, c => c.Status != VsApi.PluginStatus.Installed);
                }

                if(search.Length == 0)
                {
                    Plugins.All = Plugins.Full;
                } else
                {
                    Plugins.All = plugins;
                }                
                dataGrid.Columns[0].SortDirection = ListSortDirection.Ascending;
                dataGridAvailable.Columns[0].SortDirection = ListSortDirection.Ascending;
                dataGridUnknown.Columns[0].SortDirection = ListSortDirection.Ascending;
                dataGridNotInstalled.Columns[0].SortDirection = ListSortDirection.Ascending;
                dataGridAll.Columns[0].SortDirection = ListSortDirection.Ascending;
            }
        }

        private async Task ReloadPluginsAsync()
        {
            var _plugins = LoadLocalVspackage();
            if(Win64)
                _plugins = Array.FindAll(_plugins, c => c.Releases[0].Win64 != null || c.Releases[0].Script != null);
            else
                _plugins = Array.FindAll(_plugins, c => c.Releases[0].Win32 != null || c.Releases[0].Script != null);

            var plugins_installed = await vsrepo.GetInstalledAsync();

            // Set Plugin status (installed, not installed, update available etc.)
            foreach (var plugin in plugins_installed)
            {
                var index = Array.FindIndex(_plugins, row => row.Identifier == plugin.Key);
                if (index >= 0) //-1 if not found
                {
                    _plugins[index].Status = plugin.Value.Value;
                    _plugins[index].Releases[0].VersionLocal = plugin.Value.Key;
                }
            }
            Plugins.Full = _plugins;
            Plugins.All = _plugins;
            FilterPlugins(Plugins.Full);
        }

        private async void Button_upgrade_all(object sender, RoutedEventArgs e)
        {
            AppIsWorking(true);
            await vsrepo.UpgradeAll();
            await ReloadPluginsAsync();
            AppIsWorking(false);
        }


        private async void Button_Install(object sender, RoutedEventArgs e)
        {
            AppIsWorking(true);
            var button = sender as Button;

            var plugin_status = ((Package)button.DataContext).Status;
            string plugin = ((Package)button.DataContext).Namespace ?? ((Package)button.DataContext).Modulename;
            consolestd = "";
            consolestdL.Clear();
            switch (plugin_status)
            {
                case VsApi.PluginStatus.Installed:
                    if (MessageBox.Show("Uninstall " + ((Package)button.DataContext).Name + "?", "Uninstall?", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        await vsrepo.Uninstall(plugin);
                    }
                    break;
                case VsApi.PluginStatus.InstalledUnknown:
                    if (MessageBox.Show("Your local file (with unknown version) has the same name as " + ((Package)button.DataContext).Name + " and will be overwritten, proceed?", "Force Upgrade?", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        await vsrepo.Upgrade(plugin, force: true);
                    }

                    break;
                case VsApi.PluginStatus.NotInstalled:
                    await vsrepo.Install(plugin);
                    break;
                case VsApi.PluginStatus.UpdateAvailable:
                    await vsrepo.Upgrade(plugin);
                    break;
            }
          
            ConsoleBox.Focus();
            ConsoleBox.CaretIndex = ConsoleBox.Text.Length;
            ConsoleBox.ScrollToEnd();

            await ReloadPluginsAsync();
            AppIsWorking(false);
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterPlugins(Plugins.Full);
        }

        private void HideInstalled_Checked(object sender, RoutedEventArgs e)
        {
            FilterPlugins(Plugins.Full);
        }

        private void HideInstalled_Unchecked(object sender, RoutedEventArgs e)
        {
            FilterPlugins(Plugins.Full);
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var textbox = sender as TextBox;
            textbox.Clear();
        }

        private async void CheckBox_Win64_Checked(object sender, RoutedEventArgs e)
        {
            AppIsWorking(true);
            Win64 = true;
            await ReloadPluginsAsync();
            AppIsWorking(false);
        }

        private async void CheckBox_Win64_Unchecked(object sender, RoutedEventArgs e)
        {
            AppIsWorking(true);
            Win64 = false;
            await ReloadPluginsAsync();
            AppIsWorking(false);
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Hello friend");
        }
        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Uri.AbsoluteUri);
        }

        private void Hyperlink_Click_1(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Definitions: " + vsrepo.GetPaths(Win64).Definitions + "\nScripts: " + vsrepo.GetPaths(Win64).Scripts + "\nBinaries: " + vsrepo.GetPaths(Win64).Binaries);
        }

        private void Hyperlink_open(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Uri.AbsoluteUri);
        }

        private void Hyperlink_Click_Plugins(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", vsrepo.GetPaths(Win64).Binaries);
        }

        private void Hyperlink_Click_Scripts(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", vsrepo.GetPaths(Win64).Scripts);
        }

        private void Hyperlink_Click_about(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Version " + version);
        }

        private void DiagPrintHelper(Dictionary<string, List<string>> plugins, string id, string errmsg)
        {
            if (plugins[id].Count() > 0)
            {
                TextBlock_Diagnose.Text += errmsg;
                TextBlock_Diagnose.Text += "------------------------------------------------------------\n";
                foreach (var p in plugins[id])
                {
                    TextBlock_Diagnose.Text += "   " + p + "\n";
                }
            }
        }
        private async void TabablzControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DiagnoseTab.IsSelected)
            {
                AppIsWorking(true);
                var diag = new Diagnose(vsrepo.python_bin);
                Dictionary<string, List<string>> plugins = await diag.CheckPluginsAsync(vsrepo.GetPaths(Win64).Binaries);
                var version = await diag.GetVsVersion();

                if(plugins != null)
                {
                    var osname = (from x in new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem").Get().Cast<ManagementObject>()
                               select x.GetPropertyValue("Caption")).FirstOrDefault();

                    ManagementObjectSearcher mos = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Processor");
                    string cpu = "";
                    foreach (ManagementObject mo in mos.Get())
                    {
                        cpu = mo["Name"].ToString();
                    }

                    TextBlock_Diagnose.Text = @"/!\ Only Plugins and no Scripts are tested /!\";
                    TextBlock_Diagnose.Text += "\n" + version;
                    TextBlock_Diagnose.Text += "\nOS: " + osname != null ? osname.ToString() : "Unknown";
                    TextBlock_Diagnose.Text += "\nIs 64Bit OS?: " + System.Environment.Is64BitOperatingSystem;
                    TextBlock_Diagnose.Text += "\nCPU: " + cpu;
                    TextBlock_Diagnose.Text += "\nCPU Cores: " + System.Environment.ProcessorCount;

                    TextBlock_Diagnose.Text += "\n\n============================================================\n";

                    TextBlock_Diagnose.Text += string.Format("\nChecked Plugins: {0}, Notices: {1}, Errors: {2}\n\n",
                            plugins["no_problems"].Count() + plugins["not_a_vsplugin"].Count() + plugins["missing_dependency"].Count() + plugins["wrong_arch"].Count() + plugins["others"].Count(),
                            plugins["not_a_vsplugin"].Count(),
                            plugins["wrong_arch"].Count() + plugins["missing_dependency"].Count() + plugins["others"].Count());

                    TextBlock_Diagnose.Text += string.Format("Plugin Path: {0}\n", vsrepo.GetPaths(Win64).Binaries);

                    DiagPrintHelper(plugins, "wrong_arch", "\n🔥 Error 193 - You propably mixed 32/64 bit plugins: \n");
                    DiagPrintHelper(plugins, "missing_dependency", "\n🔥 Error 126 - A DLL dependency is probably missing: \n");
                    DiagPrintHelper(plugins, "namespace", "\n🔥 Namespace already populated, therefore it failed to load: \n");
                    DiagPrintHelper(plugins, "others", "\n🔥 Error unknown: \n");
                    DiagPrintHelper(plugins, "not_a_vsplugin", "\n😑 Notice - Not a VapourSynth Plugin: \n");
                    DiagPrintHelper(plugins, "no_problems", "\n👍 Successfully loaded Plugins: \n");

                } else
                {
                    TextBlock_Diagnose.Text = "Some error occured";
                }

                AppIsWorking(false);
            }
            
        }

    }


}
