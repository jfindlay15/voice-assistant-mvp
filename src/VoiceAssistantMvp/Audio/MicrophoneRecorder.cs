using NAudio.Wave;

namespace VoiceAssistantMvp.Audio
{
    /// <summary>
    /// Simple microphone recorder using NAudio.
    /// Captures audio from the default system microphone,
    /// saves it as a temporary WAV file, and returns the file path.
    /// </summary>
    public class MicrophoneRecorder
    {
        /// <summary>
        /// Record audio for a fixed number of seconds.
        /// </summary>
        /// <param name="seconds">How long to record, in seconds.</param>
        /// <returns>Path to the saved WAV file.</returns>
        public string RecordSeconds(int seconds)
        {
            // Generate a temp file name for the recording
            var output = Path.Combine(Path.GetTempPath(), $"rec_{Guid.NewGuid():N}.wav");

            // Configure the microphone capture
            using var waveIn = new WaveInEvent
            {
                // Use 16 kHz, mono — standard format for speech recognition
                WaveFormat = new WaveFormat(16000, 1)
            };

            // Set up a writer to store captured audio in a WAV file
            using var writer = new WaveFileWriter(output, waveIn.WaveFormat);

            // When audio data is available, write it to the WAV file
            waveIn.DataAvailable += (s, a) => writer.Write(a.Buffer, 0, a.BytesRecorded);

            // Start recording
            waveIn.StartRecording();

            // Wait for the desired duration
            Thread.Sleep(TimeSpan.FromSeconds(seconds));

            // Stop recording
            waveIn.StopRecording();

            // Return the path of the saved file
            return output;
        }
    }
}
