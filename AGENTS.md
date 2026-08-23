# AGENTS.md

## Project purpose

VoiceToPaste is a lightweight Windows desktop application for local speech-to-text
conversion. It operates primarily from the system tray, uses a configurable global
hotkey, and performs transcription with a local Whisper model. Privacy, simplicity,
and reliable background operation take priority over a large interface or visual
effects.

## Technology and runtime

- C# and .NET 10.
- Windows Forms.
- Windows x64 Release builds.
- NAudio for audio capture.
- Whisper.net and whisper.cpp-compatible GGML models for local transcription.
- Task Scheduler Managed Wrapper for autostart.
- YamlDotNet for settings and Serilog for local diagnostic logs.
- The application always runs with `requireAdministrator` declared in its manifest.
- Use classic Windows APIs through P/Invoke when that is the simplest solution, such
  as global hotkey registration and keyboard input simulation.
- Do not add WPF, WinUI, Electron, WebView, or a web-based architecture.
- Do not change the target framework or add large dependencies without justification.

## Current product behavior

- The application creates exactly one system tray icon.
- The localized tray menu contains Settings and Exit actions.
- A left click on the tray icon opens Settings.
- Minimizing Settings hides it to the tray. Closing Settings with `X` exits the
  application, as does the Exit tray action.
- A named mutex prevents a second instance in the same Windows session from creating
  another tray icon or registering the same hotkey.
- The configurable hotkey works in toggle mode. The first activation starts recording,
  the second stops recording and starts transcription, and activations during
  transcription are ignored.
- The default hotkey is `Ctrl + Shift + Space`; the hotkey can be changed or disabled.
- Audio is captured from the default system recording device as 16 kHz, 16-bit mono
  PCM and remains in memory.
- A configurable timeout automatically stops long recordings. Empty or very quiet
  recordings are not transcribed.
- The result is processed by keyword replacement rules, copied to the clipboard, and
  pasted into the currently active window with `Ctrl + V`. If pasting fails after the
  clipboard was updated, the text remains available in the clipboard.
- One shared Whisper model is used by dictation and the transcription tester for the
  lifetime of the process.
- The application supports Polish and English UI resources. UI language is stored
  independently from transcription language and takes effect after an automatic
  restart.
- Optional autostart is configured for the current user through Windows Task Scheduler
  and runs with the highest privileges.

## Resolved technical decisions

- Audio capture uses NAudio. Keyboard details must not enter audio or transcription
  code.
- Local transcription uses Whisper.net. Supported multilingual models and their
  integrity metadata are defined by `WhisperModelCatalog`.
- Models use GGML `.bin` files and are stored in the `LLM` directory beside the
  executable.
- A missing model is not selected or downloaded automatically. Settings are shown and
  the user must explicitly start the download.
- Model and CUDA downloads use expected file sizes and SHA-256 hashes. Partial or
  unverified files must never replace valid installed files.
- The available backends are Auto, NVIDIA CUDA GPU, and CPU. Native runtime selection
  is fixed after model initialization, so model and backend changes require a restart.
- Transcription language changes apply to subsequent transcriptions without reloading
  the model.
- Dictation output always uses the clipboard followed by automatic paste. There is no
  separate output mode setting.
- The supported activation mode is toggle, not push-to-talk.

## Architecture and responsibilities

Keep the architecture simple and keep recording, Whisper, and global hotkey logic out
of forms.

```text
Program
├── SettingsService / AppSettings
├── UiLanguages / UiStrings / LocalizedExceptionFactory
├── TranscriptionService
│   └── WhisperTranscriber
└── TrayApplicationContext
    ├── SettingsForm
    │   ├── HotkeyCaptureController
    │   ├── TesterForm
    │   ├── KeyWordsForm
    │   ├── WhisperModelDownloadForm
    │   └── CudaRuntimeDownloadForm
    ├── HotkeyService
    ├── AutoStartTaskService
    └── DictationController
        ├── DictationStateMachine
        ├── AudioRecorder
        ├── TranscriptionService
        ├── KeywordReplacementService
        └── TranscriptionOutput

Download infrastructure
├── WhisperModelCatalog
├── WhisperModelDownloadService
├── CudaRuntimeService
└── VerifiedFileDownloader
```

- `Program` owns startup, logging, culture selection, the single-instance mutex, model
  preload, application restart, and final disposal of the shared transcription service.
- `TrayApplicationContext` owns application lifetime, the single tray icon, Settings,
  hotkey registration, autostart repair prompts, and the main dictation controller.
