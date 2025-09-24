using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace VoiceAssistantMvp.Ai
{
    /// <summary>
    /// Minimal LLM client that supports both:
    ///   - OpenAI-compatible APIs (…/v1/chat/completions)
    ///   - Ollama's native API (…/api/chat)
    ///
    /// Reads connection info from environment variables:
    ///   - OPENAI_BASE_URL (default: https://api.openai.com/v1, or http://localhost:11434 for Ollama)
    ///   - OPENAI_API_KEY  (ignored by Ollama, required by OpenAI)
    ///   - OPENAI_MODEL or OLLAMA_MODEL (model to use; falls back to "gpt-5-turbo")
    /// </summary>
    public class LlmClient
    {
        private readonly HttpClient _http = new();
        private readonly string _baseUrl; // base API URL (OpenAI or Ollama)
        private readonly string _model;   // model name to use

        public LlmClient()
        {
            // Fetch API key from environment
            // - For OpenAI: must be a valid key
            // - For Ollama: can be any non-empty string
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("OPENAI_API_KEY not set.");

            // Add Authorization header ("Bearer ...")
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            // Read base URL (e.g., "https://api.openai.com/v1" or "http://localhost:11434")
            _baseUrl = (Environment.GetEnvironmentVariable("OPENAI_BASE_URL") ?? "https://api.openai.com/v1").TrimEnd('/');

            // Pick model: prefer OPENAI_MODEL, else OLLAMA_MODEL, else fallback
            _model = Environment.GetEnvironmentVariable("OPENAI_MODEL")
                  ?? Environment.GetEnvironmentVariable("OLLAMA_MODEL")
                  ?? "gpt-5-turbo";
        }

        /// <summary>
        /// Send user input to the LLM and get back a reply.
        /// Tries OpenAI-style API first; if that fails, falls back to Ollama's native API.
        /// </summary>
        public async Task<string> ChatAsync(string user, string systemPrompt =
     "You are a concise, helpful voice assistant. Keep answers short and actionable.")
        {
            // --- Try OpenAI-compatible first ---
            var openAiUrl = _baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) ? _baseUrl : _baseUrl + "/v1";
            var openAiBody = new
            {
                model = _model,
                messages = new object[]
                {
            new { role = "system", content = systemPrompt },
            new { role = "user",   content = user }
                }
            };

            var openAiReq = new StringContent(JsonSerializer.Serialize(openAiBody), Encoding.UTF8, "application/json");
            var openAiEndpoint = $"{openAiUrl}/chat/completions";
            var openAiResp = await _http.PostAsync(openAiEndpoint, openAiReq);

            if (openAiResp.IsSuccessStatusCode)
            {
                using var s = await openAiResp.Content.ReadAsStreamAsync();
                using var d = await JsonDocument.ParseAsync(s);
                return d.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()!.Trim();
            }

            // --- Fall back to Ollama native (/api/chat) ---
            var ollamaRoot = _baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
                ? _baseUrl[..^3]  // strip trailing "/v1"
                : _baseUrl;
            ollamaRoot = ollamaRoot.TrimEnd('/');

            var ollamaBody = new
            {
                model = _model,
                messages = new object[]
                {
            new { role = "system", content = systemPrompt },
            new { role = "user",   content = user }
                },
                stream = false
            };

            var ollamaEndpoint = $"{ollamaRoot}/api/chat";
            var ollamaReq = new StringContent(JsonSerializer.Serialize(ollamaBody), Encoding.UTF8, "application/json");
            var ollamaResp = await _http.PostAsync(ollamaEndpoint, ollamaReq);

            // Don't throw: surface detailed error text to the console caller
            var respText = await ollamaResp.Content.ReadAsStringAsync();
            if (!ollamaResp.IsSuccessStatusCode)
            {
                // Common case: 404 model not found
                return $"[LLM error {(int)ollamaResp.StatusCode} @ {ollamaEndpoint}] {respText}";
            }

            using (var s = await ollamaResp.Content.ReadAsStreamAsync())
            using (var d = await JsonDocument.ParseAsync(s))
            {
                if (d.RootElement.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var c))
                    return c.GetString()!.Trim();

                return $"[LLM warning] Unexpected response shape from {ollamaEndpoint}: {respText}";
            }
        }

    }
}
