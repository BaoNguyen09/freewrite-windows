# Cloudflare Setup

Target domain:

```text
freewrite.thienbao.dev
```

The domain is not connected yet. Use these steps when you are ready.

## GitHub Pages First

This repo deploys `site/` with GitHub Pages through `.github/workflows/pages.yml`.

Default GitHub Pages URL:

```text
https://baonguyen09.github.io/freewrite-windows/
```

Use that URL to verify the website before connecting the custom domain.

## Connect Cloudflare DNS

1. In the GitHub repo, open **Settings -> Pages**.
2. Under **Custom domain**, add:

```text
freewrite.thienbao.dev
```

3. In Cloudflare DNS for `thienbao.dev`, add:

```text
Type: CNAME
Name: freewrite
Target: baonguyen09.github.io
Proxy: DNS only
```

4. Wait for GitHub Pages to verify the domain.
5. Enable **Enforce HTTPS** in GitHub Pages when available.
6. After HTTPS is active, keep DNS-only unless you intentionally want Cloudflare proxy behavior.

## Download Button

The landing page should link to:

```text
https://github.com/BaoNguyen09/freewrite-windows/releases/latest
```

This avoids re-uploading binaries to Cloudflare and keeps GitHub Releases as the source of truth.

## After DNS Is Connected

1. Visit `https://freewrite.thienbao.dev`.
2. Click download and verify it reaches the latest GitHub Release.
3. If GitHub says the domain is already taken, remove stale Pages custom-domain settings from any other repo first.
