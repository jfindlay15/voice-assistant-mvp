using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", timeUtc = DateTime.UtcNow }));

app.MapPost("/chat", async (ChatRequest req) =>
{
    if (req is null || string.IsNullOrWhiteSpace(req.Text))
        return Results.BadRequest(new { error = "Empty text" });

    var baseUrl = Environment.GetEnvironmentVariable("LLM__BASE_URL") ?? "http://host.docker.internal:11434";
    var apiKey = Environment.GetEnvironmentVariable("LLM__API_KEY") ?? "";
    var model = Environment.GetEnvironmentVariable("LLM__MODEL") ?? "llama3.1:8b";

    using var http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
    if (!string.IsNullOrWhiteSpace(apiKey))
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

    // 1) Try OpenAI-compatible Chat Completions
    try
    {
        var openAiPayload = new
        {
            model,
            messages = new object[]
            {
                new { role = "system", content = "You are a concise, friendly voice assistant. Keep answers under 2 sentences unless asked." },
                new { role = "user",   content = req.Text }
            },

            temperature = 0.7
        };

        using var oaiResp = await http.PostAsJsonAsync("v1/chat/completions", openAiPayload);
        var oaiBody = await oaiResp.Content.ReadAsStringAsync();

        if (oaiResp.IsSuccessStatusCode)
        {
            var oai = JsonSerializer.Deserialize<OpenAIChatResp>(oaiBody, json);
            var reply = oai?.choices?.FirstOrDefault()?.message?.content
                        ?? oai?.choices?.FirstOrDefault()?.text
                        ?? "(no reply)";
            return Results.Ok(new ChatResponse(reply));
        }
        // fall through to native if 404/501/etc.
    }
    catch { /* try native next */ }

    // 2) Fallback to Ollama native /api/chat
    try
    {
        var nativePayload = new
        {
            model,
            messages = new object[]
            {
                new { role = "system", content = "You are a helpful assistant." },
                new { role = "user",   content = req.Text }
            },
            stream = false,
            options = new { temperature = 0.7 }
        };

        using var natResp = await http.PostAsJsonAsync("api/chat", nativePayload);
        var natBody = await natResp.Content.ReadAsStringAsync();
        if (natResp.IsSuccessStatusCode)
        {
            var nat = JsonSerializer.Deserialize<OllamaChatResp>(natBody, json);
            var reply = nat?.message?.content ?? "(no reply)";
            return Results.Ok(new ChatResponse(reply));
        }

        return Results.Problem($"Ollama native failed: {(int)natResp.StatusCode} {natResp.ReasonPhrase}\n{natBody}", statusCode: 502);
    }
    catch (Exception ex)
    {
        return Results.Problem($"LLM call error: {ex.Message}", statusCode: 502);
    }
});


app.Run();

// ---- Models ----
public record ChatRequest(string Text);
public record ChatResponse(string Reply);

// OpenAI-compatible (chat completions)
public class OpenAIChatResp { public Choice[]? choices { get; set; } }
public class Choice { public Msg? message { get; set; } public string? text { get; set; } }
public class Msg { public string? role { get; set; } public string? content { get; set; } }

// Ollama native /api/chat
public class OllamaChatResp { public Msg2? message { get; set; } }
public class Msg2 { public string? role { get; set; } public string? content { get; set; } }

