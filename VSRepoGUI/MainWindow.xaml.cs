using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace VSRepoGUI
{
    /// <summary>
    /// MainWindow.xaml
    /// </summary>
    ///
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public string vspackages_file; // = @"..\vspackages.json";
        //public Package[] Plugins { get; set; }
        public Package[] PluginsFull { get; set; }
        public Package[] PluginsInstalled { get; set; }
        public Package[] PluginsNotInstalled { get; set; }
        public Package[] PluginsUpdateAvailable { get; set; }
        public Package[] PluginsUnknown { get; set; }

        //public VsPlugins Plugins = new VsPlugins();
        public Paths paths;
        public VsApi vsrepo = new VsApi();
        public bool IsNotWorking { get; set; } = true;
        public event PropertyChangedEventHandler PropertyChanged;
        public bool HideInstalled { get; set; }
        public string consolestd { get; set; }
        public List<string> consolestdL = new List<string>();

        RegistryKey localKey;

        public string version = "v0.3a";
        public bool IsVsrepo = true; // else AVSRepo for Avisynth
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
            public Package[] Unknown { get; set; }

            private Package[] _all;
            public Package[] All
            {
                get { return _all; }
                set
                {
                    _all = value;
                    UpdateAvailable = Array.FindAll(_all, c => c.Status == VsApi.PluginStatus.UpdateAvailable);
                    Installed = Array.FindAll(_all, c => c.Status == VsApi.PluginStatus.Installed);
                    Unknown = Array.FindAll(_all, c => c.Status == VsApi.PluginStatus.InstalledUnknown);
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            //High dpi 288 fix so it won't cut off the title bar on start
            if (Height > SystemParameters.WorkArea.Height)
            {
                Height = SystemParameters.WorkArea.Height;
                Top = 2;
            }

            DataContext = this;

            if (IsVsrepo)
                AppTitle = "VSRepoGUI - A simple plugin manager for VapourSynth | " + version;
            else
            {
                AppTitle = "AVSRepoGUI - A simple plugin manager for AviSynth | " + version;
                vsrepo.SetPortableMode(true);
            }
                
            AddChatter(vsrepo);
            Init();
            //var wizardDialog = new SettingsWindow().ShowDialog();
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
                paths = vsrepo.GetPaths(Environment.Is64BitOperatingSystem);
                vspackages_file = paths.Definitions;
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
                paths = vsrepo.GetPaths(Win64);
            }
            

            try
            {
                PluginsFull = LoadLocalVspackage();
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
            //AppIsWorking(false);

            /* var a = new Dictionary<string, string>();
             a.Add("Binaries", paths.Binaries);
             a.Add("Scripts", paths.Scripts);
             string joutput = JsonConvert.SerializeObject(a);
             System.IO.File.WriteAllText("vsrepogui.json", joutput);*/

            // Set Plugin status (installed, not installed, update available etc.)
            foreach (var plugin in plugins_installed)
            {
                var index = Array.FindIndex(PluginsFull, row => row.Identifier == plugin.Key);
                if (index >= 0) //-1 if not found
                {
                    PluginsFull[index].Status = plugin.Value.Value;
                    PluginsFull[index].Releases[0].VersionLocal = plugin.Value.Key;
                }
            }
            FilterPlugins(PluginsFull);
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
            //await ReloadPluginsAsync();
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
                PluginsInstalled = Array.FindAll(plugins, c => c.Status == VsApi.PluginStatus.Installed);
                PluginsNotInstalled = Array.FindAll(plugins, c => c.Status == VsApi.PluginStatus.NotInstalled);
                PluginsUpdateAvailable = Array.FindAll(plugins, c => c.Status == VsApi.PluginStatus.UpdateAvailable);
                PluginsUnknown = Array.FindAll(plugins, c => c.Status == VsApi.PluginStatus.InstalledUnknown);
                dataGrid.Columns[0].SortDirection = ListSortDirection.Ascending;
            }
            
        }

        private async Task ReloadPluginsAsync()
        {
            var _plugins = LoadLocalVspackage();
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
            PluginsFull = _plugins;
            FilterPlugins(PluginsFull);
        }

        private async void Button_upgrade_all(object sender, RoutedEventArgs e)
        {
            AppIsWorking(true);
            await vsrepo.UpgradeAll();
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
            FilterPlugins(PluginsFull);
        }

        private void HideInstalled_Checked(object sender, RoutedEventArgs e)
        {
            FilterPlugins(PluginsFull);
        }

        private void HideInstalled_Unchecked(object sender, RoutedEventArgs e)
        {
            FilterPlugins(PluginsFull);
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
            paths = vsrepo.GetPaths(Win64);
            await ReloadPluginsAsync();
            AppIsWorking(false);
        }

        private async void CheckBox_Win64_Unchecked(object sender, RoutedEventArgs e)
        {
            AppIsWorking(true);
            Win64 = false;
            paths = vsrepo.GetPaths(Win64);
            await ReloadPluginsAsync();
            AppIsWorking(false);
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Hello friend");
        }
        private void Hyperlink_RequestNavigate(object sender,
                                       System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Uri.AbsoluteUri);
        }

        private void Hyperlink_Click_1(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Definitions: " + paths.Definitions + "\nScripts: " + paths.Scripts + "\nBinaries: " + paths.Binaries);
        }

        private void Hyperlink_open(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Uri.AbsoluteUri);
        }

        private void Hyperlink_Click_Plugins(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", paths.Binaries);
        }

        private void Hyperlink_Click_Scripts(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", paths.Scripts);
        }

        private void Hyperlink_Click_about(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Version " + version);
        }
    }


}
