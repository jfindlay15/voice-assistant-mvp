using System.Media;
using VoiceAssistantMvp.Audio;
using VoiceAssistantMvp.Ai;
using VoiceAssistantMvp.Speech;

//
// Voice Assistant MVP — push-to-talk (toggle)
// Press SPACE to start, press SPACE again to stop. Press ESC to quit.
//

Console.WriteLine("Voice Assistant MVP — push-to-talk (toggle).");
Console.WriteLine("Press [SPACE] to start, press [SPACE] again to stop. Press [ESC] to quit.");

var tts = new LocalTts();
//var ptt = new PushToTalkRecorder();
var vad = new VoiceAssistantMvp.Audio.VadRecorder();

// Resolve the Whisper model path (your .csproj copy rule drops it into bin/.../models/)
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

// ✅ These are the variables that were missing:
var stt = new WhisperStt(modelPath);
var llm = new LlmClient();

while (true)
{
    try
    {
        Console.Write("\nPress SPACE to start talking… ");

        // Records from first SPACE press until the next SPACE press.
        //var wavPath = await ptt.RecordUntilSpaceAgainAsync();
        var wavPath = await vad.RecordWithVadAsync();
        Console.WriteLine("\nProcessing…");

        // Transcribe with Whisper
        var text = await stt.TranscribeAsync(wavPath);
        Console.WriteLine($"> You: {text}");

        // Ask the LLM (Ollama or OpenAI depending on env vars)
        var reply = string.IsNullOrWhiteSpace(text)
            ? "I didn't catch that. Try again a little louder or closer to the microphone."
            : await llm.ChatAsync(text);

        Console.WriteLine($"> Assistant: {reply}");

        // Speak the reply
        tts.Speak(reply);

        // Optional: play back your raw audio once
        try { using var player = new SoundPlayer(wavPath); player.Load(); player.PlaySync(); } catch { /* ignore */ }
        try { File.Delete(wavPath); } catch { /* ignore */ }

        Console.WriteLine("Press ESC to quit, or press SPACE to start again.");
    }
    catch (OperationCanceledException)
    {
        // Thrown by recorder when ESC is pressed
        break;
    }
    catch (InvalidOperationException ex)
    {
        // Too-short recording, etc.
        Console.WriteLine(ex.Message);
        tts.Speak("That was too short. Please try again.");
    }
}

Console.WriteLine("Goodbye.");
