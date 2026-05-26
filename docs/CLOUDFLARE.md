# Cloudflare Setup

Target domain:

```text
freewrite.thienbao.dev
```

The domain is not connected yet. Use these steps when you are ready.

## Recommended: Cloudflare Pages

1. Push this repo to GitHub.
2. In Cloudflare, open **Workers & Pages**.
3. Create a Pages project.
4. Connect the GitHub repo.
5. Build settings:
   - Framework preset: `None`
   - Build command: leave blank
   - Build output directory: `site`
6. Deploy.
7. Open the Pages project, go to **Custom domains**.
8. Add:

```text
freewrite.thienbao.dev
```

9. Cloudflare will add or ask for a DNS record. Use:

```text
Type: CNAME
Name: freewrite
Target: <your-project>.pages.dev
Proxy: Proxied
```

10. Wait for SSL to become active.

## Download Button

The landing page should link to:

```text
https://github.com/BaoNguyen09/freewrite-windows/releases/latest
```

This avoids re-uploading binaries to Cloudflare and keeps GitHub Releases as the source of truth.

## After First Release

1. Publish GitHub Release.
2. Confirm latest release URL works.
3. Deploy Cloudflare Pages.
4. Visit `https://freewrite.thienbao.dev`.
5. Click download and verify it reaches the latest GitHub Release.
