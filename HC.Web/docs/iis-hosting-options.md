# Angular SSR on IIS — can route-driven SSR be hosted on Windows/IIS?

**Question.** *"If I make route-driven SSR, can I host it under Windows hosting, under IIS?"*

**Short answer.** IIS can never run the Angular renderer itself — Angular 19 SSR is a Node.js
program (`dist/hc.web/server/server.mjs`, Express 4). IIS can only do one of two things with it:

1. serve the **build-time output** (`dist/hc.web/browser`) as plain static files, or
2. act as a **reverse proxy / process host** in front of a Node process running on the same box.

Route-driven rendering (`app.routes.server.ts`) is what decides which of those two you need,
per route: `Prerender` and `Client` routes are fully served by option 1, only `RenderMode.Server`
routes need option 2.

This document records the analysis and the two viable deployment shapes for this repository.
It is documentation only — **no build or source configuration has been changed by writing it**;
section 6 lists the exact edits per option.

---

## 1. What "route-driven SSR" actually emits

In Angular 19.2 the per-route rendering decision is a `ServerRoute[]` array
(`src/app/app.routes.server.ts`) provided to the server bootstrap through
`provideServerRouting(...)` from `@angular/ssr`. The three modes and what they cost you:

| Mode | Build output | Runtime requirement | Plain IIS / shared Windows hosting |
|---|---|---|---|
| `RenderMode.Prerender` (SSG) | real HTML per route, e.g. `browser/shop/index.html` | none — static files | ✅ works |
| `RenderMode.Client` (CSR) | nothing; requests get the `index.csr.html` shell | none — static files + SPA rewrite | ✅ works |
| `RenderMode.Server` (SSR per request) | nothing; rendered by `server.mjs` on every hit | **Node.js 18.19+ / 20.11+ / 22** | ❌ impossible |

`RenderMode` values confirmed in `node_modules/@angular/ssr/index.d.ts`:
`Server = 0`, `Client = 1`, `Prerender = 2`.

Two behaviours in the builder worth knowing (verified in `node_modules/@angular/build`):

- `outputMode` (`"static" | "server"`) exists in 19.2 (`builders/application/schema.json`).
  Building with `"outputMode": "static"` and any route left as `RenderMode.Server` is a **build
  error**: *"Route '…' is configured with server render mode, but the build 'outputMode' is set to
  'static'."* (`utils/server-rendering/prerender.js`). That is a useful guard rail, not a problem.
- Setting `outputMode` **overrides** `prerender`: the CLI logs *"The 'prerender' option is not
  considered when 'outputMode' is specified"* and then sets prerendering from the `server` entry
  itself (`builders/application/options.js`). For `"outputMode": "static"` the Express bundle
  (`src/server.ts`) is not emitted at all (`ignoreServer`), while `src/main.server.ts` is still
  compiled because prerendering needs it.
- Whenever `ssr` or `prerender` is configured, the CLI renames the client shell to
  `index.csr.html` (`builders/application/options.js`, `INDEX_HTML_CSR`). `HC.Web/public/web.config`
  already handles both `index.html` and `index.csr.html`, so it is correct for either option below.

### Current state of this repository

| Item | Current value | Consequence |
|---|---|---|
| `angular.json` → `prerender` | `false` | no static HTML is produced |
| `angular.json` → `ssr.entry` | `src/server.ts` | a Node server bundle is produced but never deployed |
| `src/app/app.routes.server.ts` | does not exist | every route falls back to CSR |
| `src/app/app.module.server.ts` | `AppModule + ServerModule`, no providers | no server routing config |
| `dist/hc.web/prerendered-routes.json` | `{ "routes": {} }` | confirms nothing is prerendered |
| `public/web.config` | SPA rewrite to `index.html`, fallback `index.csr.html` | today the storefront is CSR-only on IIS |

In other words: the site is already hosted IIS-only; adding route-driven rendering is what would
put real HTML in front of crawlers for the pages that deserve it.

