using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace VSRepoGUI
{
    class SystemInfo
    {
        public string OS { get; set; }
        public bool Is64Bit { get; set; }

        public Cpu Cpu { get; set; }
        public List<Cpu> Cpus { get; set; }
        public int TotalCpuCores { get; set; } = 0;
        public int TotalLogicalProcessors { get; set; } = 0;
        public Gpu Gpu { get; set; }
        public List<Gpu> Gpus { get; set; }
        public Ram Ram { get; set; } = new Ram(); // in GB

        public bool HasMultipleCpus { get; set; } = false;
        public bool HasMultipleGpus { get; set; } = false;


        public SystemInfo()
        {
            OS = (from x in new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem").Get().Cast<ManagementObject>()
                    select x.GetPropertyValue("Caption")).FirstOrDefault().ToString();
            Is64Bit = Environment.Is64BitOperatingSystem;
            SetCpuInfo();
            SetGpuInfo();
            SetRamInfo();

        }
        private void SetCpuInfo()
        {
            var cpu_list = new List<Cpu>();
            var cpu_count = 0;
            var hasMoreCpus = true;

            while(hasMoreCpus)
            {
                ManagementObjectSearcher mos = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Processor WHERE DeviceID='CPU" + cpu_count + "'");
                var mos_collection = mos.Get();

                if(mos_collection.Count == 1)
                {
                    var cpu = new Cpu();
                    foreach (ManagementObject mo in mos_collection)
                    {
                        foreach (PropertyData prop in mo.Properties)
                        {
                            if (prop.Name == "DeviceID")
                            {
                                cpu_count++;
                                cpu.DeviceId = prop.Value.ToString();
                            }
                            if (prop.Name == "Name")
                                cpu.Name = prop.Value.ToString();

                            if (prop.Name == "NumberOfCores")
                                cpu.Cores = int.Parse(prop.Value.ToString());

                            if (prop.Name == "NumberOfLogicalProcessors")
                                cpu.LogicalProcessors += int.Parse(prop.Value.ToString());
                        }
                    }
                    cpu_list.Add(cpu);
                } 
                else
                {
                    hasMoreCpus = false;
                }
            } // while

            foreach (Cpu item in cpu_list)
            {
                TotalCpuCores += item.Cores;
                TotalLogicalProcessors += item.LogicalProcessors;
            }

            if (cpu_list.Count() > 1)
            {
                HasMultipleCpus = true;
                Cpus = cpu_list;
            } 
            else
            {
                if (cpu_list.Count() == 1)
                    Cpu = cpu_list[0];
                else
                    Cpu = new Cpu() { Name = "cpu detection failed"};
            }
        }


        private void SetGpuInfo()
        {
            var gpu_list = new List<Gpu>();
            var gpu_count = 0;
            var hasMoreGpus = true;

            while (hasMoreGpus)
            {
                ManagementObjectSearcher mos = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_VideoController WHERE DeviceID='VideoController" + gpu_count + "'");
                var mos_collection = mos.Get();

                if (mos_collection.Count == 1)
                {
                    var gpu = new Gpu();
                    foreach (ManagementObject mo in mos_collection)
                    {
                        foreach (PropertyData prop in mo.Properties)
                        {
                            Console.WriteLine("{0}: {1}", prop.Name, prop.Value);
                         
                            if (prop.Name == "DeviceID")
                                gpu.DeviceId = prop.Value.ToString();

                            if (prop.Name == "Name")
                                gpu.Name = prop.Value.ToString();

                            if (prop.Name == "Description")
                                gpu.Description = prop.Value.ToString();

                        }
                        //hasMoreGpus = false;
                    }
                    gpu_list.Add(gpu);
                    
                }
                else
                {
                    if(gpu_count > 0) // Not sure if VideoController always starts count from 1 and not 0
                    {
                        hasMoreGpus = false;
                    }
                        
                }
                gpu_count++;
            } // while


            if (gpu_list.Count() > 1)
            {
                HasMultipleGpus = true;
                Gpus = gpu_list;
            }
            else
            {
                if (gpu_list.Count() == 1)
                    Gpu = gpu_list[0];
                else
                    Gpu = new Gpu() { Name = "Not detected", Description = "Not detected" };
            }
        }

        private void SetRamInfo()
        {
            ManagementObjectSearcher mos_ram = new ManagementObjectSearcher("root\\CIMV2", "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
            Ram.Size = (int)Math.Round(Convert.ToDouble(mos_ram.Get().OfType<ManagementObject>().FirstOrDefault()["TotalPhysicalMemory"].ToString()) / 1024 / 1024 / 1024, 0);
        }
    }



    public class Cpu
    {
        public string DeviceId { get; set; }
        public string Name { get; set; }
        public int Cores { get; set; }
        public int LogicalProcessors { get; set; }
    }
    public class Gpu
    {
        public string DeviceId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }
    public class Ram
    {
        public int Size { get; set; } 
    }
}
