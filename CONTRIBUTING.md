# Contributing to VoiceToPaste

Thank you for considering a contribution to VoiceToPaste. Keep changes small,
understandable, and consistent with the project's focus on local processing, privacy,
and reliable background operation.

## Before starting

Small bug fixes and focused documentation improvements may be submitted directly as a
pull request.

Open an issue and agree on the direction before implementing:

- a new feature or a behavioral change;
- a user interface or localization architecture change;
- a new dependency or a dependency replacement;
- a large refactor;
- a change to persistent settings or downloaded file formats.

Do not combine unrelated refactors, formatting, or cleanup with the intended change.
Review [AGENTS.md](AGENTS.md) for the current architecture, product behavior, technical
constraints, and testing priorities.

## Development environment

Development requires:

- Windows x64;
- the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0);
- Visual Studio with Windows Forms tooling when a change requires the WinForms
  Designer.

Restore and build the solution from the repository root:

```powershell
dotnet restore VoiceToPaste.slnx
dotnet build VoiceToPaste.slnx --configuration Release
```

## Workflow

1. Create a branch for one focused change.
2. Follow the existing code and naming conventions.
3. Add or update automated tests for behavior that can be tested without extensive UI
   infrastructure.
4. Update Polish and English resources for every user-facing change.
5. Run formatting verification, the Release build, and all tests.
6. Open a pull request that explains the problem, the chosen solution, and any manual
   Windows checks performed.

There is no required branch naming scheme or commit message format. Prefer a small,
reviewable history over unrelated changes grouped into one pull request.

## Coding guidelines

- Use clear English identifiers and prefer straightforward code over clever shortcuts.
- Keep the application in the existing WinForms project unless a separate project has
  a concrete, approved benefit.
- Do not introduce WPF, WinUI, Electron, WebView, MVVM, or a web architecture.
- Avoid abstractions added only for hypothetical future requirements.
- Keep recording, Whisper, and global hotkey logic out of forms.
- Use ordinary block methods with explicit `if` statements and returns for branching
  logic. Do not use nested conditional operators.
- Use expression-bodied members only for simple operations without branching.
- Keep long-running work asynchronous and use `CancellationToken` where cancellation
  is meaningful.
- Use `async void` only for event handlers.
- Dispose native, audio, tray, model, and other owned resources deterministically.
- Verify APIs against the exact dependency versions referenced by the project.
- Do not add a large dependency without prior agreement.
- Comments should explain non-obvious decisions, native layouts, P/Invoke, ownership,
  or lifetime rather than repeat the code.

## Windows Forms and localization

- Configure forms and controls through the Visual Studio WinForms Designer whenever
  possible. Do not manually edit generated `*.Designer.cs` files.
- Define form and control geometry only in the base `Language = (Default)` Designer
  variant.
- Language-specific `*.en.resx` files may translate text but must not override layout
  properties such as `Location`, `Size`, `ClientSize`, `Dock`, `Anchor`, `Margin`, or
  `Padding`.
- Keep static form text in Designer resources and dynamic user-facing text in the
  shared `UiStrings` resources.
- Add both Polish and English values for each new user-facing string.
- Keep UI language independent from Whisper transcription language.
- Do not create extra tray icons or a permanently visible main window.

## Privacy and network access

- Never upload audio, transcription text, telemetry, or logs.
- Do not add cloud transcription or a recording or transcription history.
- Limit network access to model and CUDA downloads explicitly initiated by the user.
- Every downloaded model or runtime archive must have a pinned expected size and
  SHA-256 hash.
- Download to temporary storage, support cancellation, and publish files only after
  complete integrity verification.
- Never log audio or transcription content.

## Verification

Run these commands before opening a pull request:

```powershell
dotnet format VoiceToPaste.slnx --verify-no-changes
dotnet build VoiceToPaste.slnx --configuration Release
dotnet test VoiceToPaste.slnx --configuration Release --no-build
```

Perform relevant manual Windows integration checks when a change affects:

- the system tray or application lifetime;
- the global hotkey;
- microphone capture or playback;
- clipboard and automatic paste behavior;
- UAC or administrator boundaries;
- Windows Task Scheduler autostart;
- WinForms layout or localization;
- native Whisper or CUDA runtime loading;
- model or CUDA downloads.

## Pull request checklist

- [ ] The change is focused and does not include unrelated refactoring.
- [ ] The code is formatted and the Release build succeeds without errors.
- [ ] Existing tests pass, and relevant tests were added or updated.
- [ ] Polish and English resources remain complete and use the same layout.
- [ ] No user data, local settings, logs, models, or build artifacts are included.
- [ ] Relevant manual Windows checks are described in the pull request.
- [ ] Documentation is updated when behavior or requirements changed.

## Licensing contributions

By submitting a contribution, you confirm that you have the right to submit it and
agree to license it under both the
[PolyForm Noncommercial License 1.0.0](LICENSES/PolyForm-Noncommercial-1.0.0.txt) and
the [PolyForm Internal Use License 1.0.0](LICENSES/PolyForm-Internal-Use-1.0.0.txt),
allowing recipients to choose either license under the terms described in this
repository.

You retain copyright in your contribution. Submitting a contribution does not transfer
your copyright to the project owner.
