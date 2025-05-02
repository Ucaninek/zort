using System.Text;
using System.Management;
using System.Security.Cryptography;

namespace Outbuilt
{
    public class Fingerprinting
    {
        private static string fingerPrint = string.Empty;

        public static string HWID()
        {
            if (string.IsNullOrEmpty(fingerPrint))
            {
                fingerPrint = GetHash("CPU >> " + cpuId() + "\nVIDEO >> " + videoId());
            }
            return fingerPrint;
        }

        private static string GetHash(string s)
        {
            using (MD5 sec = MD5.Create())
            {
                byte[] bt = Encoding.ASCII.GetBytes(s);
                return GetHexString(sec.ComputeHash(bt));
            }
        }

        private static string GetHexString(byte[] bt)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < bt.Length; i++)
            {
                sb.Append(bt[i].ToString("X2"));
                if ((i + 1) != bt.Length && (i + 1) % 2 == 0) sb.Append("-");
            }
            return sb.ToString();
        }

        private static string identifier(string wmiClass, string wmiProperty)
        {
            try
            {
                using (ManagementClass mc = new ManagementClass(wmiClass))
                {
                    using (ManagementObjectCollection moc = mc.GetInstances())
                    {
                        foreach (ManagementObject mo in moc)
                        {
                            try
                            {
                                return mo[wmiProperty]?.ToString() ?? string.Empty;
                            }
                            catch
                            {
                                // Ignore individual property access errors
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore WMI class access errors
            }
            return string.Empty;
        }

        private static string cpuId()
        {
            string retVal = identifier("Win32_Processor", "UniqueId");
            if (string.IsNullOrEmpty(retVal))
            {
                retVal = identifier("Win32_Processor", "ProcessorId");
                if (string.IsNullOrEmpty(retVal))
                {
                    retVal = identifier("Win32_Processor", "Name");
                    if (string.IsNullOrEmpty(retVal))
                    {
                        retVal = identifier("Win32_Processor", "Manufacturer");
                    }
                }
            }
            return retVal;
        }

        private static string videoId()
        {
            string driverVersion = identifier("Win32_VideoController", "DriverVersion");
            string name = identifier("Win32_VideoController", "Name");
            return driverVersion + name;
        }
    }
}