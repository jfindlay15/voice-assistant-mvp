using System.Media;
using VoiceAssistantMvp.Audio;
using VoiceAssistantMvp.Ai;
using VoiceAssistantMvp.Speech;

// Mic -> Whisper -> LLM -> TTS
Console.WriteLine("Voice Assistant MVP — end-to-end loop.");

var tts = new LocalTts();
var rec = new MicrophoneRecorder();
var modelPath = Path.Combine(AppContext.BaseDirectory, "models", "ggml-small.en.bin");

// Guard for model presence
if (!File.Exists(modelPath))
{
    Console.WriteLine($"Model not found at: {modelPath}");
    return;
}

var stt = new WhisperStt(modelPath);
var llm = new LlmClient();

tts.Speak("Hold on. I will record for three seconds.");
var wavPath = rec.RecordSeconds(3);

var text = await stt.TranscribeAsync(wavPath);
Console.WriteLine($"> You: {text}");

string reply;
if (string.IsNullOrWhiteSpace(text))
{
    reply = "I didn't catch anything. Please try again a little louder.";
}
else
{
    reply = await llm.ChatAsync(text);
}
Console.WriteLine($"> Assistant: {reply}");

tts.Speak(reply);

// optional: hear your original audio
using (var player = new SoundPlayer(wavPath)) { player.Load(); player.PlaySync(); }
try { File.Delete(wavPath); } catch { }
Console.WriteLine("Done.");
