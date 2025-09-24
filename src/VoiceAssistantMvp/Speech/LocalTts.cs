using System;
using System.IO;
using System.Media;
using System.Threading.Tasks;
using Windows.Media.SpeechSynthesis;   // WinRT Text-to-Speech API
using System.Runtime.InteropServices.WindowsRuntime; // Needed to convert WinRT streams to .NET streams

namespace VoiceAssistantMvp.Speech
{
    /// <summary>
    /// Provides local text-to-speech (TTS) functionality
    /// using Windows' built-in SpeechSynthesizer.
    /// </summary>
    public class LocalTts
    {
        // WinRT SpeechSynthesizer instance
        private readonly SpeechSynthesizer _synth = new();

        /// <summary>
        /// Convenience method to call SpeakAsync synchronously.
        /// </summary>
        public void Speak(string text) => SpeakAsync(text).GetAwaiter().GetResult();

        /// <summary>
        /// Convert input text into speech and play it back synchronously.
        /// </summary>
        public async Task SpeakAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            // Generate speech audio into a WinRT stream
            using var ttsStream = await _synth.SynthesizeTextToStreamAsync(text);

            // Save the audio to a temporary .wav file so we can play it with SoundPlayer
            var wavPath = Path.Combine(Path.GetTempPath(), $"tts_{Guid.NewGuid():N}.wav");

            // Convert the WinRT stream into a regular .NET stream
            using (var netStream = ttsStream.AsStreamForRead())
            using (var file = File.Create(wavPath))
            {
                await netStream.CopyToAsync(file);
            }

            // Play the temporary WAV file synchronously
            using var player = new SoundPlayer(wavPath);
            player.Load();
            player.PlaySync();

            // Delete the temp file afterwards
            try { File.Delete(wavPath); } catch { /* ignore */ }
        }
    }
}
