using NAudio.Wave;

namespace VoiceAssistantMvp.Audio
{
    /// <summary>
    /// Start recording on SPACE press. Auto-stops when trailing silence is detected
    /// (or when SPACE is pressed again, or on max duration). 16 kHz mono WAV.
    /// </summary>
    public class VadRecorder
    {
        // Tunables (sane defaults)
        private readonly int _sampleRate = 16000;
        private readonly int _channels = 1;
        private readonly int _bufferMs = 30;          // analysis window ~30ms
        private readonly int _minSpeechMs = 300;      // require at least this much voiced audio
        private readonly int _silenceHangMs = 800;    // stop after this long of silence
        private readonly int _maxDurationMs = 10_000; // hard cap (10s)
        private readonly float _rmsThresh = 0.015f;   // ~ -36 dBFS-ish, tweak if needed

        /// <summary>
        /// Press SPACE once to start. Speak normally. It stops on silence, on SPACE, or at max duration.
        /// Returns finalized WAV path.
        /// </summary>
        public async Task<string> RecordWithVadAsync()
        {
            var output = Path.Combine(Path.GetTempPath(), $"vad_{Guid.NewGuid():N}.wav");

            WaitForSpacePress(); // wait to start

            using var waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(_sampleRate, _channels),
                BufferMilliseconds = _bufferMs
            };
            using var writer = new WaveFileWriter(output, waveIn.WaveFormat);

            int voicedMs = 0;
            int silenceMs = 0;
            int totalMs = 0;

            var tcs = new TaskCompletionSource<bool>();

            waveIn.DataAvailable += (_, a) =>
            {
                // write to file
                writer.Write(a.Buffer, 0, a.BytesRecorded);

                // compute RMS for this buffer
                float rms = ComputeRms16(a.Buffer, a.BytesRecorded);
                bool voiced = rms >= _rmsThresh;

                // update timers
                totalMs += _bufferMs;
                if (voiced) { voicedMs += _bufferMs; silenceMs = 0; }
                else { silenceMs += _bufferMs; }

                // Stop conditions:
                // 1) SPACE pressed again (checked below in the loop)
                // 2) After we've had some speech, a trailing silence hang
                // 3) Hard cap on duration
                if ((voicedMs >= _minSpeechMs && silenceMs >= _silenceHangMs) || totalMs >= _maxDurationMs)
                {
                    waveIn.StopRecording();
                }
            };

            waveIn.RecordingStopped += (_, __) =>
            {
                try { writer.Flush(); }
                finally { tcs.TrySetResult(true); }
            };

            waveIn.StartRecording();

            // Pump a tiny loop so SPACE can force-stop early if desired
            while (!tcs.Task.IsCompleted)
            {
                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Spacebar)
                {
                    waveIn.StopRecording();
                    break;
                }
                await Task.Delay(10);
            }

            await tcs.Task;

            // guard tiny files
            var fi = new FileInfo(output);
            if (!fi.Exists || fi.Length < 1024)
            {
                try { File.Delete(output); } catch { }
                throw new InvalidOperationException("Recording too short or silence only. Please try again.");
            }

            return output;
        }

        private static void WaitForSpacePress()
        {
            while (true)
            {
                var key = Console.ReadKey(intercept: true).Key;
                if (key == ConsoleKey.Spacebar) return;
                if (key == ConsoleKey.Escape) throw new OperationCanceledException("Escape pressed");
            }
        }

        private static float ComputeRms16(byte[] buffer, int bytes)
        {
            // 16-bit PCM, little-endian
            int samples = bytes / 2;
            if (samples == 0) return 0f;

            double sumSq = 0;
            for (int i = 0; i < samples; i++)
            {
                short s = BitConverter.ToInt16(buffer, i * 2);
                double n = s / 32768.0; // normalize to -1..1
                sumSq += n * n;
            }
            return (float)Math.Sqrt(sumSq / samples);
        }
    }
}
