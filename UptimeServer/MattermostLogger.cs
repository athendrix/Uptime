using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace UptimeServer
{
    public class MattermostLogger
    {
        private static readonly string? Webhook = Environment.GetEnvironmentVariable("WEBHOOK") ?? null;
        public static MattermostLogger? DefaultLogger = Webhook != null ? new MattermostLogger(new Uri(Webhook)) : null;
        HttpClient client;
        public MattermostLogger(Uri webhook)
        {
            client = new HttpClient();
            client.BaseAddress = webhook;
        }
        public async Task SendMessage(string message, string? card = null)
        {
            string strcontent;
            if (card == null)
            {
                strcontent = $"{{\"text\":\"{SanitizeJSON(message)}\"";
            }
            else
            {
                strcontent = $"{{\"text\":\"{SanitizeJSON(message)}\",\"props\": {{\"card\":\"{SanitizeJSON(card)}\"}}}}";
            }
            HttpContent content = new StringContent(strcontent);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
            await client.PostAsync("", content);
        }
        //I could probably improve this, but it should be fine for light use.
        private static string SanitizeJSON(string input)
        {
            return input
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Replace("\\", "\\\\")
                .Replace("\b", "")
                .Replace("\t", "\\t")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n");
        }
    }
}
