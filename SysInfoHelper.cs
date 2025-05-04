using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Text.RegularExpressions;
using static NativeMethods;

public class NetworkCard
{
    public string Name { get; set; }
    public string ConnectionName { get; set; }
    public string Status { get; set; }
    public bool DHCPEEnabled { get; set; }
    public List<string> IPAddresses { get; set; } = new List<string>();
}

public class SystemInfo
{
    public string HostName { get; set; }
    public string UserName { get; set; }
    public string OSName { get; set; }
    public string OSVersion { get; set; }
    public string OSConfiguration { get; set; }
    public string OSBuildType { get; set; }
    public string RegisteredOwner { get; set; }
    public string OriginalInstallDate { get; set; }
    public string SystemBootTime { get; set; }
    public string SystemManufacturer { get; set; }
    public string SystemModel { get; set; }
    public string SystemType { get; set; }
    public string Processor { get; set; }
    public string VideoCard { get; set; }
    public string BIOSVersion { get; set; }
    public string BootDevice { get; set; }
    public string SystemLocale { get; set; }
    public string InputLocale { get; set; }
    public string TimeZone { get; set; }
    public long TotalPhysicalMemory { get; set; }
    public string Domain { get; set; }
    public string LogonServer { get; set; }
    public List<NetworkCard> NetworkCards { get; set; } = new List<NetworkCard>();
}
public static class SysInfoHelper
{
    public static SystemInfo Get()
    {
        string output = "";
        var proc = new ProcessStartInfo("cmd.exe", "/c systeminfo")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = @"C:\Windows\System32\"
        };
        Process p = Process.Start(proc);
        p.OutputDataReceived += (sender, args1) => { output += args1.Data + Environment.NewLine; };
        p.BeginOutputReadLine();
        p.WaitForExit();
        return ParseSystemInfo(output);
    }

    public static string HWID()
    {
        return Outbuilt.Fingerprinting.HWID();
    }
    private static SystemInfo ParseSystemInfo(string input)
    {
        var systemInfo = new SystemInfo();
        systemInfo.TotalPhysicalMemory = (long)GetTotalMemory();
        systemInfo.Processor = GetProcessorName();
        systemInfo.VideoCard = GetVideoCardName();

        // Regular expression for matching key-value pairs
        var regex = new Regex(@"^(.*?)\s*:\s*(.*)$", RegexOptions.Multiline);
        var matches = regex.Matches(input);

        foreach (Match match in matches)
        {
            string key = match.Groups[1].Value.Trim();
            string value = match.Groups[2].Value.Trim();

            // Mapping the parsed keys to the properties of SystemInfo
            switch (key)
            {
                case "Host Name":
                    systemInfo.HostName = value;
                    break;
                case "OS Name":
                    systemInfo.OSName = value;
                    break;
                case "OS Version":
                    systemInfo.OSVersion = value;
                    break;
                case "OS Configuration":
                    systemInfo.OSConfiguration = value;
                    break;
                case "OS Build Type":
                    systemInfo.OSBuildType = value;
                    break;
                case "Registered Owner":
                    systemInfo.RegisteredOwner = value;
                    break;
                case "Original Install Date":
                    systemInfo.OriginalInstallDate = value;
                    break;
                case "System Boot Time":
                    systemInfo.SystemBootTime = value;
                    break;
                case "System Manufacturer":
                    systemInfo.SystemManufacturer = value;
                    break;
                case "System Model":
                    systemInfo.SystemModel = value;
                    break;
                case "System Type":
                    systemInfo.SystemType = value;
                    break;
                case "BIOS Version":
                    systemInfo.BIOSVersion = value;
                    break;
                    break;
                case "Boot Device":
                    systemInfo.BootDevice = value;
                    break;
                case "System Locale":
                    systemInfo.SystemLocale = value;
                    break;
                case "Input Locale":
                    systemInfo.InputLocale = value;
                    break;
                case "Time Zone":
                    systemInfo.TimeZone = value;
                    break;
                case "Domain":
                    systemInfo.Domain = value;
                    break;
                case "Logon Server":
                    systemInfo.LogonServer = value;
                    break;
                case "Hotfix(s)":
                    //systemInfo.Hotfixes = ParseHotfixes(input);
                    // No one cares about hotfixes
                    break;
                case "Network Card(s)":
                    systemInfo.NetworkCards = ParseNetworkCards(input);
                    break;
            }
        }

        systemInfo.UserName = Environment.UserName;

        return systemInfo;
    }

    static ulong GetTotalMemory()
    {
        ulong installedMemory = 0;
        MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
        if (GlobalMemoryStatusEx(memStatus))
        {
            installedMemory = memStatus.ullTotalPhys;
        }

        return installedMemory;
    }

    static string GetProcessorName()
    {
        string cpuName = string.Empty;
        using (var searcher = new ManagementObjectSearcher("select Name from Win32_Processor"))
        {
            foreach (var item in searcher.Get())
            {
                cpuName = item["Name"].ToString();
            }
        }
        return cpuName;
    }

    static string GetVideoCardName()
    {
        string gpuName = string.Empty;
        using (var searcher = new ManagementObjectSearcher("select Name from Win32_VideoController"))
        {
            foreach (var item in searcher.Get())
            {
                gpuName = item["Name"].ToString();
            }
        }
        return gpuName;
    }

    static List<NetworkCard> ParseNetworkCards(string input)
    {
        var networkCards = new List<NetworkCard>();
        var regex = new Regex(@"^\[\d+\]:\s*(.*?)(?:\s*Connection Name:\s*(.*?))?\s*(?:Status:\s*(.*?))?\s*IP address\(es\)(.*?)$", RegexOptions.Multiline);

        var matches = regex.Matches(input);

        foreach (Match match in matches)
        {
            var networkCard = new NetworkCard
            {
                Name = match.Groups[1].Value,
                ConnectionName = match.Groups[2].Value,
                Status = match.Groups[3].Value,
            };

            var ipAddresses = Regex.Matches(match.Groups[4].Value, @"\d+\.\d+\.\d+\.\d+|\w+:\w+:\w+");
            foreach (Match ip in ipAddresses)
            {
                networkCard.IPAddresses.Add(ip.Value);
            }

            networkCards.Add(networkCard);
        }

        return networkCards;
    }
}