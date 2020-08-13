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
        public string python_bin = "python.exe";
        public event PropertyChangedEventHandler PropertyChanged;



        public async Task UninstallAsync(string plugin)
        {
            await Task.Run(() => Run("uninstall", plugin));
            //var result = (List<string>)this.result;
            //return result;
        }

        public async Task InstallAsync(string plugin, bool force = false)
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
        /// false for 32 and true for 64 bit
        /// </summary>
        /// <param name="isWin64"></param>
        /// <returns>Paths</returns>
        public Paths GetPaths(bool isWin64)
        {
            if (!paths.ContainsKey(isWin64))
            {
                Run("paths");
                var result = (List<string>)this.result;
                var _paths = new Paths
                {
                    Definitions = result[0],
                    Binaries = result[1],
                    Scripts = result[2]
                };
                if(result.Count == 4)
                {
                    _paths.DistInfos = result[3];
                }
                paths.Add(isWin64, _paths);
            }
            return paths[isWin64];
        }
        public void SetPaths(bool bit, Paths paths)
        {
            this.paths[bit] = paths;
        }

        public void Update()
        {
            Run("update");
        }

        public async Task UpgradeAsync(string plugin, bool force = false)
        {
            string upgrade = "upgrade";
            if (force)
                upgrade = "-f upgrade";
            await Task.Run(() => Run(upgrade, plugin));
        }

        public async Task UpgradeAllAsync()
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

        public void SetArch(bool isWin64)
        {
            this.Win64 = isWin64;
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
            }
            return "";
        }

        private object Run(string operation, string plugins = "")
        {
            string args = String.Format("\"{0}\" {1} {2} {3} {4} {5}", vsrepo_path, portable, getCustomPaths(), getTarget(operation), operation, plugins);

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = python_bin,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
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
            Console.WriteLine("### RUN: " + process.StartInfo.FileName + " " + process.StartInfo.Arguments);
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
                    case "install":
                    case "uninstall":
                        consolestd = result_std;
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
        public string DistInfos;
    }

}
