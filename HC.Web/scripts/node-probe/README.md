# Is Node.js available on the host? — three ways to find out

HC.Web only needs a Node runtime if you choose **Option B** (route-driven SSR served by
`server.mjs`) from [`../../docs/iis-hosting-options.md`](../../docs/iis-hosting-options.md).
Option A (prerendered static files) needs nothing but IIS. This folder is the test kit for the
"can the box run Node at all?" question.

Method 1 needs console access, method 2 needs the hosting panel, method 3 works when all you have
is FTP and a browser. Any one of them is enough; method 3 also proves the *deployment path*
(handler + reverse proxy into the process) that option B depends on.

---

## 1. If you have RDP / a console on the server

```powershell
node -v                      # 18.19+, 20.11+ or 22+ is what Angular 19.2 accepts
where.exe node               # reveals the install path, e.g. C:\Program Files\nodejs\node.exe
Get-Command npm | Select-Object Source
```

Nothing printed / "not recognized" means no Node on `PATH` — which is *not* proof it is absent, so
also look for the install folder:

```powershell
'C:\Program Files\nodejs\node.exe', 'C:\Program Files (x86)\nodejs\node.exe', 'C:\nvm4w\nodejs\node.exe' |
  ForEach-Object { "$_ -> $(Test-Path $_)" }
```

The version number matters as much as the presence: `node -v` on `16.x` is a "no" for this repo, not
a "yes".

## 2. If you have the hosting panel (Plesk for Windows)

Plesk for Windows exposes Node.js through an extension:

- **Plesk → Extensions** — is "Node.js" listed/installed? If not, the platform cannot host Node apps
  at all and no amount of `web.config` will change that.
- **Plesk → Websites & Domains → <domain> → Node.js** — the page lists the *available* interpreter
  versions. A dropdown with `20.x`/`22.x` is your answer, and it is also where the app's startup
  file and environment variables are configured.
- If the icon is missing, ask support directly: *"Is the Node.js extension enabled for this
  subscription, which versions are available, and is HttpPlatformHandler installed?"* — support
  answers are the most reliable signal on shared hosting, since panel features can be enabled
  per-plan.

## 3. If you only have FTP + a browser: deploy this probe

The folder contains `probe.mjs` (a 60-line, dependency-free HTTP server that replies with the Node
version and environment) and `web.config` (an `httpPlatform` handler entry).

1. In `web.config`, replace `C:\Program Files\nodejs\node.exe` (if the path differs) and the
   `D:\CHANGE-ME\node-probe\probe.mjs` placeholder with the **absolute** path that `probe.mjs` will
   have on the server. Knowing the site's physical path is usually possible from the panel; if not,
   upload first, then ask support for it.
2. Upload the `node-probe` folder to the **site root**, next to `index.html` (so the URL becomes
   `https://<domain>/node-probe/`). Uploading it one level *above* the site root will not work.
3. Request `https://<domain>/node-probe/` in a browser and read the outcome below.
4. **Delete the folder again** — an endpoint that reports paths and PIDs is not something to leave
   on a live site.

### Reading the outcome

| What you see | Meaning | What to do |
|---|---|---|
| JSON with `"ok": true` and a `node` version | Node.js runs, and the IIS → node path works | Check `meetsAngular19Requirement`; `false` means the installed version is too old for Angular 19.2 |
| `HTTP Error 500.19 – Internal Server Error` (config error) | IIS refuses the `handlers` / `httpPlatform` section from a nested folder — section is locked at server or site level | Not proof Node is missing. Ask the host to allow it, or deploy the probe as a separate IIS application/subdomain |
| `HTTP Error 500.50` / `502.3` / a generic 500 | The handler could not start the process — wrong `node.exe` path, not installed, or HttpPlatformHandler missing | Read the log file (below). Try `processPath="node.exe"` to test PATH resolution |
| `HTTP Error 404.0` / 404 | The folder is not where you think it is, or the site's SPA rewrite swallowed the path | Confirm the file exists at `/node-probe/probe.mjs` and the folder is a real directory at the site root |
| Page hangs, then a timeout | The process started but never bound the port | Read the log; it prints the listen result or the error |
| The file's source code shown as text | IIS treated `probe.mjs` as content — the `httpPlatform` handler is not active | Handler not installed, or the request did not go through this folder's `web.config` |

### Logs

`stdoutLogEnabled="true"` writes to `logs/node-probe_<timestamp>.log` relative to the folder, next to
`probe.mjs`. It contains the "listening on 127.0.0.1:<port>" line, so it distinguishes "process
never started" from "started and replied".

### Rehearse locally first (optional)

The probe behaves identically off-IIS, so you can confirm the file you are uploading is sane:

```powershell
cd HC.Web/scripts/node-probe
$env:PORT = 4931; node probe.mjs      # then GET http://127.0.0.1:4931/
```

Without a port it exits 1 with an explanatory message, which is also what IIS shows in the log when
`%HTTP_PLATFORM_PORT%` is not passed through.

---

## Checking a candidate Node build for this repo

A "yes" from the probe only clears the runtime question. `HC.Web` builds with Angular 19.2, whose
engines are `^18.19.1 || ^20.11.1 || >=22.0.0`, so:

- `18.19+`, `20.11+`, `22+` → fine (the probe's `meetsAngular19Requirement` is `true`)
- `18.0–18.18`, `19.x`, `20.0–20.10`, `21.x` → rejected by Angular tooling, and the built server
  bundle is not guaranteed to run either
