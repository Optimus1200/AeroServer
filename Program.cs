using System.Text;
using Microsoft.AspNetCore.Http.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LocalServer
{
    class Program
    {
        const string LOG_FILEPATH = "ServerEntries.log";
        static readonly object _logLock = new();

        static async Task Main(string[] args)
        {
            // SETTINGS

            const string IP_ADDRESS = "127.0.0.1";
            int[] HTTP_PORTS = { 80, 443 };

            // PROGRAM START

            File.WriteAllText(LOG_FILEPATH, string.Empty);

            var builder = WebApplication.CreateBuilder(args);

            builder.Logging.ClearProviders();

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.AddServerHeader = false;

                foreach (var port in HTTP_PORTS)
                {
                    options.Listen(System.Net.IPAddress.Parse(IP_ADDRESS), port);
                }
            });

            var app = builder.Build();

            app.Use(async (context, next) =>
            {
                await LogRequestAsync(context);
                await next();
            });

            // Method-specific catch-all routes.
            // GET  -> aircraft data payload
            // POST -> aircraft data payload (same envelope; PS3 client expects
            //         "data" field and crashes with a null deref otherwise)
            app.MapGet("/{**catchAll}", HandleHttpGetRequest);
            app.MapPost("/{**catchAll}", HandleHttpPostRequest);

            foreach (var port in HTTP_PORTS)
            {
                Log($"[HTTP {IP_ADDRESS}:{port}] Started listening.");
            }
            Log("All listeners started.\n");

            await app.RunAsync();

            Console.WriteLine("Server offline. Press any key to exit...");
            Console.ReadKey();
        }

        static async Task LogRequestAsync(HttpContext context)
        {
            var request = context.Request;
            var local = context.Connection.LocalIpAddress?.ToString() ?? "?";
            var localPort = context.Connection.LocalPort;

            // buffer body so request handler can still read
            request.EnableBuffering();

            string body = "";
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

            // build header block similear to original http log
            var sb = new StringBuilder();
            sb.Append($"[{local}:{localPort}] Received:\n\n");
            sb.Append($"{request.Method} {request.GetEncodedPathAndQuery()} {request.Protocol}\n");
            foreach (var header in request.Headers)
            {
                sb.Append($"{header.Key}: {header.Value}\n");
            }

            // format json printing to console
            if (!string.IsNullOrWhiteSpace(body))
            {
                sb.Append('\n');
                try
                {
                    var formatted = JToken.Parse(body).ToString(Formatting.Indented);
                    sb.Append(formatted);
                }
                catch (JsonException)
                {
                    sb.Append(body);
                }
                sb.Append('\n');
            }

            Log(sb.ToString());
        }

        static async Task HandleHttpGetRequest(HttpContext context)
        {
            Console.WriteLine(
                "##########################################################\n" + 
                "# HTTP GET REQUEST CALLED - PLEASE REPORT TO OPTIMUS1200 #\n" +
                "##########################################################\n"
            );

            var response = new JObject
            {
                ["status"] = 0,
                ["data"] = new JObject
                {
                    ["aircraft"] = new JArray()
                }
            };

            await WriteJsonResponseAsync(context, response);
        }

        static async Task HandleHttpPostRequest(HttpContext context)
        {
            var response = new JObject
            {
                ["status"] = 0,
                ["data"] = new JObject
                {
                    ["aircraft"] = new JArray()
                }
            };

            await WriteJsonResponseAsync(context, response);
        }

        static async Task WriteJsonResponseAsync(HttpContext context, JObject body)
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body.ToString(Formatting.None));

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json;charset=utf-8";
            context.Response.ContentLength = bodyBytes.Length;
            context.Response.Headers["Connection"] = "close";

            await context.Response.Body.WriteAsync(bodyBytes, 0, bodyBytes.Length);
        }

        static void Log(string data)
        {
            lock (_logLock)
            {
                Console.WriteLine(data);
                File.AppendAllText(LOG_FILEPATH, data + '\n');
            }
        }
    }
}