---

## 2. Option A — IIS only: prerendered HTML + CSR everywhere else (no Node)

**Use this if** the site stays on the current Windows/Plesk hosting, or you do not want a Node
process to keep alive, monitor and pay for.

### 2.1 Add the route table

`HC.Web/src/app/app.routes.server.ts` (new file):

```ts
import { RenderMode, ServerRoute } from '@angular/ssr';

/**
 * Which routes get real HTML at build time, and which are left to the browser.
 *
 * Prerender -> written to dist/hc.web/browser/<route>/index.html at build time. Only for pages
 *              whose content does not depend on the visitor (marketing, policy, landing pages).
 * Client    -> the index.csr.html shell is served and the Angular router takes over. Everything
 *              that is per-customer or per-request must stay here.
 */
export const serverRoutes: ServerRoute[] = [
  { path: '', renderMode: RenderMode.Prerender },
  { path: 'shop', renderMode: RenderMode.Prerender },
  { path: 'privacy-policy', renderMode: RenderMode.Prerender },
  { path: 'terms-of-service', renderMode: RenderMode.Prerender },

  // Catalog and account pages: product/category data changes without a redeploy and
  // checkout/my-orders/profile are behind AuthGuard, so prerendering them would bake in
  // either stale data or a redirect to '/'.
  { path: 'shop/:categoryId', renderMode: RenderMode.Client },
  { path: 'product/:id', renderMode: RenderMode.Client },
  { path: 'cart', renderMode: RenderMode.Client },
  { path: 'checkout', renderMode: RenderMode.Client },
  { path: 'my-orders', renderMode: RenderMode.Client },
  { path: 'favorites', renderMode: RenderMode.Client },
  { path: 'login', renderMode: RenderMode.Client },
  { path: 'register', renderMode: RenderMode.Client },
  { path: 'forgot-password', renderMode: RenderMode.Client },
  { path: 'reset-password/:token', renderMode: RenderMode.Client },
  { path: 'set-password', renderMode: RenderMode.Client },
  { path: 'profile', renderMode: RenderMode.Client },
  { path: 'logout', renderMode: RenderMode.Client },

  // Anything unmatched: client-render (the router redirects '**' to '').
  { path: '**', renderMode: RenderMode.Client },
];
```

A parameterized route can also be prerendered, but it then needs a `getPrerenderParams()` that
enumerates real values at build time (usually by calling the API), e.g. product ids. Not worth it
for a catalog that changes daily — keep them `Client`.

### 2.2 Register the table on the server side

`HC.Web/src/app/app.module.server.ts`:

```ts
import { NgModule } from '@angular/core';
import { ServerModule } from '@angular/platform-server';
import { provideServerRouting } from '@angular/ssr';
import { AppComponent } from './app.component';
import { AppModule } from './app.module';
import { serverRoutes } from './app.routes.server';

@NgModule({
  imports: [AppModule, ServerModule],
  providers: [provideServerRouting(serverRoutes)],
  bootstrap: [AppComponent],
})
export class AppServerModule {}
```

`provideServerRouting` returns `EnvironmentProviders`, which is legal in NgModule `providers`
(this is the NgModule equivalent of putting it in `app.config.server.ts` in a standalone app).

### 2.3 Build for static output

`HC.Web/angular.json` → add `"outputMode": "static"` to the build options, and **remove** the
existing `"prerender": false` (with `outputMode` present it is ignored and logs a warning) and the
`"ssr": { "entry": "src/server.ts" }` block (in static mode the Express bundle is not emitted, so
the entry serves no purpose). Keep `"server": "src/main.server.ts"` — that is what tells the
builder the app can be rendered, and prerendering is derived from it.

```json
"options": {
  "outputPath": "dist/hc.web",
  "index": "src/index.html",
  "browser": "src/main.ts",
  "server": "src/main.server.ts",
  "outputMode": "static",
  "...": "polyfills, assets, styles, tsConfig as they are today"
}
```

