namespace AeroServer
{
    public class Utility
    {
        public static readonly string LOG_FILEPATH = "Server.log";

        static readonly object _logLock = new();

        public static void Log(string data, ConsoleColor color = ConsoleColor.Gray)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(data);
            Console.ResetColor();

            File.AppendAllText(LOG_FILEPATH, data + '\n');
        }

        public static async Task LogAsync(string data, ConsoleColor color = ConsoleColor.Gray)
        {
            lock (_logLock)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(data);
                Console.ResetColor();
            }

            await File.AppendAllTextAsync(LOG_FILEPATH, data + '\n');
        }
    }
}