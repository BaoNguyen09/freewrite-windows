# Freewrite for Windows

An unofficial native Windows port of [Freewrite](https://github.com/farzaa/freewrite), a distraction-free journaling app for dumping thoughts fast.

Website: https://freewrite.thienbao.dev

## Status

Beta. The app is usable locally, but the first public release is unsigned and may trigger Windows SmartScreen.

## Features

- Native Windows desktop app built with WPF and .NET 9
- Local markdown entry storage
- Choose your own storage folder
- History drawer with export/delete
- Dark and light modes
- Font and size controls
- Fullscreen writing mode
- PDF export
- Chat prompt launcher
- Audio dictation and local transcription support when required model/runtime files are present
- Video entry import and playback

## Download

Use the latest GitHub Release once published:

https://github.com/BaoNguyen09/freewrite-windows/releases/latest

The downloadable asset should be named like:

```text
FreewriteWindows-v0.1.0-beta.1-win-x64.zip
```

## Install

1. Download the latest Windows zip from GitHub Releases.
2. Extract it.
3. Run `FreewriteWindows.exe`.
4. If Windows SmartScreen appears, choose **More info** then **Run anyway**.

## Local Data

Entries are stored as markdown files in your selected folder. By default, the app uses:

```text
Documents\Freewrite
```

The app does not upload entries to a server.

## Development

Requirements:

- Windows 10 19041 or newer
- .NET 9 SDK

Build:

```powershell
dotnet build
```

Run smoke tests:

```powershell
dotnet run --project SmokeTests\FreewriteWindows.SmokeTests.csproj
```

Publish local release build:

```powershell
.\scripts\package-release.ps1 -Version 0.1.0-beta.1
```

## Known Limits

- This is an unofficial port, not the official macOS app.
- The executable is not code-signed yet.
- Windows camera, microphone, and transcription behavior depends on local device permissions.
- UI parity is close but not pixel-perfect because the original app is SwiftUI/macOS and this port is WPF/Windows.

## License

MIT. See [LICENSE](LICENSE) and [NOTICE.md](NOTICE.md).
