using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace VoiceAssistantMvp.Audio
{
    /// <summary>
    /// Minimal VAD recorder: auto-stops after sustained silence and returns a WAV path.
    /// Ensures the WAV header is finalized before returning.
    /// </summary>
    public class SimpleVadRecorder
    {
        // Tunables
        public int DeviceNumber { get; set; } = 0;
        public bool DebugMeters { get; set; } = true;
        public int SampleRate { get; set; } = 16000;
        public int Channels { get; set; } = 1;
        public int BufferMs { get; set; } = 50;
        public int SilenceMs { get; set; } = 800;
        public int MinSpeechMs { get; set; } = 200;
        public int MaxDurationMs { get; set; } = 8000;
        public double EnergyThreshold { get; set; } = 0.006;

        private DateTime _lastLog = DateTime.MinValue;

        public async Task<string> RecordAsync(CancellationToken ct = default)
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            string tmp = Path.Combine(Path.GetTempPath(), $"va_{Guid.NewGuid():N}.wav");

            // Validate/select device
            int count = WaveInEvent.DeviceCount;
            if (count <= 0) throw new InvalidOperationException("No input devices found.");
            int dev = DeviceNumber;
            if (dev < 0 || dev >= count) dev = 0;

            var waveIn = new WaveInEvent
            {
                DeviceNumber = dev,
                WaveFormat = new WaveFormat(SampleRate, Channels),
                BufferMilliseconds = BufferMs
            };

            WaveFileWriter? writer = null;
            int silentAccumMs = 0, spokenAccumMs = 0;
            bool startedSpeech = false, stopping = false;

            void Stop()
            {
                if (stopping) return;
                stopping = true;
                try { waveIn.StopRecording(); } catch { /* ignore */ }
                // IMPORTANT: do NOT dispose writer or set result here.
                // Let RecordingStopped finalize the WAV header first.
            }

            using var _reg = ct.Register(Stop);

            waveIn.DataAvailable += (_, args) =>
            {
                writer ??= new WaveFileWriter(tmp, waveIn.WaveFormat);

                // Write raw bytes
                writer.Write(args.Buffer, 0, args.BytesRecorded);

                // Peak + RMS energy
                short peak = 0; long sumSq = 0; int samples = args.BytesRecorded / 2;
                for (int i = 0; i < args.BytesRecorded; i += 2)
                {
                    short s = BitConverter.ToInt16(args.Buffer, i);
                    short a = (short)Math.Abs(s);
                    if (a > peak) peak = a;
                    sumSq += (long)s * s;
                }
                double peakLevel = peak / 32768.0;
                double rms = Math.Sqrt(sumSq / Math.Max(1, samples)) / 32768.0;
                double energy = Math.Max(peakLevel, rms);

                if (DebugMeters && (DateTime.UtcNow - _lastLog).TotalMilliseconds > 250)
                {
                    _lastLog = DateTime.UtcNow;
                    Console.WriteLine($"level: peak={peakLevel:0.000} rms={rms:0.000}");
                }

                if (energy >= EnergyThreshold * 0.8)
                {
                    startedSpeech = true;
                    spokenAccumMs += BufferMs;
                    silentAccumMs = 0;
                }
                else if (startedSpeech)
                {
                    silentAccumMs += BufferMs;
                }

                if (startedSpeech && spokenAccumMs >= MinSpeechMs && silentAccumMs >= SilenceMs)
                    Stop();
            };

            waveIn.RecordingStopped += (_, __) =>
            {
                try
                {
                    if (writer != null)
                    {
                        writer.Dispose(); // finalize header
                        writer = null;
                    }
                    else
                    {
                        // No data ever arrived: create a minimal valid WAV header (zero samples)
                        using var w = new WaveFileWriter(tmp, waveIn.WaveFormat);
                    }
                }
                catch { /* ignore */ }
                finally
                {
                    try { waveIn.Dispose(); } catch { /* ignore */ }
                    tcs.TrySetResult(tmp);
                }
            };

            Console.WriteLine($"[rec] device={dev} sr={SampleRate} ch={Channels} buf={BufferMs}ms meter={(DebugMeters ? "on" : "off")}");
            waveIn.StartRecording();

            // Hard timeout safety
            _ = Task.Delay(MaxDurationMs, ct).ContinueWith(_ => Stop(), TaskScheduler.Default);

            // Wait until RecordingStopped finalized the file
            return await tcs.Task.ConfigureAwait(false);
        }

        // Fixed-length capture (sanity test path)
        public async Task<string> RecordFixedAsync(int milliseconds = 3000, CancellationToken ct = default)
        {
            string tmp = Path.Combine(Path.GetTempPath(), $"va_{Guid.NewGuid():N}.wav");

            int count = WaveInEvent.DeviceCount;
            if (count <= 0) throw new InvalidOperationException("No input devices found.");
            int dev = DeviceNumber;
            if (dev < 0 || dev >= count) dev = 0;

            using var waveIn = new WaveInEvent
            {
                DeviceNumber = dev,
                WaveFormat = new WaveFormat(SampleRate, Channels),
                BufferMilliseconds = BufferMs
            };
            using var writer = new WaveFileWriter(tmp, waveIn.WaveFormat);

            waveIn.DataAvailable += (_, a) => writer.Write(a.Buffer, 0, a.BytesRecorded);
            waveIn.StartRecording();
            await Task.Delay(milliseconds, ct);
            waveIn.StopRecording(); // RecordingStopped will have fired internally
            return tmp;
        }
    }
}
