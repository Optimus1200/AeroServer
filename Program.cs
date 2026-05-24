using DNS.Server;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using static AeroServer.Utility;

namespace AeroServer
{
    class Program
    {
        // SETTINGS

        static readonly double VERSION = 1.1;

        static string OS = GetOSString();

        static readonly int[] TCP_PORTS = { 80, 443 };

        static readonly int DNS_PORT = 53;

        static readonly string UNHANDLED_ROUTES_FILEPATH = "UnhandledRoutes.log";

        static readonly string ARROWS_FILEPATH = "arrows.txt";

        static readonly string TSS_DIR = "tss/np/NPWR04428_00";

        static readonly string HASH_DIR = "hash";

        static JsonSerializerOptions jsonOptions = new() { WriteIndented = true };

        static readonly string SERVER_ADDRESS = GetLocalServerIp();

        static readonly string[] DNS_DOMAINS = {
            "dev-wind.siliconstudio.co.jp",
            "aci.vs765.nbgi-amnet.jp",
            //"projectaces-newtitle.bngames.net",
            //"acecombat.jp",
            //"gs-sec.ww.np.dl.playstation.net",
            "a0.ww.np.dl.playstation.net" // where tss files are requested
        };

        static readonly string PSN_DOMAIN = "a0.ww.np.dl.playstation.net";

        static WebApplication server;

        static DnsServer dnsServer;

        // FOR PS3: ENABLE
        // FOR RPCS3: DISABLE
        static readonly bool ENABLE_DNS = false;

        // right now used for requesting to download tss files,
        // the hashed versions are stored in hash folder and will be compared for security
        static readonly HttpClient httpClient = new(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        });

        // PROGRAM START

        public static async Task Main(string[] args)
        {
            File.WriteAllText(LOG_FILEPATH, string.Empty);
            File.WriteAllText(UNHANDLED_ROUTES_FILEPATH, string.Empty);
        
            server = BuildServer();
        
            if (ENABLE_DNS)
            {
                dnsServer = BuildDnsServer();
            }
        
            MapRoutes();
        
            PrintServerLogo();
        
            Log($"AeroServer {VERSION:F1} - {OS}");
        
            bool tssStatus = await CheckTssFilesAsync();
            if (tssStatus)
            {
                await RunServer();
            }
                
            Console.Write("All listeners stopped. Server offline. Press any key to exit...");
            Console.ReadKey();
        }

        static string GetOSString()
        {
            if (OperatingSystem.IsWindows())
            {
                return "Windows";
            }
            else if (OperatingSystem.IsLinux())
            {
                return "Linux";
            }

            return "Unknown OS";
        }

