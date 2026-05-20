using DNS.Server;
using Microsoft.AspNetCore.Http.Extensions;
using System.Net;
using System.Text;
using System.Text.Json;

using static AeroServer.Utility;

namespace AeroServer
{
    class Program
    {
        // SETTINGS

        static readonly double VERSION = 1.0;

        static string OS = GetOSString();

        static readonly int[] TCP_PORTS = { 80, 443 };

        static readonly int DNS_PORT = 53;

        //static readonly int[] FORWARDED_TCP_PORTS = { 3478, 3479, 3480, 5223 };

        //static readonly int[] FORWARDED_UDP_PORTS = { 3074, 3478, 3479 }; // avoid using 3658 for rpcn, use for psn

        static readonly string UNHANDLED_ROUTES_FILEPATH = "UnhandledRoutes.log";

        static readonly string ARROWS_FILEPATH = "arrows.txt";

        static readonly string TSS_FOLDER = "tss/";

        static JsonSerializerOptions jsonOptions = new() { WriteIndented = true };

        static readonly string SERVER_ADDRESS = GetLocalServerIp();

        static readonly string[] DNS_DOMAINS = {
            //"dev-wind.siliconstudio.co.jp",
            //"aci.vs765.nbgi-amnet.jp",
            //"projectaces-newtitle.bngames.net",
            //"acecombat.jp",
            //"gs-sec.ww.np.dl.playstation.net"//,
            //"a0.ww.np.dl.playstation.net"
        };

        static WebApplication server = BuildServer();

        static DnsServer dnsServer = BuildDnsServer();

        // PROGRAM START

        public static async Task Main(string[] args)
        {
            File.WriteAllText(LOG_FILEPATH, string.Empty);
            File.WriteAllText(UNHANDLED_ROUTES_FILEPATH, string.Empty);

            MapRoutes();

            PrintServerHeader();

            Log($"AeroServer {VERSION:F1} - {OS}");

            await CheckTssFiles();

            await RunServer();

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
                    options.Listen(IPAddress.Any, port);
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

            // serve our own TSS files for this game
            for (int i = 0; i < 15; i++)
            {
                server.MapGet($"/tss/np/NPWR04428_00/NPWR04428_00-{i}.tss", context => ServeFileAsync(context)); // what PS3 requests
                server.MapGet($"/tss/sp-int/NPWR04428_00/NPWR04428_00-{i}.tss", context => ServeFileAsync(context)); // what RPCS3 requests
            }

            server.MapGet("/project_eula_en/{**rest}", context => ServeFileAsync(context));
            server.MapGet("/project_events_eula/{**rest}", context => ServeFileAsync(context));

            server.MapPost("/Wind/{**rest}", context => PostWindAsync(context));

            // proxy all other a0.ww.np.dl.playstation.net traffic back to real PSN
            //server.MapGet("/tss/{**rest}", context => ProxyToPsnAsync(context));

            server.MapGet("/{**catchAll}", context => UnhandledRouteAsync(context));
            server.MapPost("/{**catchAll}", context => UnhandledRouteAsync(context));
            server.MapPut("/{**catchAll}", context => UnhandledRouteAsync(context));
            server.MapPatch("/{**catchAll}", context => UnhandledRouteAsync(context));
            server.MapDelete("/{**catchAll}", context => UnhandledRouteAsync(context));
        }

        static async Task CheckTssFiles()
        {
            if (!Directory.Exists(TSS_FOLDER))
            {
                Directory.CreateDirectory(TSS_FOLDER);
            }

            string tssRelativeDirPSN = "tss/np/NPWR04428_00/";
            string tssRelativeDirRPCN = "tss/sp-int/NPWR04428_00/";

            if (!Directory.Exists(tssRelativeDirRPCN))
            {
                Directory.CreateDirectory(tssRelativeDirRPCN);
            }

            HttpClient httpClient = new(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });

            for (int i = 0; i < 15; ++i)
            {
                string tssFile = $"NPWR04428_00-{i}.tss";
                string tssRelativeFilepathPSN = tssRelativeDirPSN + tssFile;
                string tssRelativeFilepathRPCN = tssRelativeDirRPCN + tssFile;

                if (!File.Exists(tssRelativeFilepathPSN))
                {
                    await LogAsync($"File not found: \"{tssRelativeFilepathPSN}\", requesting now...", ConsoleColor.Yellow);

                    await RequestFileAsync(
                        httpClient,
                        //$"https://a0.ww.np.dl.playstation.net/{tssRelativeFilepathPSN}", // source
                        $"http://api.psorg-web-revival.us/{tssRelativeFilepathPSN}", // source
                        tssRelativeFilepathPSN); // dst

                    if (!File.Exists(tssRelativeFilepathRPCN))
                    {
                        await LogAsync($"File not found: \"{tssRelativeFilepathRPCN}\", copying...", ConsoleColor.Yellow);

                        File.Copy(tssRelativeFilepathPSN, tssRelativeFilepathRPCN);

                        await LogAsync($"File copied to \"{tssRelativeFilepathRPCN}\"", ConsoleColor.Green);
                    }
                }
            }
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

            //Log($"[DNS {SERVER_ADDRESS}:{DNS_PORT}] Started listening.");

            Log("\nAll listeners started. Server online.\n");

            try
            {
                await server.RunAsync();
                //await Task.WhenAll( server.RunAsync(), dnsServer.Listen() );
            }
            catch (Exception e)
            {
                await LogAsync($"[FATAL] Server failed to start: {e.Message}", ConsoleColor.Red);
                if (e.InnerException != null)
                    await LogAsync($"[FATAL] Inner: {e.InnerException.Message}", ConsoleColor.Red);
            }
        }

        // TODO - Finish up and implement
        static async Task SaveDataUpload(HttpContext context)
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

                /*
                 case "ev_save_data_upload":
                    SaveDataUpload(context);
                 */

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
                "result": "OK",
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



        static void PrintServerHeader()
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

            // route domain to our server local IP address

            foreach (string domain in DNS_DOMAINS)
            {
                masterFile.AddIPAddressResourceRecord(domain, SERVER_ADDRESS);
            }

            // proxy off Google's DNS (8.8.8.8) for things we don't care about mapping
            // so standard traffic (like PSN logins if they still ping out) won't break
            var dnsServer = new DnsServer(
                new WildcardResolver(SERVER_ADDRESS, masterFile),
                "8.8.8.8"
            );

            return dnsServer;
        }


    }
}
