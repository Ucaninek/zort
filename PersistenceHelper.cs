using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace zort
{
    public static class PersistenceHelper
    {
        public static void AddToStartup(string appName, string appPath)
        {
            // Add the application to the startup registry key
            using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"))
            {
                key.SetValue(appName, appPath);
            }
        }
        public static void RemoveFromStartup(string appName)
        {
            // Remove the application from the startup registry key
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
            {
                key?.DeleteValue(appName, false);
            }
        }

        public static byte[] CreateClone()
        {
            // Create a clone of the application with added arbitrary data to change the hash
            //throw new NotImplementedException();

            return Encoding.UTF8.GetBytes("hella");
        }
    }
}
