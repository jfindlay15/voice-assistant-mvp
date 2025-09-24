using NAudio.Wave;

namespace VoiceAssistantMvp.Audio
{
    /// <summary>
    /// Console-friendly push-to-talk:
    /// Press SPACE to start recording, press SPACE again to stop.
    /// Avoids flaky key-up detection with GetAsyncKeyState.
    /// </summary>
    public class PushToTalkRecorder
    {
        /// <summary>
        /// Waits for SPACE, starts recording, then stops on the next SPACE.
        /// Returns the finalized WAV path.
        /// </summary>
        public async Task<string> RecordUntilSpaceAgainAsync()
        {
            var output = Path.Combine(Path.GetTempPath(), $"ptt_{Guid.NewGuid():N}.wav");

            // 1) Wait for the first SPACE press to start
            WaitForSpacePress();

            using var waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 1),
                BufferMilliseconds = 50
            };
            using var writer = new WaveFileWriter(output, waveIn.WaveFormat);

            waveIn.DataAvailable += (_, a) => writer.Write(a.Buffer, 0, a.BytesRecorded);

            var tcs = new TaskCompletionSource<bool>();
            waveIn.RecordingStopped += (_, __) =>
            {
                try { writer.Flush(); }
                finally { tcs.TrySetResult(true); }
            };

            waveIn.StartRecording();

            // 2) Debounce so we don't stop on the same key press
            await Task.Delay(150);

            // 3) Run loop: stop when SPACE is pressed again
            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true).Key;
                    if (key == ConsoleKey.Spacebar) break;       // stop recording
                    if (key == ConsoleKey.Escape)                 // allow ESC to bubble up
                        throw new OperationCanceledException("Escape pressed");
                }
                await Task.Delay(10);
            }

            waveIn.StopRecording();
            await tcs.Task; // ensure WAV header is finalized

            // guard against empty files
            var fi = new FileInfo(output);
            if (!fi.Exists || fi.Length < 1024)
            {
                try { File.Delete(output); } catch { }
                throw new InvalidOperationException("Recording too short. Please speak a bit longer.");
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
    }
}
