using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace VoiceAssistantMvp.Ai
{
    /// <summary>
    /// Minimal client for OpenAI-compatible chat APIs.
    /// Works with both:
    ///   - OpenAI cloud (https://api.openai.com/v1)
    ///   - Local Ollama (http://localhost:11434/v1)
    /// Reads configuration from environment variables so you can switch backends
    /// without changing code.
    /// </summary>
    public class LlmClient
    {
        private readonly HttpClient _http = new();
        private readonly string _baseUrl; // API endpoint base (OpenAI or Ollama)
        private readonly string _model;   // Which model to use

        public LlmClient()
        {
            // Read API key from environment variables
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

            // Fail early if no key is set (Ollama can use any non-empty string)
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("OPENAI_API_KEY not set.");

            // Add the key to HTTP headers (Ollama ignores it, OpenAI requires it)
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            // Read base URL (default = OpenAI cloud, override = Ollama local server)
            _baseUrl = (Environment.GetEnvironmentVariable("OPENAI_BASE_URL")
                        ?? "https://api.openai.com/v1").TrimEnd('/');

            // Choose model:
            // - Prefer explicit environment variable (OPENAI_MODEL or OLLAMA_MODEL)
            // - Fall back to "gpt-5-turbo"
            _model = Environment.GetEnvironmentVariable("OPENAI_MODEL")
                  ?? Environment.GetEnvironmentVariable("OLLAMA_MODEL")
                  ?? "gpt-5-turbo";
        }

        /// <summary>
        /// Send a message to the chat model and return its reply.
        /// </summary>
        /// <param name="user">The user’s input text.</param>
        /// <param name="systemPrompt">System role prompt (defaults to “helpful assistant”).</param>
        /// <returns>Assistant’s reply as a string.</returns>
        public async Task<string> ChatAsync(string user, string systemPrompt =
            "You are a concise, helpful voice assistant. Keep answers short and actionable.")
        {
            // Build JSON request body (matches OpenAI/Ollama chat API format)
            var body = new
            {
                model = _model,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user",   content = user }
                }
            };

            var json = JsonSerializer.Serialize(body);

            // POST to the API
            var res = await _http.PostAsync($"{_baseUrl}/chat/completions",
                new StringContent(json, Encoding.UTF8, "application/json"));
            res.EnsureSuccessStatusCode();

            // Parse JSON response
            using var stream = await res.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            // Extract the assistant’s message text
            return doc.RootElement
                      .GetProperty("choices")[0]
                      .GetProperty("message")
                      .GetProperty("content")
                      .GetString()!
                      .Trim();
        }
    }
}
