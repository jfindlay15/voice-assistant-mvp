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
            //
            // --- Attempt 1: OpenAI-compatible API (/v1/chat/completions) ---
            //
            // Ensure we have a URL that ends with /v1
            var openAiUrl = _baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
                ? _baseUrl
                : _baseUrl + "/v1";

            // Request body in OpenAI format
            var openAiBody = new
            {
                model = _model,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user",   content = user }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(openAiBody), Encoding.UTF8, "application/json");
            var openAiResp = await _http.PostAsync($"{openAiUrl}/chat/completions", content);

            if (openAiResp.IsSuccessStatusCode)
            {
                // Parse OpenAI-style JSON
                using var stream = await openAiResp.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);
                return doc.RootElement.GetProperty("choices")[0]
                          .GetProperty("message").GetProperty("content").GetString()!.Trim();
            }

            //
            // --- Attempt 2: Ollama native API (/api/chat) ---
            //
            var ollamaUrl = _baseUrl.TrimEnd('/');

            // Request body in Ollama format
            var ollamaBody = new
            {
                model = _model,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user",   content = user }
                },
                stream = false // we want the whole reply at once
            };

            var ollamaResp = await _http.PostAsync($"{ollamaUrl}/api/chat",
                new StringContent(JsonSerializer.Serialize(ollamaBody), Encoding.UTF8, "application/json"));
            ollamaResp.EnsureSuccessStatusCode();

            // Parse Ollama-style JSON
            using (var stream = await ollamaResp.Content.ReadAsStreamAsync())
            using (var doc = await JsonDocument.ParseAsync(stream))
            {
                // Ollama returns { "message": { "role": "assistant", "content": "..." }, ... }
                if (doc.RootElement.TryGetProperty("message", out var msg) &&
                    msg.TryGetProperty("content", out var c))
                {
                    return c.GetString()!.Trim();
                }
            }

            // Fallback if neither API succeeded
            return "I couldn't get a response from the model.";
        }
    }
}
