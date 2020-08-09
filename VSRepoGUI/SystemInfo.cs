using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;

namespace VSRepoGUI
{
    class SystemInfo
    {
        public string OS { get; set; }
        public bool Is64Bit { get; set; }

        public CPU Cpu { get; set; }
        public List<CPU> Cpus { get; set; }
        public int TotalCpuCores { get; set; } = 0;
        public int TotalLogicalProcessors { get; set; } = 0;
        public GPU Gpu { get; set; }
        public List<GPU> Gpus { get; set; }
        public RAM Ram { get; set; } = new RAM(); // in GB

        public bool HasMultipleCpus { get; set; } = false;
        public bool HasMultipleGpus { get; set; } = false;


        public SystemInfo()
        {
            OS = (from x in new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem").Get().Cast<ManagementObject>()
                    select x.GetPropertyValue("Caption")).FirstOrDefault().ToString();
            Is64Bit = Environment.Is64BitOperatingSystem;

            var cpus = GetCpus();
            foreach (CPU item in cpus)
            {
                TotalCpuCores += item.Cores;
                TotalLogicalProcessors += item.LogicalProcessors;
            }

            if (cpus.Count() > 1)
            {
                HasMultipleCpus = true;
                Cpus = cpus;
            }
            else
            {
                if (cpus.Count() == 1)
                    Cpu = cpus[0];
                else
                    Cpu = new CPU() { Name = "cpu detection failed" };
            }


            var gpus = GetGpus();
            if (gpus.Count() > 1)
            {
                HasMultipleGpus = true;
                Gpus = gpus;
            }
            else
            {
                if (gpus.Count() == 1)
                    Gpu = gpus[0];
                else
                    Gpu = new GPU() { Name = "Not detected", Description = "Not detected" };
            }

            SetRamInfo();

        }
        public List<CPU> GetCpus()
        {
            var cpu_list = new List<CPU>();

            ManagementObjectSearcher mos = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Processor");

            foreach (ManagementObject mo in mos.Get())
            {
                CPU cpu = new CPU
                {
                    DeviceId = mo["DeviceID"].ToString(),
                    Name = mo["Name"].ToString(),
                    Cores = int.Parse(mo["NumberOfCores"].ToString()),
                    LogicalProcessors = int.Parse(mo["NumberOfLogicalProcessors"].ToString()),
                };
                cpu_list.Add(cpu);
            }
            return cpu_list;            
        }


        public List<GPU> GetGpus()
        {
            var gpu_list = new List<GPU>();
      
            ManagementObjectSearcher mos = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_VideoController");

            foreach (ManagementObject mo in mos.Get())
            {
                GPU cpu = new GPU
                {
                    DeviceId = mo["DeviceID"].ToString(),
                    Description = mo["Description"].ToString(),
                    Name = mo["Name"].ToString(),
                };
                gpu_list.Add(cpu);
            }
            return gpu_list;
        }

        private void SetRamInfo()
        {
            ManagementObjectSearcher mos_ram = new ManagementObjectSearcher("root\\CIMV2", "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
            Ram.Size = (int)Math.Round(Convert.ToDouble(mos_ram.Get().OfType<ManagementObject>().FirstOrDefault()["TotalPhysicalMemory"].ToString()) / 1024 / 1024 / 1024, 0);
        }
    }



    public class CPU
    {
        public string DeviceId { get; set; }
        public string Name { get; set; }
        public int Cores { get; set; }
        public int LogicalProcessors { get; set; }
    }
    public class GPU
    {
        public string DeviceId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }
    public class RAM
    {
        public int Size { get; set; } 
    }
}
