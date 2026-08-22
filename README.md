# VoiceToPaste

VoiceToPaste is a lightweight Windows application that converts speech to text with a
local Whisper model. It runs primarily in the system tray and uses a configurable
global hotkey to copy the transcription to the clipboard and paste it into the active
application.

Use it to dictate prompts, messages, emails, and other text in applications that
support standard clipboard pasting.

> NOTE\
> VoiceToPaste currently supports CPU processing and NVIDIA GPU acceleration through
> CUDA. AMD GPU acceleration is not supported and is not currently planned unless
> someone contributes and maintains an implementation.

## Features

- Local speech recognition through Whisper.net and whisper.cpp-compatible models.
- Configurable global hotkey operating in toggle mode.
- Automatic clipboard copy followed by `Ctrl + V` into the active application.
- Polish and English user interface.
- Transcription language selected independently from the interface language.
- CPU processing and optional NVIDIA CUDA acceleration.
- Configurable recording timeout and rejection of empty or very quiet recordings.
- Full-phrase keyword replacement rules applied before output.
- System tray operation with optional start minimized behavior.
- Optional current-user autostart through Windows Task Scheduler.

## Download and installation

When a binary release is available, download its archive from
[GitHub Releases](https://github.com/panyann/VoiceToPaste/releases), extract the whole
archive into a dedicated folder, and run `VoiceToPaste.exe`.

VoiceToPaste is portable and does not use an installer. Keep the complete release
output together: the application requires files that are distributed beside the EXE,
including icons, licenses, and native runtime components.

Settings, diagnostic logs, downloaded models, and optional CUDA libraries are stored
beside the executable or in subdirectories below it. The selected folder must therefore
allow the elevated VoiceToPaste process to create and update files.

## First launch

1. Accept the Windows UAC prompt. VoiceToPaste always runs as an administrator.
2. The interface starts in Polish when Windows uses a Polish display language and in
   English otherwise.
3. Open **Settings** and select a Whisper model. No model is selected or downloaded
   automatically.
4. If the model is missing, review the download information and click **Download**.
   VoiceToPaste verifies the expected size and SHA-256 hash before installing it.
5. After a valid model is selected, the application restarts and loads it in the
   background.

The default hotkey is `Ctrl + Shift + Space`, and the default recording timeout is 60 seconds.
Both can be changed in Settings.

## How to use

1. Focus the application into which you want to paste text.
2. Press `Ctrl + Shift + Space` to start recording.
3. Speak, then press `Ctrl + Shift + Space` again to stop recording and start transcription.
4. Keep the intended target active until transcription finishes.
5. VoiceToPaste copies the result to the clipboard and sends `Ctrl + V` to the window
   that is active at that moment.

The same tray icon indicates the current state:

- green — ready;
- red — recording;
- blue — transcribing.

Hotkey activations are ignored while transcription is in progress. Recording also
stops automatically when the configured timeout is reached. If automatic pasting fails
after the clipboard was updated, the text remains available for manual pasting.

A left click on the tray icon opens Settings. Minimizing Settings hides the window to
the tray; closing it with `X` exits VoiceToPaste.

## Interface and transcription languages

The user interface is available in Polish and English. Changing it in Settings saves
the selection and restarts VoiceToPaste automatically.

The transcription language is a separate setting. It supports automatic detection and
the multilingual languages available in whisper.cpp. Changing the transcription
language affects subsequent transcriptions without reloading the model.

## Privacy

- Audio recognition and transcription are performed locally.
- VoiceToPaste does not upload audio, transcription text, telemetry, or logs.
- Recordings remain in memory, and VoiceToPaste does not maintain recording or
  transcription history.
- Network access is limited to model and CUDA downloads explicitly started by the
  user.
- Model and CUDA downloads are verified against pinned sizes and SHA-256 hashes before
  use.

The final text is deliberately placed in the Windows clipboard and pasted into the
active application. Clipboard history, clipboard synchronization, and the behavior of
the target application are controlled by Windows or that application, not by
VoiceToPaste.

## Whisper models and processing backends

VoiceToPaste supports these multilingual Whisper models:

| Model | Approximate download size | General trade-off |
| --- | ---: | --- |
| Tiny | 74 MiB | Fastest, lowest recognition quality |
| Base | 141 MiB | Fast with basic recognition quality |
| Small | 465 MiB | Balance between speed and quality |
| Medium | 1.43 GiB | Higher quality and memory use |
| Large v3 Turbo | 1.51 GiB | High quality and faster than Large v3 |
| Large v3 | 2.88 GiB | Highest quality and greatest hardware requirements |

Models are downloaded on demand from the
[whisper.cpp repository on Hugging Face](https://huggingface.co/ggerganov/whisper.cpp)
and stored in the `LLM` directory beside the executable.

### Supported transcription languages

All models listed above are multilingual. VoiceToPaste supports automatic language
detection or explicit selection of any of the 100 transcription languages provided by
whisper.cpp. These include Polish, English, German, French, Spanish, Italian,
Portuguese, Ukrainian, Russian, Czech, Slovak, Chinese, Japanese, Korean, and many
others. The user interface language does not limit the available transcription
languages.

The complete list used by the application is defined in
[`TranscriptionLanguages.cs`](VoiceToPaste/Models/TranscriptionLanguages.cs).
Recognition quality varies by language, selected model, microphone quality, and
recording conditions.

Available processing backends:

- **Auto** — attempts NVIDIA CUDA and can fall back to CPU.
- **GPU — NVIDIA CUDA** — prefers CUDA and can fall back to CPU if it cannot be loaded.
- **CPU** — does not attempt to use CUDA.

Changing the selected model or backend requires a restart because the native runtime
and model are initialized once per process. CUDA support is optional and its required
libraries are downloaded only after confirmation.

## Administrator privileges

VoiceToPaste declares `requireAdministrator` in its manifest. Windows displays a UAC
prompt when the application is started manually. An unsigned build may be identified as
coming from an unknown publisher.

Elevation allows automatic paste to work when the target application also runs as an
administrator, such as an elevated terminal or administrative tool.

## Start with Windows

The **Start with Windows** option creates the `VoiceToPaste.Autostart` task for the
current user in Windows Task Scheduler. It starts the configured executable at sign-in
with the highest privileges and without an additional interactive UAC prompt.

When autostart is enabled, VoiceToPaste validates the task definition during startup.
A missing or outdated task, for example after moving the application, can be repaired
after confirmation.

> Disable **Start with Windows** before moving or deleting VoiceToPaste so its task is
> removed.

The application can remain in any suitable portable folder. Because the autostart task
launches the configured path with elevated privileges, anything able to replace that
EXE could also be launched elevated. Choose and protect the folder according to your
environment.

## Requirements

- Windows x64.
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0).
- Permission to accept the UAC prompt.
- A microphone available as the default Windows recording device.
- A supported Whisper model selected and downloaded before transcription.
- Enough storage and memory for the selected model.
- Internet access only when downloading a model or optional CUDA libraries.

An NVIDIA GPU is optional; CPU transcription is supported.

## Build from source

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) on Windows,
then run from the repository root:

```powershell
dotnet restore VoiceToPaste.slnx
dotnet build VoiceToPaste.slnx --configuration Release
dotnet test VoiceToPaste.slnx --configuration Release
dotnet run --project VoiceToPaste/VoiceToPaste.csproj
```

Create the Windows x64 Release output with:

```powershell
dotnet publish VoiceToPaste/VoiceToPaste.csproj --configuration Release
```

Distribute the complete publish output rather than the EXE alone. It contains both UI
languages and all required external resources and native runtime files.

See [CONTRIBUTING.md](CONTRIBUTING.md) before submitting a change.

## Licensing

Copyright © 2026 panyann.

You may choose either license included in this repository:

- [PolyForm Noncommercial License 1.0.0](LICENSES/PolyForm-Noncommercial-1.0.0.txt)
  permits personal and noncommercial use, including free public forks and
  redistribution.
- [PolyForm Internal Use License 1.0.0](LICENSES/PolyForm-Internal-Use-1.0.0.txt)
  permits internal use and modification, including internal business use, but does
  not permit distribution.

Public forks and redistributed copies must remain noncommercial and retain the
required notice and applicable license. VoiceToPaste may not be sold, offered as a
paid service, or incorporated into a product distributed to customers.

Required Notice: Copyright © 2026 panyann. VoiceToPaste is originally available at
https://github.com/panyann/VoiceToPaste.

This summary does not replace the full license texts. Third-party components are
licensed separately; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