`"prerender": true` is then implied — do not also set it.

```powershell
cd HC.Web
npm run build
```

Expected output shape:

```text
dist/hc.web/
  browser/                     <- publish THIS folder to the IIS site root
    index.html                 <- prerendered '/'
    index.csr.html             <- shell for Client routes
    shop/index.html            <- prerendered '/shop'
    privacy-policy/index.html
    terms-of-service/index.html
    main-*.js, styles-*.css, media/, images/, web.config
  prerendered-routes.json      <- now lists the prerendered routes
```

No `server/` folder is deployed, no Node on the host, no `PORT`, no process to babysit.

### 2.4 IIS configuration

The existing `HC.Web/public/web.config` (copied into `browser/` by the `assets` entry in
`angular.json`) already does everything needed:

- `defaultDocument` → `index.html`, then `index.csr.html`
- MIME types for `.json`, `.woff`, `.woff2`, `.webmanifest`
- SPA rewrite for unknown paths to the shell, with `/api/...` excluded
- real files/folders are never rewritten, so prerendered route folders are served as-is

One ordering detail matters once `Client` routes exist alongside prerendered ones: the rule
"Angular Routes" rewrites to `index.html`, which after a prerender build is the **prerendered home
page**, not a neutral shell. So `GET /cart` (a `Client` route, no file on disk) would return the
home page's HTML; Angular then discards it and re-renders, logging hydration mismatches (`NG0500`).
It works, but it is wasteful and noisy in the console.

The clean fix is to serve `index.csr.html` (the real shell) for unmatched URLs: put the
**"Angular Routes (CSR shell)" rule before the "Angular Routes" rule**, or change the first rule's
action to `url="index.csr.html"`. Prerendered routes are unaffected — their folders are real
directories, and the `IsDirectory` condition makes both rules skip them, so IIS serves
`shop/index.html` through `defaultDocument` as intended.

Host requirements:

| Requirement | Why |
|---|---|
| **URL Rewrite 2.1 module** | `web.config` uses `<rewrite>`; without it IIS returns 500.19 |
| App pool `.NET CLR version` = **No Managed Code** | nothing managed runs |
| No write permissions needed | pure static content |
| `.html`, `.json`, `.woff2` served | already declared in `web.config` |

If the host has no URL Rewrite module, prerendered routes still work (they are physical files) but
deep links into `Client` routes would 404. The fallback without URL Rewrite is an `<httpErrors>`
entry that maps 404 to `/index.csr.html`.

### 2.5 Caveats

- Prerendered HTML is frozen at build time. `HomeComponent` loads products/categories/stats in
  `ngOnInit`, so **the build machine must be able to reach `https://serv.homecuties.com/api/`**
  (see `src/environments/environment.prod.ts`). If those numbers must be live, keep the home route
  `Client` or accept a snapshot that is as old as the last deploy.
- Auth-guarded routes must never be `Prerender` — the guard would bake a redirect into the HTML.
- SEO is per-page: `/` and `/shop` get real HTML; `/product/:id` does not unless you later add
  `getPrerenderParams`. If product-page SEO matters more than freshness, prerender only the
  product ids you care about in a scheduled build.
- Hydration is already enabled (`provideClientHydration(withEventReplay())` in `app.module.ts`),
  which is exactly what prerendered HTML needs.
- `RenderMode.Server` must not appear in the table if you build with `outputMode: "static"` — the
  build fails on purpose.

---

## 3. Option B — IIS in front of Node: true per-request SSR (`RenderMode.Server`)

**Use this if** you need live HTML for every request (personalised content, always-fresh catalog)
and you control a Windows VPS/dedicated server.

### 3.1 Prerequisites

| Prerequisite | Note |
|---|---|
| Node.js on the server | Angular 19.2 supports `18.19.1+`, `20.11.1+`, `>=22` |
| IIS (any recent version) | site runs in an app pool set to **No Managed Code** |
| URL Rewrite 2.1 | required by both variants below |
| ARR (Application Request Routing) | only for variant B2; enable *Proxy → Enable proxy* at server level |
| HttpPlatformHandler | only for variant B1 |

