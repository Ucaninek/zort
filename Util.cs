using System.Diagnostics;

namespace zort
{
    public static class Util
    {
        public static void RestartComputer()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "shutdown",
                Arguments = "/r /t 0",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                process.WaitForExit();
            }
        }
    }
}