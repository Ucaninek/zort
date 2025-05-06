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
    public class ElevationHelper
    {

        public static bool IsElevated()
        {
            var adminGroup = new System.Security.Principal.SecurityIdentifier(
                System.Security.Principal.WellKnownSidType.BuiltinAdministratorsSid, null);
            var principal = new System.Security.Principal.WindowsPrincipal(
                System.Security.Principal.WindowsIdentity.GetCurrent());
            return principal.IsInRole(adminGroup);
        }

        public static void ForceElevate(string path = null)
        {
            bool didElevate = false;
            while (!didElevate)
            {
                didElevate = TryElevate(path);
                Thread.Sleep(1000);
            }
        }

        public static bool TryElevate(string path = null)
        {
            try
            {
                if (path == null) path = Assembly.GetExecutingAssembly().Location;
                var startInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    FileName = "cmd.exe",
                    Arguments = $"/c start \"\" \"{Path.GetFullPath(path)}\"",
                    CreateNoWindow = true,
                };
                var process = Process.Start(startInfo);
                if(!process.WaitForExit(5000))
                {
                    process.Kill();
                    return false;
                }
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
    }
}
