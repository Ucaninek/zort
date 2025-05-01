using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace zort
{
    public class ServiceManager
    {

        [RunInstaller(true)]
        public class ProjectInstaller : Installer
        {
            private readonly ServiceInstaller serviceInstaller;
            private readonly ServiceProcessInstaller processInstaller;

            public ProjectInstaller()
            {
                serviceInstaller = new ServiceInstaller();
                processInstaller = new ServiceProcessInstaller
                {
                    Account = ServiceAccount.LocalSystem
                };

                serviceInstaller.StartType = ServiceStartMode.Automatic;
                serviceInstaller.ServiceName = "conhostsvc";

                Installers.Add(serviceInstaller);
                Installers.Add(processInstaller);
            }
        }
    }
}
