# Archived video capture UI

The original Windows port exposed **Video → Record Video / Choose Video** using `CameraCaptureUI` and an in-app `MediaCapture` fallback.

That flow is **hidden from the bottom nav** as of the Talk release. Existing `.mov` video entries still open and play from history.

Source for the old behavior remains in `MainWindow.xaml.cs` (`RecordVideoWithWindowsCameraAsync`, `ImportVideoFromPath`, `VideoPlayer`, etc.) for reference or re-enable.

New voice entries use **Talk** (`.wav` + local Whisper transcription) instead of video.
