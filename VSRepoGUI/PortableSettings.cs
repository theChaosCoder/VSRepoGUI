using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Newtonsoft.Json;

namespace VSRepoGUI
{
    public partial class PortableSettings
    {
        [JsonProperty("Bin")]
        public string Bin { get; set; }

        [JsonProperty("win32")]
        public Win Win32 { get; set; }

        [JsonProperty("win64")]
        public Win Win64 { get; set; }

        private string settingsfile = "vsrepogui.json";

        public PortableSettings LoadLocalFile(string file)
        {
            this.settingsfile = file;
            return LoadLocalFile();
        }

        public PortableSettings LoadLocalFile()
        {
            if (File.Exists(MakeFullPath(settingsfile)))
            {
                var jsonString = File.ReadAllText(MakeFullPath(settingsfile));
                try
                {
                    var settingsFile = JsonConvert.DeserializeObject<PortableSettings>(jsonString);

                    settingsFile.Bin = MakeFullPath(settingsFile.Bin);
                    settingsFile.Win32.Binaries = MakeFullPath(settingsFile.Win32.Binaries);
                    settingsFile.Win32.Scripts = MakeFullPath(settingsFile.Win32.Scripts);
                    settingsFile.Win64.Binaries = MakeFullPath(settingsFile.Win64.Binaries);
                    settingsFile.Win64.Scripts = MakeFullPath(settingsFile.Win64.Scripts);
                    return settingsFile;
                } catch(Exception e)
                {
                    MessageBox.Show("vsrepogui.json is invalid");
                    return null;
                }
            }
            return null;
        }

        public void SaveLocalFile()
        {
            throw new NotImplementedException();
            //string joutput = JsonConvert.SerializeObject(a);
            //System.IO.File.WriteAllText("vsrepogui.json", joutput);
        }

        public string MakeFullPath(string path)
        {
            if (!Path.IsPathRooted(path)) {
                return Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) + "\\" + path;
            }
            return path;
        }
    }

    public partial class Win
    {
        [JsonProperty("Binaries")]
        public string Binaries { get; set; }

        [JsonProperty("Scripts")]
        public string Scripts { get; set; }
    }

}
