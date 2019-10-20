using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSRepoGUI
{
    public class AvsRegistry
    {
        public string RegPath { get; set; }

        public string InstallPath { get; set; }
        public string PluginDirplus { get; set; }
        public string PluginDir2_5 { get; set; }


        public AvsRegistry GetRegistry(RegistryKey regkey, string subkey)
        {
            var key = regkey.OpenSubKey(subkey);
            if (key == null)
                return null;

            RegPath = subkey;

            foreach (var item in key.GetValueNames())
            {
                switch (item)
                {
                    case "":
                        InstallPath = (string)key.GetValue(""); break;
                    case "PluginDir+":
                        PluginDirplus = (string)key.GetValue("PluginDir+"); break;
                    case "PluginDir2_5":
                        PluginDir2_5 = (string)key.GetValue("PluginDir2_5"); break;
                }
            }
            return this;
        }
    }

    public class AvsRegistryHelper
    {

        RegistryKey localKey32 = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry32);
        RegistryKey localKey64 = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry64);

        public Dictionary<string, AvsRegistry> Registry = new Dictionary<string, AvsRegistry>();


        public void ReadAllAvsRegistryInfos()
        {
            var dict = new Dictionary<string, AvsRegistry>();

            var regl32 = new AvsRegistry().GetRegistry(localKey32, @"SOFTWARE\Avisynth");
            var regl64 = new AvsRegistry().GetRegistry(localKey64, @"SOFTWARE\Avisynth");

            if (regl32 != null)
            {
                dict["avs32"] = regl32;
            }

            if (regl64 != null)
            {
                dict["avs64"] = regl64;
            }

            this.Registry = dict;
        }

    }
}