Deploy **both** folders, keeping the layout the builder produced:

```text
D:\sites\homecuties\
  server\      <- dist/hc.web/server  (server.mjs, chunks, index.server.html)
  browser\     <- dist/hc.web/browser (static assets)
```

`server.ts` resolves the browser folder as `../browser` relative to `server.mjs`
(`const browserDistFolder = resolve(serverDistFolder, '../browser')`), so the two must stay
siblings. The site's physical path points at `D:\sites\homecuties` (or at `server` if you use
variant B2 with an explicit process path).

### 3.2 Variant B1 — HttpPlatformHandler (IIS starts and supervises Node)

`HC.Web/public/web.config` (or a separate `web.config` at the site root, on the IIS side only):

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <handlers>
      <add name="httpPlatformHandler" path="*" verb="*"
           modules="httpPlatformHandler" resourceType="Unspecified" requireAccess="Script" />
    </handlers>
    <httpPlatform processPath="C:\Program Files\nodejs\node.exe"
                  arguments="&quot;D:\sites\homecuties\server\server.mjs&quot;"
                  stdoutLogEnabled="true"
                  stdoutLogFile=".\logs\node"
                  startupTimeLimit="60">
      <environmentVariables>
        <environmentVariable name="PORT" value="%HTTP_PLATFORM_PORT%" />
        <environmentVariable name="NODE_ENV" value="production" />
      </environmentVariables>
    </httpPlatform>
  </system.webServer>
</configuration>
```

IIS spawns Node, waits for it to listen on the port it assigned (`%HTTP_PLATFORM_PORT%`), forwards
all requests to it, restarts it if it dies, and streams stdout/stderr into `.\logs\node_*.log`.

### 3.3 Variant B2 — Node as a Windows service + ARR reverse proxy

Better for a long-lived app: the process is independent of IIS recycling, and IIS only terminates
TLS. Install the service with [NSSM](https://nssm.cc/) (or WinSW):

```powershell
nssm install HomecutiesWeb "C:\Program Files\nodejs\node.exe" "D:\sites\homecuties\server\server.mjs"
nssm set HomecutiesWeb AppDirectory "D:\sites\homecuties\server"
nssm set HomecutiesWeb AppEnvironmentExtra PORT=4000 NODE_ENV=production
nssm set HomecutiesWeb Start SERVICE_AUTO_START
nssm set HomecutiesWeb AppStdout "D:\sites\homecuties\logs\web.out.log"
nssm set HomecutiesWeb AppStderr "D:\sites\homecuties\logs\web.err.log"
nssm start HomecutiesWeb
```

Then proxy the site to it (`ARR → Server Proxy Settings → Enable proxy` must be on):

```xml
<system.webServer>
  <rewrite>
    <rules>
      <rule name="ReverseProxyToNode" stopProcessing="true">
        <match url="(.*)" />
        <action type="Rewrite" url="http://localhost:4000/{R:1}" />
        <serverVariables>
          <set name="HTTP_X_FORWARDED_PROTO" value="https" />
          <set name="HTTP_X_FORWARDED_HOST" value="{HTTP_HOST}" />
        </serverVariables>
      </rule>
    </rules>
  </rewrite>
