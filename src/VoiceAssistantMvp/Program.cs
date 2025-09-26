using System;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using VoiceAssistantMvp.Ai;
using VoiceAssistantMvp.Audio;
using VoiceAssistantMvp.Speech;
using VoiceAssistantMvp.Services; // ChatService

//
// Voice Assistant MVP — auto-stop on silence
// SPACE = start talking, SPACE again = stop early, ESC = quit
//

Console.WriteLine("Voice Assistant MVP — auto-stop on silence.");
Console.WriteLine("Press [SPACE] to start; recording will auto-stop after silence. Press [ESC] to quit.");

// --- List input devices and auto-pick AirPods/Hands-Free if present ---
Console.WriteLine("Input devices:");
int deviceIndex = 1;
for (int i = 0; i < WaveInEvent.DeviceCount; i++)
{
    var c = WaveInEvent.GetCapabilities(i);
    Console.WriteLine($"  [{i}] {c.ProductName} ({c.Channels} ch)");
    if (c.ProductName.Contains("AirPods", StringComparison.OrdinalIgnoreCase) ||
        c.ProductName.Contains("Hands-Free", StringComparison.OrdinalIgnoreCase))
    {
        deviceIndex = i;
    }
}
Console.WriteLine($"Using input device index: {deviceIndex}");

// --- Configure VAD (AirPods prefer 48 kHz) ---
var vad = new SimpleVadRecorder
{
    DeviceNumber = deviceIndex, // chosen above
    SampleRate = 16000,       // AirPods are happiest at 48 kHz; if levels are tiny, try 16000
    Channels = 1,           // if the meter stays flat, try 2
    BufferMs = 50,
    SilenceMs = 900,         // slightly longer for Bluetooth stability
    MinSpeechMs = 200,
    EnergyThreshold = 0.006,       // more sensitive for BT mics; raise to 0.010–0.015 if it never stops
    MaxDurationMs = 8000,
    DebugMeters = true
};

// --- Resolve Whisper model in output (bin/.../models/) ---
string ResolveModelPath()
{
    var outRel = Path.Combine(AppContext.BaseDirectory, "models", "ggml-small.en.bin");
    return File.Exists(outRel) ? outRel : "";
}

var modelPath = ResolveModelPath();
if (string.IsNullOrEmpty(modelPath))
{
    Console.WriteLine("Whisper model not found in output models/. Build should copy it there. Aborting.");
    return;
}

var stt = new WhisperStt(modelPath);
var llm = new LlmClient();
var apiBase = Environment.GetEnvironmentVariable("VA__API_BASE") ?? "http://localhost:8080";

// --- Helper to block until one of the specified keys is pressed ---
static ConsoleKey WaitForKey(params ConsoleKey[] keys)
{
    while (true)
    {
        var key = Console.ReadKey(true).Key;
        foreach (var k in keys)
            if (key == k) return key;
    }
}

while (true)
{
    Console.Write("\nPress SPACE to start; auto-stops on silence. (ESC to quit) ");

    var key = WaitForKey(ConsoleKey.Spacebar, ConsoleKey.Escape);
    if (key == ConsoleKey.Escape) break;

    try
    {
        Console.WriteLine("\nRecording… (speak normally)");

        // Allow SPACE (stop early) or ESC (cancel + exit) WHILE recording
        bool exitRequested = false;
        using var cts = new CancellationTokenSource();
        _ = Task.Run(() =>
        {
            while (!cts.IsCancellationRequested)
            {
                var k = Console.ReadKey(true).Key;
                if (k == ConsoleKey.Spacebar) { cts.Cancel(); break; } // manual stop
                if (k == ConsoleKey.Escape) { exitRequested = true; cts.Cancel(); break; }
            }
        });

        // Auto-stops on silence (or SPACE via the CTS above)
        var wavPath = await vad.RecordAsync(cts.Token);

        Console.WriteLine("Processing…");

        // Transcribe
        var text = await stt.TranscribeAsync(wavPath);

        // Treat “[BLANK_AUDIO]” as empty
        if (string.Equals(text, "[BLANK_AUDIO]", StringComparison.OrdinalIgnoreCase))
            text = "";

        Console.WriteLine($"> You: {text}");

        // Get reply (API first, then local fallback)
        string reply;
        if (string.IsNullOrWhiteSpace(text))
        {
            reply = "I didn't catch that. Try again a little louder or closer to the microphone.";
        }
        else
        {
            try
            {
                reply = await ChatService.SendAsync(apiBase, text);
            }
            catch
            {
                reply = await llm.ChatAsync(text); // fallback to local LLM
            }
        }

        Console.WriteLine($"> Assistant: {reply}");
        new LocalTts().Speak(reply);

        // Optional: play back your raw audio once (for debugging)
        try { using var player = new SoundPlayer(wavPath); player.Load(); player.PlaySync(); } catch { /* ignore */ }
        try { File.Delete(wavPath); } catch { /* ignore */ }

        if (exitRequested) break;
        Console.WriteLine("Press SPACE to talk again, or ESC to quit.");
    }
    catch (OperationCanceledException)
    {
        // ESC during recording
        break;
    }
    catch (InvalidOperationException ex)
    {
        // Too-short recording, etc.
        Console.WriteLine(ex.Message);
        new LocalTts().Speak("That was too short. Please try again.");
    }
}

Console.WriteLine("Goodbye.");
