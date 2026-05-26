# Publishing

This is the release checklist for Freewrite for Windows.

## First-Time GitHub Setup

1. Create a public GitHub repository named `freewrite-windows`.
2. Add this local repo as the remote:

```powershell
git remote add origin https://github.com/BaoNguyen09/freewrite-windows.git
git branch -M main
git push -u origin main
```

3. Confirm the repo includes:
   - `README.md`
   - `LICENSE`
   - `NOTICE.md`
   - `.github/workflows/release.yml`
   - `.github/workflows/pages.yml`

## GitHub Pages

The site deploys from `site/` through GitHub Actions.

Default URL:

```text
https://baonguyen09.github.io/freewrite-windows/
```

To deploy manually:

1. Open GitHub Actions.
2. Run the `Pages` workflow.
3. Check **Settings -> Pages** for the live URL.

## Local Release Package

```powershell
.\scripts\package-release.ps1 -Version 0.1.0-beta.1
```

Outputs:

```text
release\FreewriteWindows-v0.1.0-beta.1-win-x64.zip
release\FreewriteWindows-v0.1.0-beta.1-win-x64.zip.sha256
```

## GitHub Release

Tag and push:

```powershell
git tag v0.1.0-beta.1
git push origin v0.1.0-beta.1
```

The release workflow creates a draft GitHub Release. Review it, then publish it.

## Manual Release Fallback

If GitHub Actions fails:

1. Run `.\scripts\package-release.ps1 -Version 0.1.0-beta.1`.
2. Open GitHub Releases.
3. Create tag `v0.1.0-beta.1`.
4. Upload the zip and `.sha256` file.
5. Paste release notes from below.

## Release Notes Template

```markdown
## Freewrite Windows v0.1.0-beta.1

First public beta of the unofficial native Windows port of Freewrite.

### Includes
- Local markdown journal entries
- Choose storage folder
- History drawer
- Dark/light mode
- Font and size controls
- Fullscreen writing
- PDF export
- Chat prompt launcher
- Audio dictation/transcription support
- Video entry import/playback

### Known Issues
- Unsigned Windows executable may trigger SmartScreen.
- Camera/microphone/transcription behavior depends on Windows permissions and local model/runtime files.
- UI is close to the macOS app but not pixel-perfect.

### Install
Download `FreewriteWindows-v0.1.0-beta.1-win-x64.zip`, extract it, and run `FreewriteWindows.exe`.
```
