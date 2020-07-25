using PropertyChanged;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
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
        public Dictionary<string, string> plugins_dll_parents = new Dictionary<string, string>();
        public VsApi vsrepo;
        public VsRegistryHelper vsregistry = new VsRegistryHelper();
        public string CurrentPluginPath  { get; set; }
        public string CurrentScriptPath  { get; set; }
        public bool IsNotWorking { get; set; } = true;
        public bool PortableMode { get; set; } = false;
        public event PropertyChangedEventHandler PropertyChanged;
        public bool HideInstalled { get; set; }
        public string consolestd { get; set; }
        public List<string> consolestdL = new List<string>();

        public string version = "v0.9.2";
        public string AppTitle { get; set; }
        public bool Win64 { get; set; }
        

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
            vsrepo = new VsApi();
            Plugins = new VsPlugins();
            vsregistry.ReadAllVsRegistryInfos();

            //High dpi 288 fix so it won't cut off the title bar on start
            if (Height > SystemParameters.WorkArea.Height)
            {
                Height = SystemParameters.WorkArea.Height;
                Top = 2;
            }

            
            InitializeComponent();
            
            // init Jot Settings Tracker
            SettingsService.Tracker.Track(this);
            //showedFirstTimeSettingsAvs = false;


            AddChatter(vsrepo);

            AppTitle = "VSRepoGUI - A simple plugin manager for VapourSynth | " + version;
            InitVapoursynth();

            Win64 = Environment.Is64BitOperatingSystem; // triggers checkbox changed event
        }


        private void InitVapoursynth()
        {
            //TabablzControl doesn't support hiding or collapsing Tabitems. Hide "Settings" (last) tab if we are in vapoursynth mode	
            TabablzControl.Items.RemoveAt(TabablzControl.Items.Count - 1);

            var settings = new PortableSettings().LoadLocalFile();

            var args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                vsrepo.python_bin = args[1];
            } else
            {
                //check if python is in PATH first
                if (IsPythonCallable())
                {
                    vsrepo.python_bin = GetPythonLocation("python.exe");
                    _ = vsregistry.SetCurrentVsInstallation(vsrepo.python_bin);
                }
                else //is not in PATH
                {
                    // Get PythonPath from VapourSynth registry and test if it's callable
                    var pybin = GetPythonLocation(vsregistry.GetAvailablePythonPath());
                    
                    if (!String.IsNullOrEmpty(pybin))
                    {
                        vsrepo.python_bin = pybin;
                    } else
                    {
                        MessageBox.Show(@"It seems that Python is not installed or not set in your PATH variable. Add Python to PATH or call like this: 'VSRepoGui.exe path\to\python.exe'");
                        System.Environment.Exit(1);
                    }
                    
                }
            }

            if(settings is null)
            {
                if(vsregistry.Registry.Count() == 0)
                {
                    // MessageBox.Show("Can not find your VapourSynth installation. You can create a vsrepogui.json file for portable mode.");
                    MessageBox.Show("Can not find your VapourSynth installation(s).");
                    System.Environment.Exit(1);
                }
                 
                var vsrepo_file = vsregistry.GetCurrentRegPath() + "\\vsrepo\\vsrepo.py";
                Console.WriteLine("vsrepo_file: " + vsrepo_file);

                if (File.Exists(vsrepo_file))
                {
                    vsrepo.SetVsrepoPath(vsrepo_file);
                }
                else
                {
                    MessageBox.Show("Found VS installation in " + vsregistry.GetCurrentRegPath() + " but no vsrepo.py file in " + vsrepo_file + ". Make sure you have at least version R45 of VapourSynth installed.");
                    System.Environment.Exit(1);
                }
                AppIsWorking(true);
                vsrepo.SetArch(Environment.Is64BitOperatingSystem);
                //Trigger GetPaths for 32/64 bit, they are cached in VsApi class anyway
                _ = vsrepo.GetPaths(true).Definitions; _ = vsrepo.GetPaths(false).Definitions;
                vspackages_file = vsrepo.GetPaths(Environment.Is64BitOperatingSystem).Definitions;
                //Win64 = Environment.Is64BitOperatingSystem;
                Console.WriteLine("vspackages_file: " + vsrepo_file);
            }
            else // Portable mode, valid vsrepogui.json found
            {
                LabelPortable.Visibility = Visibility.Visible;
                PortableMode = true;
                vsrepo.SetPortableMode(PortableMode);
                vsrepo.SetVsrepoPath(settings.Bin);
                vspackages_file = Path.GetDirectoryName(settings.Bin) + "\\vspackages.json";
                
                // Set paths manually and DONT trigger Win64 onPropertyChanged yet
                vsrepo.SetPaths(true, new Paths()  { Binaries = settings.Win64.Binaries, Scripts = settings.Win64.Scripts, Definitions = vspackages_file });
                vsrepo.SetPaths(false, new Paths() { Binaries = settings.Win32.Binaries, Scripts = settings.Win32.Scripts, Definitions = vspackages_file });

                // Triggering  Win64 is now safe
                //Win64 = Environment.Is64BitOperatingSystem;
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
            
        }

         private string GetPythonLocation(string python_bin)
         {
             string cmd = "import sys; print(sys.executable)";
             ProcessStartInfo start = new ProcessStartInfo();
             start.FileName = python_bin;
             start.Arguments = string.Format("-c \"{0}\"", cmd);
             start.UseShellExecute = false;// Do not use OS shell
             start.CreateNoWindow = true; // We don't need new window
             start.RedirectStandardOutput = true;// Any output, generated by application will be redirected back
             start.RedirectStandardError = false; // Any error in standard output will be redirected back (for example exceptions)
             using (Process process = Process.Start(start))
             {
                 using (StreamReader reader = process.StandardOutput)
                 {
                     string result = reader.ReadToEnd();
                     return result.Trim();
                 }
             }
         }

        private bool IsPythonCallable()
        {
            try
            {
                Process p = new Process();
                p.StartInfo.FileName = "python.exe";
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
            FilterPlugins(Plugins.Full);
        }

        private async void Button_upgrade_all(object sender, RoutedEventArgs e)
        {
            AppIsWorking(true);
            await vsrepo.UpgradeAllAsync();
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
            if(HasWriteAccessToFolder(CurrentPluginPath))
            {
                switch (plugin_status)
                {
                    case VsApi.PluginStatus.Installed:
                        if (MessageBox.Show("Uninstall " + ((Package)button.DataContext).Name + "?", "Uninstall?", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            await vsrepo.UninstallAsync(plugin);
                        }
                        break;
                    case VsApi.PluginStatus.InstalledUnknown:
                        if (MessageBox.Show("Your local file (with unknown version) has the same name as " + ((Package)button.DataContext).Name + " and will be overwritten, proceed?", "Force Upgrade?", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            await vsrepo.UpgradeAsync(plugin, force: true);
                        }
                        break;
                    case VsApi.PluginStatus.NotInstalled:
                        await vsrepo.InstallAsync(plugin);
                        break;
                    case VsApi.PluginStatus.UpdateAvailable:
                        await vsrepo.UpgradeAsync(plugin);
                        break;
                }
            
            
          
                ConsoleBox.Focus();
                ConsoleBox.CaretIndex = ConsoleBox.Text.Length;
                ConsoleBox.ScrollToEnd();

                await ReloadPluginsAsync();
            } else
            {
                MessageBox.Show("Can't write to plugins folder. Restart program as admin.");
            }
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

        private void CheckBox_Win64_Checked(object sender, RoutedEventArgs e)
        {
            CheckBoxToggleHelper(sender as CheckBox);
        }

        private void CheckBox_Win64_Unchecked(object sender, RoutedEventArgs e)
        {
            CheckBoxToggleHelper(sender as CheckBox);
        }

        private async void CheckBoxToggleHelper(CheckBox c)
        {
            AppIsWorking(true);
            Win64 = c.IsChecked.Value;
            vsrepo.SetArch(Win64);
            CurrentPluginPath = vsrepo.paths[Win64].Binaries;
            CurrentScriptPath = vsrepo.paths[Win64].Scripts;
            await ReloadPluginsAsync();
            AppIsWorking(false);
        }

        // TODO check https://docs.microsoft.com/de-de/dotnet/api/system.io.filestream.canwrite?view=netframework-4.8 
        public static bool HasWriteAccessToFolder(string FilePath)
        {
            try
            {
                FileSystemSecurity security;
                if (File.Exists(FilePath))
                {
                    security = File.GetAccessControl(FilePath);
                }
                else
                {
                    security = Directory.GetAccessControl(Path.GetDirectoryName(FilePath));
                }
                var rules = security.GetAccessRules(true, true, typeof(NTAccount));

                var currentuser = new WindowsPrincipal(WindowsIdentity.GetCurrent());
                bool result = false;
                foreach (FileSystemAccessRule rule in rules)
                {
                    if (0 == (rule.FileSystemRights &
                        (FileSystemRights.WriteData | FileSystemRights.Write)))
                    {
                        continue;
                    }

                    if (rule.IdentityReference.Value.StartsWith("S-1-"))
                    {
                        var sid = new SecurityIdentifier(rule.IdentityReference.Value);
                        if (!currentuser.IsInRole(sid))
                        {
                            continue;
                        }
                    }
                    else
                    {
                        if (!currentuser.IsInRole(rule.IdentityReference.Value))
                        {
                            continue;
                        }
                    }

                    if (rule.AccessControlType == AccessControlType.Deny)
                        return false;
                    if (rule.AccessControlType == AccessControlType.Allow)
                        result = true;
                }
                return result;
            }
            catch
            {
                return false;
            }
        }


        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Hello friend");
        }
        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(e.Uri.AbsoluteUri);
        }

        private void Hyperlink_Click_1(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Definitions: " + vsrepo.GetPaths(Win64).Definitions + "\nScripts: " + vsrepo.GetPaths(Win64).Scripts + "\nBinaries: " + vsrepo.GetPaths(Win64).Binaries);
        }

        private void Hyperlink_open(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(e.Uri.AbsoluteUri);
        }

        private void Hyperlink_namespace(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            var package = (Package)(sender as Hyperlink).DataContext;
            Process.Start("https://github.com/vapoursynth/vsrepo/tree/master/local/" + (package.Namespace ?? package.Modulename) + ".json");

        }

        private void Hyperlink_Click_Plugins(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", vsrepo.GetPaths(Win64).Binaries);
        }

        private void Hyperlink_Click_Scripts(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", vsrepo.GetPaths(Win64).Scripts);
        }

        private void Hyperlink_Explorer(object sender, RoutedEventArgs e)
        {
            var hyperlink = sender as Hyperlink;
            Process.Start("explorer.exe", hyperlink.NavigateUri.ToString());
        }

        private void Hyperlink_Click_about(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Version " + version);
        }


        /// <summary>
        /// Print a "block" of plugins, helper function
        /// </summary>
        /// <param name="plugins"></param>
        /// <param name="id"></param>
        /// <param name="errmsg"></param>
        /// <param name="tb"></param>
        /// <param name="fullpath"></param>
        private static void DiagPrintHelper(Dictionary<string, List<string>> plugins, string id, string errmsg, Paragraph tb, bool fullpath = false)
        {
            if (plugins[id].Count() > 0)
            {
                //tb.Text += errmsg;
                tb.Inlines.Add(new Run(errmsg) { FontSize = 14 });
                tb.Inlines.Add(new Run("------------------------------------------------------------\n") { Foreground = Brushes.SlateBlue });
                foreach (var p in plugins[id])
                {
                    if(fullpath)
                    {
                        tb.Inlines.Add("   ");
                        if (Path.IsPathRooted(p))
                            tb.Inlines.Add(new Run(Path.GetDirectoryName(p) + @"\") { Foreground = Brushes.Silver });
                        tb.Inlines.Add(Path.GetFileName(p) + "\n");
                    }
                    else
                    {
                        tb.Inlines.Add("   " + Path.GetFileName(p) + "\n");
                    }
                }
            }
        }


        private async void TabablzControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            // ##### Vapoursynth diagnose #####
        
            if (DiagnoseTab.IsSelected)
            {
                // Textoutput controls init
                RichTextBox richtextbox = new RichTextBox();
                FlowDocument flowdoc = new FlowDocument();
                Paragraph tb = new Paragraph();

                richtextbox.IsDocumentEnabled = true;
                richtextbox.IsReadOnly = true;
                tb.FontFamily = new FontFamily("Lucida Console");
                tb.Padding = new Thickness(8);

                flowdoc.Blocks.Add(tb);
                richtextbox.Document = flowdoc;
                //tb.TextWrapping = TextWrapping.Wrap;


                // http://www.vapoursynth.com/doc/autoloading.html#windows
                // 1. <AppData>\VapourSynth\plugins64
                // 2. <VapourSynth path>\core64\plugins
                // 3. <VapourSynth path>\plugins64
                // vsrepo returns (always?) the path of the AppData folder no 1.
                // User Plugins in core64 are bad.

                AppIsWorking(true);
                bool current_vs_installation_works = true;
                var diag = new Diagnose(vsrepo.python_bin);

                var version = await diag.GetVapoursynthVersion();
                var vs_dll = await diag.GetLoadedVapoursynthDll();

                if (String.IsNullOrWhiteSpace(version))
                    current_vs_installation_works = false;

                Dictionary<string, List<string>> plugins = await diag.CheckPluginsAsync(vsrepo.GetPaths(Win64).Binaries);
                if (!PortableMode)
                {
                    if (current_vs_installation_works)
                    {
                        Dictionary<string, List<string>> plugins64folder = await diag.CheckPluginsAsync(vsregistry.GetCurrentRegPluginsPath());

                        //Check for duplicate dll files in both folders: appdata\plugins and vapoursynth\plugins
                        var p1 = plugins.Values.SelectMany(x => x.Select(p => Path.GetFileName(p))).ToList();
                        var p2 = plugins64folder.Values.SelectMany(x => x.Select(p => Path.GetFileName(p))).ToList();


                        //Merge appdata\plugins and vapoursynth\plugins
                        for (int i = 0; i < plugins.Count; i++)
                        {
                            plugins[plugins.ElementAt(i).Key] = plugins[plugins.ElementAt(i).Key].Concat(plugins64folder[plugins.ElementAt(i).Key]).ToList();
                        }

                        plugins["duplicate_dlls"] = p1.Intersect(p2).ToList();
                    }
                    else
                    {
                        // plugins["duplicate_dlls"] = new List<string>();
                    }

                }
                else
                {
                    plugins["duplicate_dlls"] = new List<string>();
                }


                var SystemInfo = new SystemInfo();

                tb.Inlines.Add(@"/!\ Only Plugins and no Scripts are tested /!\");
                if (current_vs_installation_works)
                    tb.Inlines.Add("\n" + version);
                else
                    tb.Inlines.Add(new Run("\n\nImporting VapourSynth in python failed! \n") { FontSize = 15, FontWeight = FontWeights.Bold, Foreground = Brushes.Red });
                tb.Inlines.Add("\nOS: " + SystemInfo.OS);
                tb.Inlines.Add("\n64Bit OS: " + (SystemInfo.Is64Bit ? "Yes" : "No"));
                if(SystemInfo.HasMultipleGpus)
                {
                    int i = 0;
                    foreach (Gpu gpu in SystemInfo.Gpus) 
                    {
                        tb.Inlines.Add("\nGPU"+ (i++) +": " + gpu.Description);
                    }
                }
                else
                {
                    tb.Inlines.Add("\nGPU: " + SystemInfo.Gpu.Description);
                }
                if (SystemInfo.HasMultipleCpus)
                {
                    int i = 0;
                    foreach (Cpu cpu in SystemInfo.Cpus)
                    {
                        tb.Inlines.Add("\nCPU" + (i++) + ": " + cpu.Name);
                    }
                    tb.Inlines.Add("\nTotal CPU Cores: " + SystemInfo.TotalCpuCores);
                    tb.Inlines.Add("\nTotal Logical Processors: " + SystemInfo.TotalLogicalProcessors);
                }
                else
                {
                    tb.Inlines.Add("\nCPU: " + SystemInfo.Cpu.Name);
                    tb.Inlines.Add("\nCPU Cores: " + SystemInfo.TotalCpuCores);
                    tb.Inlines.Add("\nLogical Processors: " + SystemInfo.TotalLogicalProcessors);
                }

                tb.Inlines.Add("\nRAM: " + SystemInfo.Ram.Size + " GB");

                tb.Inlines.Add(new Run("\n\nPython location: ") { FontSize = 12, FontWeight = FontWeights.Bold });
                tb.Inlines.Add(await diag.GetPythonLocation()); //vsrepo.python_bin


                tb.Inlines.Add(new Run("\nLoaded VapourSynth dll: ") { FontSize = 12, FontWeight = FontWeights.Bold });
                if (vs_dll != null)
                    tb.Inlines.Add(vs_dll + "\n");
                else
                    tb.Inlines.Add(" - \n");

                // Show VS installations found in the registry
                if (vsregistry.Registry.ContainsKey("local64"))
                {
                    tb.Inlines.Add("\nFound an installation in ");
                    tb.Inlines.Add(new Run(@"HKEY_LOCAL_MACHINE\" + vsregistry.Registry["local64"].RegPath) { Foreground = Brushes.Blue });
                    tb.Inlines.Add("\n - Path: " + vsregistry.Registry["local64"].Path);
                    tb.Inlines.Add("\n - PythonPath: " + vsregistry.Registry["local64"].PythonPath);
                    tb.Inlines.Add("\n - Version: " + vsregistry.Registry["local64"].Version);
                }
                if (vsregistry.Registry.ContainsKey("local32"))
                {
                    tb.Inlines.Add("\nFound an installation in ");
                    tb.Inlines.Add(new Run(@"HKEY_LOCAL_MACHINE\" + vsregistry.Registry["local32"].RegPath) { Foreground = Brushes.Blue });
                    tb.Inlines.Add("\n - Path: " + vsregistry.Registry["local32"].Path);
                    tb.Inlines.Add("\n - PythonPath: " + vsregistry.Registry["local32"].PythonPath);
                    tb.Inlines.Add("\n - Version: " + vsregistry.Registry["local32"].Version);
                }
                if (vsregistry.Registry.ContainsKey("user64"))
                {
                    tb.Inlines.Add("\nFound an installation in ");
                    tb.Inlines.Add(new Run(@"HKEY_CURRENT_USER\" + vsregistry.Registry["user64"].RegPath) { Foreground = Brushes.Blue });
                    tb.Inlines.Add("\n - Path: " + vsregistry.Registry["user64"].Path);
                    tb.Inlines.Add("\n - PythonPath: " + vsregistry.Registry["user64"].PythonPath);
                    tb.Inlines.Add("\n - Version: " + vsregistry.Registry["user64"].Version);
                }
                if (vsregistry.Registry.ContainsKey("user32"))
                {
                    tb.Inlines.Add("\nFound an installation in ");
                    tb.Inlines.Add(new Run(@"HKEY_CURRENT_USER\" + vsregistry.Registry["user32"].RegPath) { Foreground = Brushes.Blue });
                    tb.Inlines.Add("\n - Path: " + vsregistry.Registry["user32"].Path);
                    tb.Inlines.Add("\n - PythonPath: " + vsregistry.Registry["user32"].PythonPath);
                    tb.Inlines.Add("\n - Version: " + vsregistry.Registry["user32"].Version);
                }


                tb.Inlines.Add("\n\n============================================================\n");

                if (plugins != null)
                {
                    //Mark plugins (dlls files) which are known to vsrepo
                    Dictionary<string, string> known_files = new Dictionary<string, string>();
                    int count_unident_dll = 0;
                    foreach (var p in plugins["not_a_vsplugin"])
                    {
                        if (plugins_dll_parents.ContainsKey(Path.GetFileName(p)))
                            known_files.Add(p, plugins_dll_parents[Path.GetFileName(p)]);
                        else
                            count_unident_dll++;
                    }


                    tb.Inlines.Add(new Run(string.Format("\nChecked Plugins: {0}, Notices: {1}, Errors: {2}\n\n",
                                plugins.Values.SelectMany(x => x).Count(),
                            //plugins["no_problems"].Count() + plugins["not_a_vsplugin"].Count() + plugins["missing_dependency"].Count() + plugins["wrong_arch"].Count() + plugins["others"].Count(),
                            count_unident_dll + plugins["duplicate_dlls"].Count(),
                            plugins["wrong_arch"].Count() + plugins["missing_dependency"].Count() + plugins["others"].Count() + plugins["namespace"].Count()))
                    { Foreground = Brushes.DarkOrange });

                    if (PortableMode)
                        tb.Inlines.Add(string.Format("Plugin Path: {0}\n", vsrepo.GetPaths(Win64).Binaries));
                    else
                    {
                        Hyperlink link1 = new Hyperlink();
                        Hyperlink link2 = new Hyperlink();
                        link1.RequestNavigate += new System.Windows.Navigation.RequestNavigateEventHandler(Hyperlink_Explorer);
                        link2.RequestNavigate += new System.Windows.Navigation.RequestNavigateEventHandler(Hyperlink_Explorer);
                        link1.IsEnabled = link2.IsEnabled = true;
                        link1.Inlines.Add(vsrepo.GetPaths(Win64).Binaries);
                        link2.Inlines.Add(vsregistry.GetCurrentRegPluginsPath());
                        link1.NavigateUri = new Uri(vsrepo.GetPaths(Win64).Binaries);
                        link2.NavigateUri = new Uri(vsregistry.GetCurrentRegPluginsPath());

                        tb.Inlines.Add("Plugin Paths: \n\t • ");
                        tb.Inlines.Add(link1);
                        tb.Inlines.Add("\n\t • ");
                        tb.Inlines.Add(link2);
                        tb.Inlines.Add("\n");
                        //tb.Inlines.Add(string.Format("Plugin Paths: \n\t{0}\n\t{1}\n", vsrepo.GetPaths(Win64).Binaries, vsregistry.GetCurrentRegPluginsPath()));
                    }


                    DiagPrintHelper(plugins, "wrong_arch", "\n\n🔥 Error 193 - You probably mixed 32/64 bit plugins: \n", tb, true);

                    if (plugins["missing_dependency"].Count() > 0)
                    {
                        tb.Inlines.Add(new Run("\n\n🔥 Error 126 - A DLL dependency is probably missing: \n") { FontSize = 14 });
                        tb.Inlines.Add(new Run("------------------------------------------------------------\n") { Foreground = Brushes.SlateBlue });
                        bool hint_listpedeps = false;
                        foreach (var p in plugins["missing_dependency"])
                        {
                            Diagnose.Depends file_dependencies = null;

                            try
                            {
                                file_dependencies = await diag.GetDllDependencies(p);
                            }
                            catch
                            {
                                hint_listpedeps = true;
                            }

                            if (file_dependencies != null)
                            {
                                tb.Inlines.Add("   ");
                                if (Path.IsPathRooted(p))
                                    tb.Inlines.Add(new Run(Path.GetDirectoryName(p) + @"\") { Foreground = Brushes.Silver });
                                tb.Inlines.Add(Path.GetFileName(p) + "\n");
                                tb.Inlines.Add("   \t requires following dependencies (one of these could be missing):\n\n");

                                foreach (var dependency in file_dependencies.Imports)
                                {
                                    tb.Inlines.Add("   \t - " + dependency + "\n");
                                }
                                tb.Inlines.Add("\n");
                            }
                            else
                            {
                                tb.Inlines.Add("   ");
                                if (Path.IsPathRooted(p))
                                    tb.Inlines.Add(new Run(Path.GetDirectoryName(p) + @"\") { Foreground = Brushes.Silver });
                                tb.Inlines.Add(Path.GetFileName(p) + "\n");
                            }
                        }
                        if (hint_listpedeps)
                        {
                            tb.Inlines.Add("\nInstall listpedeps.exe via  'choco install pedeps'");
                            tb.Inlines.Add("\nor copy listpedeps.exe next to vsrepogui.exe for detailed information about missing dependencies.");
                            tb.Inlines.Add("\nDownload here: https://github.com/brechtsanders/pedeps/releases\n\n");
                        }
                    }

                    DiagPrintHelper(plugins, "namespace", "\n\n🔥 Namespace already populated, therefore it failed to load: \n", tb, true);
                    DiagPrintHelper(plugins, "others", "\n\n🔥 Error unknown: \n", tb, true);

                    var not_a_vsplugin_known = plugins["not_a_vsplugin"].Where(x => known_files.ContainsKey(x)).Select(x => x).ToList();
                    var not_a_vsplugin_unknown = plugins["not_a_vsplugin"].Where(x => !known_files.ContainsKey(x)).Select(x => x).ToList();

                    if (not_a_vsplugin_known.Count() > 0)
                    {
                        tb.Inlines.Add(new Run("\n\n🙂 Identified non-VapourSynth Plugins: \n") { FontSize = 14 });
                        tb.Inlines.Add(new Run("------------------------------------------------------------\n") { Foreground = Brushes.SlateBlue });
                        foreach (var p in not_a_vsplugin_known)
                        {
                            tb.Inlines.Add("   ");
                            if (Path.IsPathRooted(p))
                                tb.Inlines.Add(new Run(Path.GetDirectoryName(p) + @"\") { Foreground = Brushes.Silver });
                            tb.Inlines.Add(Path.GetFileName(p) + "\t");
                            tb.Inlines.Add(new Run("[belongs to " + known_files[p] + "]\n") { Foreground = Brushes.Orange });
                        }
                    }
                    if (not_a_vsplugin_unknown.Count() > 0)
                    {
                        tb.Inlines.Add(new Run("\n\n🤨 Unidentified DLLs (maybe also Plugin dependencies?): \n") { FontSize = 14 });
                        tb.Inlines.Add(new Run("------------------------------------------------------------\n") { Foreground = Brushes.SlateBlue });
                        foreach (var p in not_a_vsplugin_unknown)
                        {
                            tb.Inlines.Add("   ");
                            if (Path.IsPathRooted(p))
                                tb.Inlines.Add(new Run(Path.GetDirectoryName(p) + @"\") { Foreground = Brushes.Silver });
                            tb.Inlines.Add(Path.GetFileName(p) + "\n");
                        }
                    }
                    DiagPrintHelper(plugins, "duplicate_dlls", "\n\n🤨 These dlls exits in both folders: \n", tb, true);

                    //DiagPrintHelper(plugins, "not_a_vsplugin", "\n\n🤨 Notice - Probably a Plugin dependency (not a VS Plugin): \n");
                    DiagPrintHelper(plugins, "no_problems", "\n\n👍 Successfully loaded Plugins: \n", tb, false);

                }
                else
                {
                    tb.Inlines.Add("Could not test plugins");
                }

                ScrollViewer.Content = richtextbox;
                AppIsWorking(false);
            }
            
            
        }

    }


}
