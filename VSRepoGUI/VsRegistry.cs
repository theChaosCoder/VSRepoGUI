using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSRepoGUI
{
    public class VsRegistry
    {
        public string RegPath { get; set; }

        public string CorePlugins { get; set; }
        public string Path { get; set; }
        public string Plugins { get; set; }
        public string PythonPath { get; set; }
        public string VapourSynthDLL { get; set; }
        public string Version { get; set; }
        public string VSScriptDLL { get; set; }

        public VsRegistry GetRegistry(RegistryKey regkey, string subkey)
        {
            var key = regkey.OpenSubKey(subkey);
            if (key == null)
                return null;

            RegPath = subkey;

            foreach (var item in key.GetValueNames())
            {
                switch (item)
                {
                    case "CorePlugins":
                        CorePlugins = (string)key.GetValue("CorePlugins"); break;
                    case "Path":
                        Path = (string)key.GetValue("Path"); break;
                    case "Plugins":
                        Plugins = (string)key.GetValue("Plugins"); break;
                    case "PythonPath":
                        PythonPath = (string)key.GetValue("PythonPath"); break;
                    case "VapourSynthDLL":
                        VapourSynthDLL = (string)key.GetValue("VapourSynthDLL"); break;
                    case "Version":
                        Version = (string)key.GetValue("Version"); break;
                    case "VSScriptDLL":
                        VSScriptDLL = (string)key.GetValue("VSScriptDLL"); break;
                }
            }
            return this;
        }
    }

    public class VsRegistryHelper
    {

        RegistryKey localKey32 = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry32);
        RegistryKey localKey64 = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry64);
        RegistryKey userKey32 = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.CurrentUser, RegistryView.Registry64);
        RegistryKey userKey64 = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.CurrentUser, RegistryView.Registry64);

        public Dictionary<string, VsRegistry> Registry = new Dictionary<string, VsRegistry>();
        public string current_vs_installation;


        public bool SetCurrentVsInstallation(string python_path)
        {
            foreach(var regitem in Registry)
            {
                if(regitem.Value.PythonPath.ToLower().TrimEnd('\\') == Path.GetDirectoryName(python_path).ToLower().TrimEnd('\\'))
                {
                    current_vs_installation = regitem.Key;
                    return true;
                }
            }
            return false;
        }

        public string GetCurrentRegPath()
        {
            if (Registry.Count() > 0)
                return Registry[current_vs_installation].Path;

            return null;
        }

        public string GetCurrentRegPluginsPath()
        {
            if (Registry.Count() > 0)
                return Registry[current_vs_installation].Plugins;

            return null;
        }

        public string GetAvailablePythonPath()
        {
            if (Registry.Count() > 0)
                return Registry[current_vs_installation].PythonPath;

            return null;
        }

        public void ReadAllVsRegistryInfos()
        {
            var dict = new Dictionary<string, VsRegistry>();

            var regl32 = new VsRegistry().GetRegistry(localKey32, @"SOFTWARE\VapourSynth-32");
            var regl64 = new VsRegistry().GetRegistry(localKey64, @"SOFTWARE\VapourSynth");
            var regu32 = new VsRegistry().GetRegistry(userKey32, @"SOFTWARE\VapourSynth-32");
            var regu64 = new VsRegistry().GetRegistry(userKey64, @"SOFTWARE\VapourSynth");

            if (regu32 != null)
            {
                dict["user32"] = regu32;
                current_vs_installation = "user32";
            }

            if (regu64 != null)
            {
                dict["user64"] = regu64;
                current_vs_installation = "user64";
            }

            if (regl32 != null)
            {
                dict["local32"] = regl32;
                current_vs_installation = "local32";
            }

            if (regl64 != null)
            {
                dict["local64"] = regl64;
                current_vs_installation = "local64";
            }

            this.Registry = dict;
        }

    }
}
