using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace VSRepoGUI
{
    public class VsApi : INotifyPropertyChanged
    {
        private string portable = "";
        private object result;
        public enum PluginStatus : int { NotInstalled, Installed, InstalledUnknown, UpdateAvailable };
        public bool Win64;
        public Dictionary<bool, Paths> paths = new Dictionary<bool, Paths>();
        private string vsrepo_path = "vsrepo.py";
        public string consolestd { get; set; }
        public string python_bin = "python.exe"; //"C:\\Python37\\python.exe"
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }



        public async Task Uninstall(string plugin)
        {
            await Task.Run(() => Run("uninstall", plugin));
            //var result = (List<string>)this.result;
            //return result;
        }

        public async Task Install(string plugin, bool force = false)
        {
            string install = "install";
            if (force)
                install = "-f install";
            await Task.Run(() => Run(install, plugin));
        }

        public Dictionary<string, KeyValuePair<string, PluginStatus>> GetInstalled()
        {
            Run("installed");
            var result = (Dictionary<string, KeyValuePair<string, PluginStatus>>)this.result;
            return result;
        }

        public async Task<Dictionary<string, KeyValuePair<string, PluginStatus>>> GetInstalledAsync()
        {
            return await Task.Run(() => GetInstalled());
        }

        /// <summary>
        /// bit for 32 and 64 bit
        /// </summary>
        /// <param name="bit"></param>
        /// <returns></returns>
        public Paths GetPaths(bool bit)
        {
            if (!paths.ContainsKey(bit))
            {
                Run("paths");
                var result = (List<string>)this.result;
                var _paths = new Paths
                {
                    Definitions = result[0],
                    Binaries = result[1],
                    Scripts = result[2]
                };
                paths.Add(bit, _paths);
            }
            return paths[bit];
        }
        public void SetPaths(bool bit, Paths paths)
        {
            this.paths[bit] = paths;
        }

        public void Update()
        {
            Run("update");
        }

        public async Task Upgrade(string plugin, bool force = false)
        {
            string upgrade = "upgrade";
            if (force)
                upgrade = "-f upgrade";
            await Task.Run(() => Run(upgrade, plugin));
        }

        public async Task UpgradeAll()
        {
            await Task.Run(() => Run("upgrade-all"));
        }

        public void SetPortableMode(bool status)
        {
            if (status)
                this.portable = "-p";
            else
                this.portable = "";
        }

        public void SetArch(bool bit)
        {
            this.Win64 = bit;
        }

        public void SetVsrepoPath(string path)
        {
            this.vsrepo_path = path;
        }

        public string getTarget(string operation)
        {
            /*List<string> targetCommands = new List<string>
            {
                "install",
                "upgrade",
                "upgrade-all",
                "uninstall",
            };

            if (!targetCommands.Contains(operation)) {
                return "";
            }*/
            if (Win64)
                return "-t win64";
            return "-t win32";
        }

        public string getCustomPaths()
        {
            if (!String.IsNullOrEmpty(portable))
            {
                if(paths.ContainsKey(Win64))
                {
                    return String.Format("-b \"{0}\" -s \"{1}\"", paths[Win64].Binaries, paths[Win64].Scripts);
                }
                return "";                
            }
            return "";
        }

        private object Run(string operation, string plugins = "")
        {


            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = python_bin,
                    Arguments = String.Format("\"{0}\" {1} {2} {3} {4} {5}", vsrepo_path, portable, getCustomPaths(), getTarget(operation), operation, plugins), //-p install grain
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show("There is a problem with python" + ex.ToString());
                //System.Environment.Exit(1);
            }
            Console.WriteLine("### RUN: " + process.StartInfo.Arguments);
            //process.BeginOutputReadLine();
            //string error = process.StandardError.ReadToEnd();
            //process.WaitForExit();


            string result_std;
            List<string> paths = new List<string>();
            var installed = new Dictionary<string, KeyValuePair<string, PluginStatus>>();
            //string result = process.StandardOutput.ReadToEnd();
            while ((result_std = process.StandardOutput.ReadLine()) != null)
            {
                switch (operation)
                {
                    case "installed":
                        
                        if (!result_std.Contains("Identifier"))
                        {
                            //Console.WriteLine(result_std);

                            var status = PluginStatus.Installed;
                            if (result_std[0].ToString() == "+")
                            {
                                status = PluginStatus.InstalledUnknown;
                            }
                            if (result_std[0].ToString() == "*")
                            {
                                status = PluginStatus.UpdateAvailable;
                            }
                            string lastWord = result_std.Split(' ').Last().Trim();
                            var localversion = result_std.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Reverse().Skip(2).Reverse().Last().Trim();
                            var kv = new KeyValuePair<string, PluginStatus>(localversion.ToString(), status);

                            installed.Add(lastWord, kv);
                            Console.WriteLine(lastWord);
                        }
                        this.result = installed;
                        break;

                    case "paths":
                        if (!result_std.Contains("Paths"))
                        {
                            string fitlered = string.Join(" ", result_std.Split(' ').Skip(1)).Trim();
                            paths.Add(fitlered);
                            Console.WriteLine(fitlered);
                        }
                        this.result = paths;
                        break;
                }
            }

            process.WaitForExit();
            return result; // #### TODO process.ExitCode
        }
    }


    /// <summary>
    /// Contains paths used by vsrepo.py
    /// </summary>
    public class Paths
    {
        public string Definitions;
        public string Binaries;
        public string Scripts;
    }

}
