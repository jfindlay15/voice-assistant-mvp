using System.Media;
using VoiceAssistantMvp.Audio;
using VoiceAssistantMvp.Speech;

//
// Entry point for the Voice Assistant MVP.
// Right now this program only tests microphone recording and text-to-speech playback.
//
Console.WriteLine("Voice Assistant MVP — mic smoke test.");

// Initialize our Text-to-Speech (TTS) helper
var tts = new LocalTts();

// Use TTS to tell the user what will happen
tts.Speak("I will record for three seconds. Please say something now.");

// Initialize the microphone recorder
var rec = new MicrophoneRecorder();

// Record 3 seconds of audio from the default system microphone.
// The recorder saves the result as a temporary .wav file and returns its path.
var wavPath = rec.RecordSeconds(3);

// Use TTS to announce playback
tts.Speak("Playing back your recording.");

// Playback the recorded .wav file using the built-in SoundPlayer
using var player = new SoundPlayer(wavPath);
player.Load();
player.PlaySync();

// Clean up the temp file
try { File.Delete(wavPath); } catch { /* ignore */ }

Console.WriteLine("Mic test done.");