- `DictationController` orchestrates one dictation session, including timeout, silence
  rejection, transcription, keyword replacement, output, and error recovery.
- `DictationStateMachine` contains only valid dictation state transitions.
- `HotkeyService` registers and releases the Win32 global hotkey and reports activation.
- `AudioRecorder` captures audio in memory and supports signal diagnostics and playback
  for the tester. It must not know about hotkeys, UI, or Whisper.
- `TranscriptionService` owns the single shared model and coordinates initialization,
  state, backend selection, transcription, and disposal.
- `WhisperTranscriber` performs low-level local model loading and inference.
- `TranscriptionOutput` writes the final text to the clipboard and sends paste input.
- `SettingsService` repairs, reads, and atomically writes user settings.
- `AutoStartTaskService` creates, validates, repairs, and removes only the
  `VoiceToPaste.Autostart` task.
- `VerifiedFileDownloader` provides streaming download and integrity verification used
  by model and CUDA installers.
- `KeywordReplacementService` applies full-phrase replacement rules after
  transcription and before output.

Keep these elements in the existing WinForms project while the application remains
small. Do not create extra projects, layers, or interfaces solely to satisfy a pattern.

## Dictation state and concurrency

Treat recording and transcription as explicit shared states:

```text
Idle → Recording → Transcribing → Idle
```

- Serialize hotkey activations so start, manual stop, and timeout cannot stop or
  transcribe the same recording twice.
- Do not start another recording while the previous one is recording or transcribing.
- Every error and cancellation path must return the main dictation session to `Idle`.
- Tray state must come from `DictationController.StateChanged`, not independent flags.
- Temporarily suspend main hotkey handling while capturing a new hotkey or while the
  transcription tester is open.
- Keep long-running model loading, recording workflow, downloads, and transcription
  asynchronous so the UI remains responsive.
- Use `CancellationToken` where interruption is meaningful.
- Do not use `async void` outside event handlers.

## Models and backends

- `TranscriptionService` owns one model per process and shares it between dictation and
  the tester.
- Treat `WhisperModelCatalog` as the source of truth for supported model IDs, file
  names, URLs, sizes, and SHA-256 hashes.
- Treat `CudaRuntimeService` as the source of truth for required CUDA archives, files,
  and integrity metadata.
- Auto and GPU modes may fall back to CPU when CUDA cannot be loaded. CPU mode must not
  attempt to load CUDA.
- Do not switch the initialized native backend or model inside the same process.
- Report missing, corrupt, or unloadable models to the user instead of silently
  disabling transcription.

## Downloads and network boundaries

- Never upload audio, transcription text, telemetry, or logs.
- Network access is limited to model and CUDA downloads explicitly initiated by the
  user.
- Inform the user what will be downloaded before starting a large transfer.
- Stream downloads to temporary files and support cancellation.
- Verify expected size and SHA-256 before publishing any downloaded file.
- Do not replace a valid model or runtime with a partial or failed download.
- Clean temporary download and extraction artifacts on success, cancellation, and
  failure whenever possible.

## Settings and persistent files

- Resolve application-owned paths through `ApplicationPaths` so single-file publishing
  uses the executable directory rather than an extraction directory.
- `settings.yaml`, local logs, `LLM` models, icons, and optional CUDA files live beside
  the executable or in subdirectories beneath it.
- Preserve the readable camel-case YAML structure.
- Normalize missing or invalid setting values and retain safe defaults.
- Save settings through a temporary file and atomic replacement so an interrupted
  write does not leave a truncated configuration.
- Keep the Task Scheduler definition, `AppSettings.AutoStart`, and `settings.yaml`
  consistent. Roll back changes when either task or settings persistence fails.
- Log only information needed for diagnosis. Never log audio or transcription content.

## Localization

- Supported UI languages are Polish and English.
- UI language and Whisper transcription language are independent settings.
- Apply the selected culture before creating WinForms controls or background services.
- Changing UI language saves the setting and safely restarts the single instance.
- Keep static form and control properties in WinForms Designer resources.
- Put dynamic user-facing text in the shared `UiStrings` resources.
- Add both Polish and English values for every new user-facing string.
- Keep technical exception details separate from localized UI presentation.
- English form resources may contain translated text but must not override layout or
  geometry such as `Location`, `Size`, `ClientSize`, `Dock`, `Anchor`, `Margin`, or
  `Padding`.

## Windows Forms UI rules

