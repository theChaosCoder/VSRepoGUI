using System;
using System.Collections.Generic;

using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace VSRepoGUI
{
     public partial class Settings
    {
        [JsonProperty("Bin")]
        public string Bin { get; set; }

        [JsonProperty("win32")]
        public Win Win32 { get; set; }

        [JsonProperty("win64")]
        public Win Win64 { get; set; }

        private string settingsfile = "vsrepogui.json";

        public Settings LoadLocalFile()
        {
            if (File.Exists(settingsfile))
            {
                //string joutput = JsonConvert.SerializeObject(a);
                //System.IO.File.WriteAllText("vsrepogui.json", joutput);
                var jsonString = File.ReadAllText(settingsfile);
                return JsonConvert.DeserializeObject<Settings>(jsonString);

            }
            return null;
        }

        public void SaveLocalFile()
        {
            throw new NotImplementedException();
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