</system.webServer>
```

`<serverVariables>` must be unlocked at server level in `applicationHost.config`
(`<rewrite><allowedServerVariables>`), otherwise the rule fails with a config error.

### 3.4 Code changes needed in this repository for option B

These do not apply to option A.

1. **`allowedHosts` — mandatory in production.** Angular 19.2 rejects requests whose `Host` header
   is not allow-listed (SSRF protection) while `localhost` always passes, so the site would work
   locally and return an error through IIS. In `HC.Web/angular.json`:

   ```json
   "ssr": {
     "entry": "src/server.ts",
     "allowedHosts": ["homecuties.com", "www.homecuties.com", "localhost"]
   }
   ```

2. **`server.ts` builds absolute URLs from `req.protocol`.** Behind ARR/HttpPlatformHandler, TLS
   terminates at IIS, so `req.protocol` is `http` and
   `` url: `${protocol}://${headers.host}${originalUrl}` `` produces `http://` URLs inside the
   rendered HTML (canonical tags, `og:url`, any absolute asset URL). Fix with either
   `app.set('trust proxy', 1)` before the routes (Express then honours
   `X-Forwarded-Proto`), or render with a relative URL only.

3. **`express.static` wins over the renderer for prerendered routes.**
   `app.get('**', express.static(browserDistFolder, { index: 'index.html' }))` serves
   `browser/index.html` or `browser/<route>/index.html` when those files exist, so a route that is
   both prerendered and `RenderMode.Server` never reaches `CommonEngine`. Keep the two sets
   disjoint (prerender only what the Node server does not need to render), or pass
   `index: false` if the engine should own `/`.

4. **Keep the folder layout** described in 3.1 — `server/` and `browser/` as siblings.

5. **`server.ts` already respects `PORT`**, so both variants above work without editing it.

### 3.5 Do not use iisnode

`iisnode` is the module most IIS tutorials still mention. Do not use it here:

- unmaintained for years (last release 2017), no support for modern Node;
- needs a CommonJS entry point; Angular 19 emits **ESM** (`server.mjs`), and iisnode's
  `nodeProcessCommandLine` / `interceptor` plumbing does not handle `.mjs` reliably;
- stdout/`process.env` handling differs from `%HTTP_PLATFORM_PORT%`, so `PORT` wiring is fragile.

HttpPlatformHandler or ARR + a service are the supported shapes.

---

## 4. Shared Windows hosting reality check (Plesk / cPanel-style)

Most "Windows hosting" plans are IIS + ASP.NET + PHP only. Before planning on option B, ask the
host these five questions — if any answer is "no", only option A is available:

1. Is Node.js installed on the server, and **which version**? (needs 18.19+ / 20.11+ / 22)
2. Am I allowed a **long-running process** / Windows service, or only request-scoped handlers?
3. Is **URL Rewrite 2.1** available, and is `<serverVariables>` unlocked?
4. Is **Application Request Routing** (proxy) enabled, or **HttpPlatformHandler** installed?
5. Can I set environment variables (`PORT`, `NODE_ENV`) for the site or the service?

Plesk for Windows historically offered Node.js through iisnode; on current builds it is often
removed or version-locked to old Node, which makes option B unrealistic on shared plans. Option A
needs none of the above — it is plain static file hosting.

Questions 1, 4 and 5 can be answered without a support ticket: `HC.Web/scripts/node-probe/`
contains a deployable probe (an `httpPlatform` handler running a 60-line HTTP server) whose README
also documents the RDP and Plesk-panel checks, and how to read each IIS error code. If the probe
answers in JSON, the host can run option B; if it returns `500.19`, the handler section is locked
and that is an answer too.

---

## 5. Decision guide

| Situation | Recommendation |
|---|---|
| Site stays on the current Plesk/Windows shared host | **Option A** — `Prerender` for `/`, `/shop`, policy pages; `Client` for everything else; ship `browser/` only |
| SEO matters, data freshness does not | **Option A**, and widen the `Prerender` set over time |
| SEO matters for product pages too | Either `getPrerenderParams` in a scheduled static build (A), or per-request SSR (B) |
| You own a Windows VPS and are fine running a service | **Option B2** (NSSM + ARR) — most predictable; B1 (HttpPlatformHandler) if you prefer IIS to own the process |
| You want the least operational risk | **Option A**. No Node version drift, no process reaper, no proxy config, no `allowedHosts` |
| You need personalised HTML per request | Only **Option B** can do that (option A gives a single HTML snapshot) |