        static WebApplication BuildServer()
        {
            var builder = WebApplication.CreateBuilder();

            builder.Logging.ClearProviders();

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.AddServerHeader = false;

                foreach (int port in TCP_PORTS)
                {
                    if (port == 443 && ENABLE_DNS)
                    {
                        options.Listen(IPAddress.Any, port, listenOptions => listenOptions.UseHttps());
                    }
                    else
                    {
                        options.Listen(IPAddress.Any, port);
                    }      
                }
            });

            return builder.Build();
        }

        static void MapRoutes()
        {
            // normalize double slashes before routing
            server.Use(async (context, next) =>
            {
                string? path = context.Request.Path.Value;
                if (path != null && path.Contains("//"))
                {
                    context.Request.Path = path.Replace("//", "/");
                }
                await next();
            });

            // 1. handle host/domain
            // 2. handle request
            server.MapFallback("/{**catchAll}", async (context) =>
            {
                string host = context.Request.Host.Value;

                if (context.Request.Host.Value.Contains(PSN_DOMAIN))
                {
                    if (context.Request.Path.Value != null &&
                        context.Request.Path.Value.EndsWith(".tss", StringComparison.OrdinalIgnoreCase))
                    {
                        await ServeFileAsync(context);
                    }
                    else
                    {
                        // TODO: implement psn proxy
                        // 1. forward request to psn domain
                        // 2. receive response from psn domain
                        // 3. return response back to ps3
                    }
                }
                else
                {
                    string pathBase = context.Request.Path.Value ?? string.Empty;
                    if (pathBase != string.Empty)
                    {
                        pathBase = pathBase.Substring(0, pathBase.IndexOf('/', 1));
                    }     

                    switch (pathBase)
                    {
                        case "/Wind":
                            await PostWindAsync(context);
                            break;

                        case "/tss":
                        case "/project_eula_en":
                        case "/project_events_eula":
                            await ServeFileAsync(context);
                            break;

                        default:
                            await UnhandledRouteAsync(context);
                            break;
                    }
                }
            });
        }

        static async Task<bool> CheckTssFilesAsync()
        {
            if (!Directory.Exists(TSS_DIR))
            {
                Directory.CreateDirectory(TSS_DIR);
            }

            bool areFilesGood = true;

            await LogAsync(string.Empty);

            for (int i = 0; i < 15; ++i)
            {
                string tssFilepath = TSS_DIR + "/" + $"NPWR04428_00-{i}.tss";

                if (!File.Exists(tssFilepath))
                {
                    await LogAsync($"File not found: \"{tssFilepath}\", requesting now...", ConsoleColor.Yellow);

                    await RequestFileAsync(
                        httpClient,
                        //$"https://a0.ww.np.dl.playstation.net/{tssFilepath}", // source
                        $"http://api.psorg-web-revival.us/{tssFilepath}", // source
                        tssFilepath); // dst
                }

                if (!VerifyFileHash(tssFilepath))
                {
                    await LogAsync($"Failed to validate: {tssFilepath}", ConsoleColor.Red);
                    areFilesGood = false;
                }
                else
                {
                    await LogAsync($"Successfully validated: {tssFilepath}", ConsoleColor.Green);
                }
            }

            return areFilesGood;
        }

        static async Task<IResult> RequestFileAsync(HttpClient httpClient, string sourcePath, string dstPath)
        {
            try
            {
                using HttpResponseMessage? response = await httpClient.GetAsync(sourcePath, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                string directory = dstPath.Substring(0, dstPath.LastIndexOf("/"));

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var fileStream = new FileStream(dstPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
                await response.Content.CopyToAsync(fileStream);

                string successMessage = $"File saved to: \"{dstPath}\"";

                await LogAsync(successMessage, ConsoleColor.Green);

                return Results.Ok(successMessage);
            }
            catch (Exception ex)
            {
                string errorMessage = $"Failed to download: {ex.Message}";

                await LogAsync(errorMessage, ConsoleColor.Red);

                return Results.Problem(errorMessage);
            }
        }

        static async Task RunServer()
        {
            Console.WriteLine();

            foreach (int port in TCP_PORTS)
            {
                Log($"[TCP {SERVER_ADDRESS}:{port}] Started listening.");
            }

            if (ENABLE_DNS)
            {
                Log($"[DNS {SERVER_ADDRESS}:{DNS_PORT}] Started listening.");
            }

            Log("\nAll listeners started. Server online.\n");

            try
            {
                if (ENABLE_DNS)
                {
                    await Task.WhenAll(server.RunAsync(), dnsServer.Listen(DNS_PORT, IPAddress.Any));
                }
                else
                {
                    await server.RunAsync();
                }
            }
            catch (Exception e)
            {
                await LogAsync($"[FATAL] Server failed to start: {e.Message}", ConsoleColor.Red);
                if (e.InnerException != null)
                    await LogAsync($"[FATAL] Inner: {e.InnerException.Message}", ConsoleColor.Red);
            }
        }

        // TODO - Finish up and implement
        static async Task PostWindSaveDataUploadAsync(HttpContext context)
        {
            string content = string.Empty;

            using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true))
            {
                content = await reader.ReadToEndAsync();
            }

            using JsonDocument doc = JsonDocument.Parse(content);

            JsonElement root = doc.RootElement;

            int slot = 0;
            string data = string.Empty;
            string uid = root.GetProperty("uid").GetString() ?? string.Empty;

            if (root.TryGetProperty("ev_save_data_upload", out JsonElement ev))
            {
                if (ev.TryGetProperty("slot", out JsonElement slotElement) &&
                    slotElement.ValueKind == JsonValueKind.Number)
                {
                    slot = slotElement.GetInt32();
                }

                if (ev.TryGetProperty("data", out JsonElement dataElement) &&
                    dataElement.ValueKind == JsonValueKind.String)
                {
                    data = dataElement.GetString() ?? string.Empty;
                }

                await LogAsync($"ev_save_data_upload.slot = {slot}");
                await LogAsync($"ev_save_data_upload.data length = {data.Length}");
            }

            string savePath = Path.Combine(Directory.GetCurrentDirectory(), $"Wind{uid}", "saves", slot.ToString());

            Directory.CreateDirectory(savePath);

            await File.WriteAllTextAsync(Path.Combine(savePath, "save.bin"), data);

            var response =
                """
                {
                    "result": "OK"
                }
                """;

            await LogResponseAsync(context, response);
        }

        static async Task UnhandledRouteAsync(HttpContext context)
        {
            string route = context.Request.Path.Value ?? string.Empty;

            await LogAsync($"[UNHANDLED {context.Request.Method}] Route not implemented: {route}\n", ConsoleColor.Yellow);

            await File.AppendAllTextAsync(UNHANDLED_ROUTES_FILEPATH, route + "\n");

            // user version - don't crash on unhandled routes

            await StubResponseAsync(context);
            return;

            // dev version - crash on handled routes

            context.Response.Clear();

            context.Response.StatusCode = 501;
            context.Response.ContentType = "text/plain; charset=utf-8";
            context.Response.Headers.Connection = "close";

            await context.Response.WriteAsync($"Not implemented: {route}");
        }

        static async Task ServeFileAsync(HttpContext context)
        {
            await LogRequestAsync(context);
            string? route = context.Request.Path.Value;
            string? filepath = route?.TrimStart('/');

            if (filepath != null && filepath.Contains(".tss"))
            {
                filepath = filepath.Replace("sp-int", "np");
            }

            if (!File.Exists(filepath))
            {
                string response = $"File not found: {filepath}";

                context.Response.StatusCode = 404;
                await context.Response.WriteAsync(response);
                await LogAsync(response + "\n", ConsoleColor.Red);
                return;
            }

            // prevent others from requesting unauthorized filepaths
            string fullPath = Path.GetFullPath(filepath);
            string currentDirectory = Directory.GetCurrentDirectory();
            if (!fullPath.StartsWith(currentDirectory))
            {
                context.Response.StatusCode = 403; // forbidden
                return;
            }

           

            byte[] bytes = await File.ReadAllBytesAsync(filepath);

            string ext = Path.GetExtension(filepath).ToLowerInvariant();
            string contentType = ext switch
            {
                ".tss" => "tss",
                ".xml" => "application/xml",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".html" => "text/html",
                _ => "application/octet-stream"
            };

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/octet-stream";
            context.Response.ContentLength = bytes.Length;
            context.Response.Headers.Connection = "close";

            // write pure binary safely to the PS3 without string conversions
            await context.Response.Body.WriteAsync(bytes);

            // Log a placeholder string instead of parsing raw binary data as UTF-8
            await LogResponseAsync(context, $"[Binary Data: {bytes.Length} bytes sent]\n");
        }

        static async Task PostWindAsync(HttpContext context)
        {
            await LogRequestAsync(context);

            if (string.IsNullOrEmpty(context.Request.Path.Value))
            {
                await UnhandledRouteAsync(context);
                return;
            }

            var pathTokens = new Queue<string>(context.Request.Path.Value.Split('/'));

            // discard "" and "Wind"
            pathTokens.Dequeue();
            pathTokens.Dequeue();

            string currentPath = pathTokens.Dequeue();

            switch (currentPath)
            {
                case "authorize":
                    await StubResponseAsync(context);
                    break;

                case "player":
                    await StubPostWindPlayerAsync(context);
                    break;

                case "save":
                    await PostWindSaveAsync(context, pathTokens);
                    break;

                default:
                    await UnhandledRouteAsync(context);
                    break;
            }
        }

        static async Task PostWindSaveAsync(HttpContext context, Queue<string> pathTokens)
        {
            string currentPath = pathTokens.Dequeue(); // extract "save"

            switch (currentPath)
            {
                case "accum_data":
                case "ev_accept_challenge":
                case "ev_challenge_reward":
                case "ev_death":
                case "ev_dev_aircraft":
                case "ev_entitlement_query":
                case "ev_eula_accept":
                case "ev_exit_room":
                case "ev_load_save_error":
                case "ev_load_save_success":
                case "ev_login":
                case "ev_matching_result":
                case "ev_mission_cancel":
                case "ev_mission_result":
                case "ev_objective_end":
                case "ev_objective_retry":
                case "ev_pinger":
                case "ev_room_creation":
                case "ev_sortie":
                case "ev_title_return":
                case "ev_voucher_redemption":
                    await StubResponseAsync(context);
                    break;

                 case "ev_save_data_upload":
                    await PostWindSaveDataUploadAsync(context);
                    break;

                default:
                    await UnhandledRouteAsync(context);
                    break;
            }
        }

        static async Task StubPostWindPlayerAsync(HttpContext context)
        {
            string content = string.Empty;

            using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true))
            {
                content = await reader.ReadToEndAsync();
            }

            using JsonDocument doc = JsonDocument.Parse(content);

            string callerFunction = doc.RootElement.GetProperty("call").ToString();

            string response = string.Empty;

            switch (callerFunction)
            {
                case "getRankingList":
                case "getAnnouncement":
                    response =
                        """
                        {
                            "result": "OK"
                        }
                        """;
                    break;

                case "getNews":
                    response =
                        """
                        {
                            "result": "OK",
                            "data": { 
                                "newsList": []
                            }
                        }
                        """;
                    break;

                case "getRankingRegulation":
                    response =
                        """
                        {
                            "result": "OK",
                            "data": {
                                "regurations": [
                                    {
                                        "ev_id": 1,
                                        "ev_name": "",
                                        "TestRegulation": "",
                                        "long_event_name": "",
                                        "TestRegulationLong": "",
                                        "present_name_str": "",
                                        "PresentName": "",
                                        "ranking_type_name": "",
                                        "RankingTypeName": "",
                                        "mission_name": "",
                                        "MissionName": "",
                                        "max_winner_rank": 999,
                                        "info_begin_time": 100,
                                        "begin_time": 100,
                                        "interim_time": 100,
                                        "end_time": 100,
                                        "result_disp_time": 100,
                                        "receive_reward_time": 100,
                                        "status": 1,
                                        "matching_regulation_id": 1,
                                        "ranking_rule_id": 1,
                                        "target_missions": [],
                                        "target_aircrafts": [],
                                        "use_original_aircraft_ids": true,
                                        "present_items": [],
                                        "url_option": 0
                                    }
                                ]
                            }
                        }
                        """;
                    break;

                case "getRecoveryInfo":
                    response =
                        """
                        {
                            "result": "OK",
                            "data": {
                                "recovery_id": 1
                            }
                        }
                        """;
                    break;

                default:
                    await UnhandledRouteAsync(context);
                    return;
            }

            byte[] bodyBytes = Encoding.UTF8.GetBytes(response);

            context.Response.Clear();
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json;charset=utf-8";
            context.Response.ContentLength = bodyBytes.Length;
            context.Response.Headers.Connection = context.Request.Headers.Connection;

            await context.Response.Body.WriteAsync(bodyBytes);
            await LogResponseAsync(context, response + "\n");
        }

        static string CreateRequestHeader(HttpContext context, string direction)
        {
            var sb = new StringBuilder();

            char borderChar = '#';

            string headerBody = $"[{SERVER_ADDRESS}:{context.Connection.LocalPort}] {direction} [{context.Connection.RemoteIpAddress}:{context.Connection.RemotePort}] (at {DateTime.Now.ToString()})";

            sb.Append(borderChar, headerBody.Length + 4);

            sb.Append("\n" + borderChar);

            sb.Append(' ', headerBody.Length + 2);

            sb.Append(borderChar);

            sb.Append("\n" + borderChar + " " + headerBody + " " + borderChar);

            sb.Append("\n" + borderChar);

            sb.Append(' ', headerBody.Length + 2);

            sb.Append(borderChar + "\n");

            sb.Append(borderChar, headerBody.Length + 4);

            return sb.ToString();
        }

        static async Task LogRequestAsync(HttpContext context)
        {
            var request = context.Request;

            // buffer body so request handler can still read
            request.EnableBuffering();

            string body = string.Empty;

            if (request.ContentLength > 0 || request.Headers.ContainsKey("Transfer-Encoding"))
            {
                using var reader = new StreamReader(
                    request.Body,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: false,
                    leaveOpen: true);

                body = await reader.ReadToEndAsync();

                request.Body.Position = 0;
            }

            // build header block similar to original http log
            var sb = new StringBuilder();

            sb.Append(CreateRequestHeader(context, "<<<<") + "\n\n");

            sb.Append($"{request.Method} {request.GetEncodedPathAndQuery()} {request.Protocol}\n");

            foreach (var header in request.Headers)
            {
                sb.Append($"{header.Key}: {header.Value}\n");
            }

            await LogAsync(sb.ToString());

            // format json printing to console
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    using JsonDocument doc = JsonDocument.Parse(body);
                    await LogAsync(JsonSerializer.Serialize(doc.RootElement, jsonOptions) + "\n");
                }
                catch (JsonException)
                {
                    await LogAsync(body + "\n");
                }
            }
        }

        static async Task StubResponseAsync(HttpContext context)
        {
            var response =
            """
            {
                "result": "OK"
            }
            """;

            byte[] bodyBytes = Encoding.UTF8.GetBytes(response);

            context.Response.Clear();

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json;charset=utf-8";
            context.Response.ContentLength = bodyBytes.Length;
            context.Response.Headers.Connection = context.Request.Headers.Connection;

            await context.Response.Body.WriteAsync(bodyBytes);

            await LogResponseAsync(context, response + "\n");
        }

        static async Task LogResponseAsync(HttpContext context, string bodyString)
        {
            // Simply read the properties; do NOT assign to them or write to the body stream.
            string response =
                CreateRequestHeader(context, ">>>>") + "\n\n" +
                $"""
                Status: {context.Response.StatusCode}
                Content Length: {context.Response.ContentLength}
                Connection: {context.Response.Headers.Connection}

                """ + bodyString;

            await LogAsync(response);
        }

        static void PrintServerLogo()
        {
            if (OperatingSystem.IsWindows())
            {
                // To see console window expand to fit text:
                // Windows -> Settings -> Terminal Settings -> Terminal: Windows Console Host

                int windowWidth = Math.Min(115, Console.LargestWindowWidth);
                int windowHeight = Math.Min(69, Console.LargestWindowHeight);

                //Console.SetBufferSize(windowWidth, windowHeight);
                Console.SetWindowSize(windowWidth, windowHeight);
                Console.SetWindowPosition(0, 0);
                //Console.Clear();
            }

            string[] lines = File.ReadAllLines(ARROWS_FILEPATH);

            Console.WriteLine();

            foreach (string line in lines)
            {
                Console.WriteLine("  " + line);
            }

            Console.Write("\n\n");
        }

        static string GetLocalServerIp()
        {
            string hostName = Dns.GetHostName();

            IPHostEntry ipHostEntry = Dns.GetHostEntry(hostName);

            foreach (var ip in ipHostEntry.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }

            return string.Empty;
        }

        static DnsServer BuildDnsServer()
        {
            var masterFile = new MasterFile();

            // resolve these domains to localhost
            foreach (string domain in DNS_DOMAINS)
            {
                masterFile.AddIPAddressResourceRecord(domain, SERVER_ADDRESS);
            }

            var dnsServer = new DnsServer(masterFile, "8.8.8.8");

            //dnsServer.Requested += (sender, e) =>
            //{
            //    string cleanReq = PrettyPrintDns(e.Request.ToString() ?? string.Empty);
            //    Log($"[DNS REQUEST]\n{cleanReq}");
            //};
            //
            //dnsServer.Responded += (sender, e) =>
            //{
            //    string cleanRes = PrettyPrintDns(e.Response.ToString() ?? string.Empty);
            //    Log($"[DNS RESPONSE]\n{cleanRes}");
            //};

            dnsServer.Errored += (sender, e) => Log($"[DNS ERROR] {e.Exception.Message}", ConsoleColor.Red);

            return dnsServer;
        }

        static string PrettyPrintDns(string dnsLogString)
        {
            if (string.IsNullOrWhiteSpace(dnsLogString))
                return string.Empty;

            var sb = new StringBuilder();

            // Basic formatting to break apart the complex string
            string formatted = dnsLogString
                .Replace("{Header=", "{\n  Header = ")
                .Replace("}, ", "\n  },\n  ")
                .Replace(", ", ",\n    ")
                .Replace("=[{", " = [\n    {\n      ")
                .Replace("]}]", "\n    }\n  ]")
                .Replace("]", "\n  ]");

            return formatted + "\n";
        }

        static bool VerifyFileHash(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hashBytes = sha256.ComputeHash(stream);

            byte[] expectedHashBytes = File.ReadAllBytes(HASH_DIR + "/" + filePath.Replace(".tss", ".txt"));

            if (hashBytes.Length != expectedHashBytes.Length)
            {
                return false;
            }

            for (int i = 0; i < hashBytes.Length; i++)
            {
                if (hashBytes[i] != expectedHashBytes[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
