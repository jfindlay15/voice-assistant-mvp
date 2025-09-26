using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace VoiceAssistantMvp.Services
{
    public static class ChatService
    {
        private static readonly HttpClient Http = new();

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public static async Task<string> SendAsync(string baseUrl, string text, CancellationToken ct = default)
        {
            var url = $"{baseUrl.TrimEnd('/')}/chat";
            var req = new ChatRequest(text);
            using var resp = await Http.PostAsJsonAsync(url, req, JsonOptions, ct);
            resp.EnsureSuccessStatusCode();
            var payload = await resp.Content.ReadFromJsonAsync<ChatResponse>(JsonOptions, ct);
            return payload?.Reply ?? "";
        }

        public record ChatRequest(string Text);
        public record ChatResponse(string Reply);
    }
}
