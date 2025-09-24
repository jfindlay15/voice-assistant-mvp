using Whisper.net;
using Whisper.net.Ggml;

namespace VoiceAssistantMvp.Ai
{
    /// <summary>
    /// Wraps local Whisper inference to transcribe short WAV files (16kHz mono ideal).
    /// Requires a GGML model file on disk (e.g., models/ggml-small.en.bin).
    /// </summary>
    public class WhisperStt
    {
        private readonly string _modelPath;

        /// <param name="modelPath">Full path to a ggml*.bin model file.</param>
        public WhisperStt(string modelPath)
        {
            _modelPath = modelPath;
        }

        /// <summary>
        /// Transcribe the given WAV file to text using Whisper locally.
        /// </summary>
        public async Task<string> TranscribeAsync(string wavPath)
        {
            using var factory = WhisperFactory.FromPath(_modelPath);
            using var processor = factory.CreateBuilder()
                .WithLanguage("en")
                .Build();

            using var audio = File.OpenRead(wavPath);

            var sb = new System.Text.StringBuilder();

            // ProcessAsync returns a stream of segments. We collect them.
            await foreach (var segment in processor.ProcessAsync(audio))
            {
                // segment.Text already includes leading spaces/punctuation as needed.
                sb.Append(segment.Text);
            }

            return sb.ToString().Trim();
        }
    }
}
