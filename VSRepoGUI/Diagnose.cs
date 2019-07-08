using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace VSRepoGUI
{
    public class Diagnose
    {
        public string python_bin = "python.exe";
        

        public Diagnose(string pythonbin)
        {
            this.python_bin = pythonbin;
        }


        /// <summary>
        /// Result [["continuity.dll", "Plugin load failed, namespace edgefixer already populated (D:\\xy\\plugins64\\continuity.dll)"], 
        ///         ["cublas64_80.dll", "No entry point found in D:\\xy\\plugins64\\cublas64_80.dll"], ... ]]
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, List<string>>> CheckPluginsAsync(string path)
        {
            Dictionary<string, List<string>> dllfiles = new Dictionary<string, List<string>>();
            string code = GetDllCheckCode(path);
            string result = await Task.Run(() => run_python(code));
            Console.WriteLine(result);
            string[][] packages;
            try
            {
                packages = JsonConvert.DeserializeObject<string[][]>(result);
                if (packages == null)
                    return null;
            }
            catch
            {
                return null;
            }
            dllfiles.Add("no_problems", (from file in packages
                                         where file[1].Contains("already loaded")
                                         select Path.Combine(path, file[0])).OrderBy(f => f).ToList());

            dllfiles.Add("not_a_vsplugin", (from file in packages
                                            where file[1].Contains("No entry point found")
                                            select Path.Combine(path, file[0])).OrderBy(f => f).ToList());

            dllfiles.Add("wrong_arch", (from file in packages
                                        where file[1].Contains("returned 193")
                                        select Path.Combine(path, file[0])).OrderBy(f => f).ToList());

            dllfiles.Add("missing_dependency", (from file in packages
                                        where file[1].Contains("returned 126")
                                        select Path.Combine(path, file[0])).OrderBy(f => f).ToList());

            dllfiles.Add("namespace", (from file in packages
                                        where file[1].Contains("already populated")
                                        select Path.Combine(path, file[0])).OrderBy(f => f).ToList());
            
            dllfiles.Add("others", (from file in packages
                                    where !(file[1].Contains("returned 193") || file[1].Contains("No entry point found") || file[1].Contains("already loaded") || file[1].Contains("returned 126") || file[1].Contains("already populated"))
                                    select Path.Combine(path, file[0])).OrderBy(f => f).ToList());
            return dllfiles;
        }

        public async Task<string> GetVapoursynthVersion()
        {
            return await Task.Run(() => run_python("import vapoursynth as vs; core = vs.get_core(); print(core.version())"));
        }


        public async Task<string> GetLoadedVapoursynthDll()
        {
            var result = await Task.Run(() => run_python("import vapoursynth; print(vapoursynth.__file__)").Trim());
            if (String.IsNullOrEmpty(result))
                return null;
            return result.Split('.')[0] + ".dll";
        }


        public async Task<string> GetPythonLocation()
        {
            return await Task.Run(() => run_python("import sys; print(sys.executable)").Trim());
        }
        
        public async Task<Depends> GetDllDependencies(string file)
        {
            var result = await Task.Run(() => run_listpedeps(file));
            var dep = ParseDepends(result);
            if (dep == null)
                return null;
            else
                return dep;
        }


        private Depends ParseDepends(string input)
        {
            var dep = new Depends();

            var lines = input.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
  
            dep.file = input;
            dep.architecture = Array.FindAll(lines, c => c.Contains("architecture:"))[0].Split(':')[1].Trim();
            dep.machine_name = Array.FindAll(lines, c => c.Contains("machine name:"))[0].Split(':')[1].Trim();
            dep.subsystem = Array.FindAll(lines, c => c.Contains("subsystem:"))[0].Split(':')[1].Trim();
            dep.minimum_windows_version = Array.FindAll(lines, c => c.Contains("minimum Windows version:"))[0].Split(':')[1].Trim();

            var split_import = input.Split(new string[] { "IMPORTS" }, StringSplitOptions.RemoveEmptyEntries)[1].Split(new string[] { "EXPORTS" }, StringSplitOptions.RemoveEmptyEntries)[0].Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach(var import in split_import)
            {
                var imp_tmp = import.Split(':');
                if(imp_tmp[0].Trim() != "KERNEL32.dll")
                {
                    dep.Imports.Add(imp_tmp[0].Trim());
                }
            }
            dep.Imports = dep.Imports.Distinct().ToList();

            var split_export = input.Split(new string[] { "EXPORTS" }, StringSplitOptions.RemoveEmptyEntries)[1].Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var export in split_export)
            {
                var exp_tmp = export.Split(':');
                dep.Exports.Add(exp_tmp[0].Trim());
                if (exp_tmp[1].Trim().Contains("VapourSynthPluginInit"))
                    dep.IsVapourSynthPlugin = true;
            }
            dep.Exports = dep.Exports.Distinct().ToList();

            return dep;
        }


        //Do not format this code string
        public string GetDllCheckCode(string path)
        {
            path = path.Replace(@"\\", @"\");
            return string.Format(
@"import sys, os, glob, json
import vapoursynth as vs
core = vs.get_core();
path = path = r'{0}'
plugin_dir = glob.glob(path + '/*.dll')
error = []
for dll in plugin_dir:
    try:
        core.std.LoadPlugin(path = dll)
        core.std.LoadPlugin(path = dll)
    except Exception as e:
        error.append([os.path.basename(dll), str(e)])
        continue
print(json.dumps(error))
            ", path);
        }

        public string run_python(string cmd)
        {
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
                    return result;
                }
            }
        }

        public string run_listpedeps(string file)
        {
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = "listpedeps.exe";
            start.Arguments = string.Format("\"{0}\"", file);
            start.UseShellExecute = false;// Do not use OS shell
            start.CreateNoWindow = true; // We don't need new window
            start.RedirectStandardOutput = true;// Any output, generated by application will be redirected back
            start.RedirectStandardError = false; // Any error in standard output will be redirected back (for example exceptions)
            try
            {
                using (Process process = Process.Start(start))
                {
                    using (StreamReader reader = process.StandardOutput)
                    {
                        string result = reader.ReadToEnd();
                        return result;
                    }
                }
            } catch
            {
                return null;
            }
            
        }

        public class Depends
        {
            public string file;
            public string architecture;
            public string machine_name;
            public string subsystem;
            public string minimum_windows_version;
            public List<string> Imports = new List<string>();
            public List<string> Exports = new List<string>();
            public bool IsVapourSynthPlugin = false;
            public bool IsAvisynthPlugin = false;
        }
    }
}