**Recommendation for HC.Web as it stands:** option A. The storefront API is already a separate
origin (`https://serv.homecuties.com/api`), the site already publishes static files to IIS, and
`public/web.config` was written for exactly this shape. Route-driven rendering then only decides
*which* routes get baked HTML — no new infrastructure at all. Revisit option B only if you move to
a VPS/dedicated server and need live per-request rendering.

---

## 6. What changes for each option

### Option A — files to touch

| File | Change |
|---|---|
| `HC.Web/src/app/app.routes.server.ts` | **new** — the `ServerRoute[]` table (section 2.1) |
| `HC.Web/src/app/app.module.server.ts` | add `providers: [provideServerRouting(serverRoutes)]` (section 2.2) |
| `HC.Web/angular.json` | add `"outputMode": "static"`; remove `"prerender": false` and the `"ssr"` block; keep `"server": "src/main.server.ts"` (section 2.3) |
| `HC.Web/public/web.config` | optional but recommended: put the `index.csr.html` rewrite first so `Client` routes get the shell and not the prerendered home page (section 2.4) |
| Deployment | publish `HC.Web/dist/hc.web/browser` to the site root |

### Option B — files to touch

| File | Change |
|---|---|
| `HC.Web/src/app/app.routes.server.ts` | **new** — table using `RenderMode.Server` where live HTML is needed |
| `HC.Web/src/app/app.module.server.ts` | add `providers: [provideServerRouting(serverRoutes)]` |
| `HC.Web/angular.json` | add `"ssr": { "entry": "src/server.ts", "allowedHosts": [...] }` and `"outputMode": "server"` (with it, prerendering of `Prerender` routes is implied as well) |
| `HC.Web/src/server.ts` | `app.set('trust proxy', 1)` and/or relative render URL (section 3.4) |
| IIS | `web.config` with reverse proxy (3.3) or `httpPlatform` (3.2); ARR/HttpPlatformHandler installed |
| Deployment | publish `server/` **and** `browser/` as siblings; Node service or HttpPlatformHandler |

### Verify locally before deploying (both options)

```powershell
cd HC.Web

# Option A: build and inspect what got prerendered
npm run build
Get-Content dist/hc.web/prerendered-routes.json      # should list '/', '/shop', ...
Get-ChildItem dist/hc.web/browser -Directory         # shop/, privacy-policy/, ...
Test-Path dist/hc.web/browser/index.html             # True when '/' is prerendered

# Option B: run the Node renderer and hit a Server-mode route
$env:PORT = 4000; npm run serve:ssr:HC.Web
# in another shell:
#   curl.exe -I http://localhost:4000/            -> 200, HTML body contains rendered markup
#   curl.exe -I http://localhost:4000/product/1   -> 200, rendered per request
```

Serving `dist/hc.web/browser` with any static server (IIS, `npx serve`) is a faithful rehearsal of
option A; if deep links work there, they work on IIS with `web.config` in place.

---

## 7. Sources used for this analysis

- `HC.Web/angular.json` — current `prerender`/`ssr` options
- `HC.Web/src/server.ts` — Express + `CommonEngine`, `PORT`, `../browser` resolution
- `HC.Web/src/app/app.module.server.ts`, `app.module.ts` — NgModule bootstrap, hydration
- `HC.Web/src/app/app-routing.module.ts` — the route list the server table mirrors
- `HC.Web/public/web.config` — existing IIS SPA/static configuration
- `HC.Web/node_modules/@angular/ssr/index.d.ts` — `RenderMode`, `ServerRoute`,
  `provideServerRouting`
- `HC.Web/node_modules/@angular/build/src/builders/application/schema.json` — `outputMode`,
  `ssr.allowedHosts`
- `HC.Web/node_modules/@angular/build/src/builders/application/options.js` — `index.csr.html` /
  `index.server.html` naming
- `HC.Web/node_modules/@angular/build/src/utils/server-rendering/prerender.js` — static build
  rejects `RenderMode.Server`

