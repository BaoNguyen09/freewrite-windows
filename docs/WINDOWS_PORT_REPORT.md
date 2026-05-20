# Freewrite Windows Port Report

Source inspected: `farzaa/freewrite` on `main`, commit `c21d71c` (`Fix TextEditor cursor position and window dragging on macOS Tahoe, persist video volume`).

## Implemented

- Native Windows desktop app using WPF/.NET 9.
- Same local-first entry format:
  - Markdown entries in `[UUID]-[yyyy-MM-dd-HH-mm-ss].md`.
  - Video metadata as matching `.md` file with `Video Entry`.
  - Video directory layout: `Videos/[UUID]-[timestamp]/[UUID]-[timestamp].mov`.
  - Transcript path: `Videos/[UUID]-[timestamp]/transcript.md`.
- Startup behavior from the macOS source:
  - Create welcome entry for first run.
  - Create a new text entry if latest entry is video.
  - Create a new entry when no entry exists for today.
  - Prefer today's empty text entry.
- Continuous auto-save.
- History sidebar with newest-first ordering, entry selection, delete, and text PDF export.
- Folder selection so the user can choose the Freewrite storage root.
- Compatible default root: `%USERPROFILE%\Documents\Freewrite`.
- Font size cycling, Lato/Arial/System/Serif/random font buttons.
- 15-minute timer with click start/pause, double-click reset, mouse wheel 5-minute adjustment, and fade-out bottom nav.
- Backspace/delete suppression toggle.
- Light/dark theme preference.
- ChatGPT/Claude/copy-prompt flow using the same prompt text from the Swift source.
- Video playback through WPF `MediaElement`, including loop-on-end.
- Video creation through the Windows built-in camera UI, plus manual video import as fallback.
- Best-effort Windows continuous dictation capture during camera recording, saved to `transcript.md` when Windows returns recognized text.

## Not Fully Identical

### Video Recording UI

macOS Freewrite records video in-app through `AVFoundation` and requires camera, microphone, and Apple Speech permissions before showing an immersive recorder.

The Windows port records through `CameraCaptureUI`, which launches the built-in Windows camera capture UI and then imports the resulting file into the same Freewrite storage layout. This is a real Windows-native recording path, but it is not identical UX because the preview/recording controls are hosted by Windows instead of inside Freewrite's edge-to-edge overlay.

Windows-native path to reach exact in-app parity:

- Use Windows `MediaCapture` for camera and microphone capture.
- Use Windows speech recognition APIs for continuous dictation/transcript capture.
- Save into the existing `.mov`-named per-entry folder layout.
- Add a WPF camera preview host.

This is not impossible in first principles; it is an implementation gap, not an OS impossibility.

### Speech Transcription

The macOS app transcribes during recording with Apple's Speech framework and saves `transcript.md`. The Windows app now starts Windows continuous dictation while the Windows camera UI is open and saves recognized text as `transcript.md` when available. This is best-effort because Windows camera capture and Windows speech recognition can both need microphone access. If Windows cannot run both at once, recording still proceeds and the entry is saved without a generated transcript. The app also preserves and reads `transcript.md` when present, and imports a sidecar transcript if a same-named `.md` exists next to an imported video.

### Video Codecs

macOS AVKit handles the `.mov` files created by the original app. WPF `MediaElement` depends on installed Windows media codecs. If Windows cannot decode a selected video, the app shows a codec error. For best compatibility with original Freewrite data, use `.mov` files produced by Freewrite or Windows-supported H.264/AAC files.

### PDF Export

The original uses PDFKit/CoreText. The Windows port writes a simple built-in PDF without external packages. It preserves text content but does not fully match the original font rendering, pagination, or Unicode typography.

## Current Binary Build

Run:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

Output:

```text
bin\Release\net9.0-windows10.0.19041.0\win-x64\publish\FreewriteWindows.exe
```

This binary requires the .NET 9 Windows Desktop Runtime, which is present on this machine.
