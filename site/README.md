# Freewrite Website

Static landing page for `freewrite.thienbao.dev`.

GitHub Pages deploys this folder through `.github/workflows/pages.yml`.

The download button points to the latest GitHub Release.

## If Edge flags the site as unsafe

The HTML in this repo is static. The unsafe warning usually comes from **Cloudflare proxy settings** or **SmartScreen reputation**, not from the page source.

### 1. Fix Cloudflare DNS (most important)

In Cloudflare for `thienbao.dev`:

1. Open **DNS** and find `freewrite`.
2. Replace proxied A records (`104.21.x`, `172.67.x`) with a single record:
   - **Type:** CNAME
   - **Name:** freewrite
   - **Target:** `baonguyen09.github.io`
   - **Proxy status:** DNS only (grey cloud)
3. Save and wait a few minutes for DNS to propagate.

Grey cloud sends traffic straight to GitHub Pages. Orange cloud (proxied) injects hidden iframe/challenge scripts that Edge SmartScreen can treat as suspicious.

### 2. Turn off aggressive Cloudflare bot features

If you keep the orange cloud on, disable these under **Security**:

- Bot Fight Mode
- Super Bot Fight Mode
- Browser Integrity Check (or set Security Level to Low / Essentially Off)

Also disable **Web Analytics** under Analytics if you do not need it. That adds an extra injected script.

### 3. Enable HTTPS on GitHub Pages

After DNS points to GitHub (grey cloud):

1. Repo **Settings → Pages**
2. Confirm custom domain is `freewrite.thienbao.dev`
3. Wait until the certificate shows as ready (can take up to 24 hours)
4. Enable **Enforce HTTPS**

If the certificate stays stuck on "Certificate not yet created", remove the custom domain, wait one minute, add it again.

### 4. Report a SmartScreen false positive

If Edge still blocks after DNS changes:

1. On the block page, choose **More information**
2. Click **Report that this site doesn't contain threats**
3. Or use [Microsoft's report form](https://www.microsoft.com/en-us/wdsi/support/report-unsafe-site-guest)

Reviews can take a few days.
