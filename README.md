# Voice Assistant MVP (Windows) 🎤

Local, privacy-first assistant that runs on your dev machine:
**Mic → Whisper (STT) → LLM (Ollama/OpenAI) → Windows TTS**.

---

## Features
- **Push-to-talk** (toggle): press **SPACE** to start, **SPACE** to stop.  
- **VAD option**: auto-stop when you’re silent (configurable).  
- **Local STT**: Whisper (`ggml-small.en.bin`) runs offline.  
- **Local LLM**: Ollama (e.g., `llama3.2:3b` or `llama3.1:8b`).  
- **Speech out**: Windows built-in TTS.

---

## Prereqs
- **Windows 10/11**, **.NET SDK 8.0+** (`dotnet --version`)
- **Ollama**:
  ```powershell
  winget install Ollama.Ollama
  ollama serve
  ```
  Pull a model (pick one):
  ```powershell
  ollama pull llama3.2:3b    # smaller, faster on CPU
  # or
  ollama pull llama3.1:8b    # larger, higher quality
  ```
- **Whisper model** (English small): download to your user profile (kept out of git)
  ```powershell
  mkdir "$env:USERPROFILE\.voice-assistant\models" -Force
  curl -L -o "$env:USERPROFILE\.voice-assistant\models\ggml-small.en.bin" ^
    https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.en.bin
  ```

---

## Environment Variables (Ollama local)
Set these in your PowerShell session (or permanently with `setx` then restart terminal):
```powershell
$env:OPENAI_BASE_URL="http://localhost:11434"  # note: no /v1
$env:OPENAI_API_KEY="ollama"                   # any non-empty string
$env:OLLAMA_MODEL="llama3.2:3b"                # or llama3.1:8b
```
Quick sanity check:
```powershell
ollama list
ollama run $env:OLLAMA_MODEL "Say hello"
```

---

## Build & Run
```powershell
cd src/VoiceAssistantMvp
dotnet build
dotnet run
```

**Usage**
- The console shows: “Press [SPACE] to start, press [SPACE] again to stop. Press [ESC] to quit.”
- Speak after you press **SPACE**. Press **SPACE** again to stop (or use VAD mode; see below).
- You’ll see the transcript and hear the assistant’s reply.

---

## Project Layout
```
voice-assistant-mvp/
├─ README.md
├─ models/                          # (optional) if you keep models in-repo; recommended to use user-profile path below
└─ src/
   └─ VoiceAssistantMvp/
      ├─ Ai/
      │  ├─ LlmClient.cs            # OpenAI-compatible + Ollama native fallback
      │  └─ WhisperStt.cs           # local Whisper transcription
      ├─ Audio/
      │  ├─ PushToTalkRecorder.cs   # SPACE to start/SPACE to stop (toggle)
      │  └─ VadRecorder.cs          # auto-stop on silence (VAD)
      ├─ Speech/
      │  └─ LocalTts.cs             # Windows TTS
      ├─ Program.cs                 # main loop
      └─ VoiceAssistantMvp.csproj   # build config
```

---

## Configuration Notes

### 1) Whisper model path (auto-copy at build)
We keep the model **outside the repo** and have MSBuild copy it into the app’s `bin/.../models` on build. Ensure your `.csproj` contains:

```xml
<ItemGroup>
  <!-- Copy Whisper model from user profile into build output -->
  <None Include="$(UserProfile)\.voice-assistant\models\ggml-small.en.bin">
    <Link>models\ggml-small.en.bin</Link>
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

Your code then loads:
```
AppContext.BaseDirectory/models/ggml-small.en.bin
```

### 2) Toggle vs. VAD recorder
- **Default (toggle PTT)** in `Program.cs`:
  ```csharp
  var ptt = new PushToTalkRecorder();
  var wavPath = await ptt.RecordUntilSpaceAgainAsync();
  ```
- **Switch to VAD (auto-stop)**:
  ```csharp
  var vad = new VadRecorder();
  var wavPath = await vad.RecordWithVadAsync();
  ```
**VAD tuning** (in `VadRecorder.cs`):  
`_rmsThresh` (sensitivity), `_silenceHangMs` (silence required to stop), `_maxDurationMs` (hard cap).

### 3) Ollama + API compatibility
`LlmClient` tries **OpenAI-style** `.../v1/chat/completions` first, then falls back to **Ollama native** `.../api/chat`.  
Use `OPENAI_BASE_URL="http://localhost:11434"` (no `/v1`) for Ollama.

---

## Git Hygiene
Add/keep these in `.gitignore`:
```
.vs/
bin/
obj/
out/
Debug/
Release/
publish/
models/*.bin
models/*.gguf
*.user
*.suo
*.log
.DS_Store
Thumbs.db
```
> Large model files are not pushed to GitHub. Keep them in `%USERPROFILE%\.voice-assistant\models\`.

---

## Troubleshooting

**App says `OPENAI_API_KEY not set`.**  
Set in current shell:
```powershell
$env:OPENAI_API_KEY="ollama"
```
(For permanent: `setx OPENAI_API_KEY "ollama"` then reopen terminal.)

**404 from LLM call.**  
- Ensure `OPENAI_BASE_URL="http://localhost:11434"` (no `/v1`).  
- Ensure the model exists: `ollama list` and pull it if missing.

**Whisper “Native Library not found”.**  
Add runtime package:
```powershell
dotnet add package Whisper.net.Runtime
```

**Invalid wave RIFF header / empty audio.**  
Use the **toggle recorder** or the updated VAD recorder (waits for file finalization). Speak for >1s.

**No spoken audio.**  
Make sure `System.Windows.Extensions` is installed (for `SoundPlayer`):
```powershell
dotnet add package System.Windows.Extensions
```
Check your Windows output device and volume.

---

## Roadmap
- Better audio cues (start/stop beeps).
- Multi-turn memory & simple tool use (calendar/email).
- **Dockerization**: containerize sidecars (Ollama; future Python services).  
  *The Windows desktop app itself should stay native; we can containerize back-end services and call them over HTTP.*

---


## Run the Voice Assistant

### 1) Start backend (API + Ollama)
```powershell
# from repo root (where docker-compose.yml lives)
docker compose up -d --build

# one-time: pull the model into the Ollama container
docker compose exec ollama ollama pull llama3.1:8b

# quick checks
Invoke-WebRequest http://localhost:8080/health | % Content
docker compose logs -f api


## Author
John Findlay