- Keep the UI small, calm, and functional.
- Do not create a permanently visible main window.
- Do not create additional tray icons for recording, transcription, or errors. Change
  the existing icon, tooltip, or show a short message instead.
- Configure controls through WinForms Designer whenever possible. Do not manually edit
  generated `*.Designer.cs` files.
- Set form and control geometry only in the base `Language = (Default)` variant through
  WinForms Designer.
- Prefer standard WinForms controls and layout containers for new layouts rather than
  manual pixel positioning in application code.
- Reuse a form instance or dispose it correctly. Repeatedly showing Settings must not
  create hidden duplicate forms.
- Modal tester, editor, and download forms must release their owned resources.
- Only forms decide when to open windows and dialogs and when to request an application
  restart. Services and workflows return plain results and never hide such UI side
  effects behind delegates.

## Resource lifetime and error handling

- Unregister the global hotkey.
- Stop and dispose audio capture and playback resources.
- Hide and dispose `NotifyIcon`, tray icons, and tray menu resources.
- Dispose the shared Whisper model exactly once when the application exits.
- Cancel pending recording timeout and download work where supported.
- Handle missing or busy microphones, hotkey conflicts, missing models, failed model
  initialization, download failures, transcription failures, and paste failures.
- A background error must not silently terminate the tray process.
- Show localized user-facing errors and keep diagnostic details in local logs.
- Preserve clipboard text when automatic paste fails after a successful clipboard
  update.

## Autostart

- Implement autostart only through `AutoStartTaskService` and Windows Task Scheduler,
  not through the `Run` registry key or Startup folder.
- The task belongs to the current user, runs at logon with the highest privileges, and
  always uses the fixed `VoiceToPaste.Autostart` name.
- Validate the complete expected task definition and offer repair for missing or
  outdated tasks without silently resetting the user's setting.
- Before moving or removing the application, the user should disable autostart so the
  task is removed.
- Recommend a directory protected from modification by non-elevated processes for an
  executable launched by the elevated task.

## Development guidelines

- Prefer simple, readable code over elaborate patterns.
- Do not use MVVM or web architecture in WinForms.
- Use events to communicate application state changes to the UI.
- Inspect existing code and follow its conventions before making changes.
- Verify APIs against the exact library version used by the project.
- Make small, cohesive, reviewable changes.
- Avoid unrelated refactors and abstractions added only for possible future needs.
- Use block methods with explicit `if` statements and returns for branching logic.
- Do not use nested conditional operators.
- Use expression-bodied members only for simple operations without branching.
- Comments should be in English and explain unusual decisions, especially P/Invoke, native structure
  layout, resource ownership, and lifetime. Do not repeat self-explanatory code.
- Keep public GitHub documentation in English.
- After every change, run formatting, build, and relevant tests.
- Do not claim completion when the change could not be compiled or safely verified.

## Testing

Prioritize automated tests for state, settings, localization, validation, integrity
checks, and data processing. Do not build extensive UI test infrastructure for simple
form property assignments.

Never let test convenience dictate production design. If tests would require hidden
delegates or side effects in services, test the clean code through its concrete
dependencies instead.

Important automated coverage includes:

- dictation state transitions and error recovery;
- settings repair, normalization, and serialization;
- hotkey validation and formatting;
- UI resource parity and the absence of language-specific layout overrides;
- shared transcription service lifetime and backend behavior;
- model catalog and verified downloads;
- CUDA file selection and integrity metadata;
- keyword replacement behavior;
- native `INPUT` structure layout and transcription output behavior.

Important manual Windows integration checks include:

- startup creates exactly one tray icon and a second instance exits;
- manual startup requires UAC confirmation;
- autostart creates, validates, repairs, and removes the current-user elevated task;
- Settings can be minimized, hidden, and reopened repeatedly;
- closing Settings with `X` and choosing Exit both release resources and terminate;
- changing UI language safely restarts the application and preserves transcription
  language;
- hotkey registration conflicts are reported clearly;
- recording uses the default microphone and missing or busy input does not crash;
- the UI remains responsive during model loading, downloads, and transcription;
- transcription and output errors return the main session to `Idle`;
- user audio and text are never uploaded.

## Non-goals

Do not implement these features unless explicitly requested:

- microphone selection;
- cloud transcription;
- recording or transcription history;
- user accounts and synchronization;
- a rich text editor;
- automatic updates;
- animations or elaborate screen overlays;
- an installer or Microsoft Store publishing.
