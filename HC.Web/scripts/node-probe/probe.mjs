/**
 * Minimal HTTP probe: answers "does this hosting account have Node.js, and which version?".
 *
 * Deploy this file (with web.config next to it) as a folder under the site root, then request the
 * folder in a browser. A JSON response means Node.js runs; an IIS error page means it does not (or
 * that HttpPlatformHandler is unavailable). See README.md in this folder for how to read each
 * status code.
 *
 * It is deliberately dependency-free and read-only: it reports versions and environment, nothing
 * else, and it never writes to disk. Delete the whole folder once the question is answered.
 */
import { createServer } from 'node:http';

const port = Number(process.env.PORT ?? process.env.HTTP_PLATFORM_PORT ?? 0);
const version = process.versions.node;
const [major, minor] = version.split('.').map(Number);

// Angular 19.2 (this repo) requires ^18.19.1 || ^20.11.1 || >=22.0.0 — see @angular/cli engines.
const meetsAngularRequirement =
  major === 18 ? minor >= 19 : major === 20 ? minor >= 11 : major >= 22;

function report() {
  return {
    ok: true,
    message: 'Node.js is available on this host.',
    node: version,
    meetsAngular19Requirement: meetsAngularRequirement,
    platform: process.platform,
    arch: process.arch,
    executable: process.execPath,
    pid: process.pid,
    cwd: process.cwd(),
    uptimeSeconds: Math.round(process.uptime()),
    environment: {
      PORT: process.env.PORT ?? null,
      HTTP_PLATFORM_PORT: process.env.HTTP_PLATFORM_PORT ?? null,
      NODE_ENV: process.env.NODE_ENV ?? null,
    },
    time: new Date().toISOString(),
  };
}

if (!port) {
  console.error('No PORT / HTTP_PLATFORM_PORT provided. IIS supplies it through httpPlatform.');
  process.exit(1);
}

const server = createServer((req, res) => {
  res.writeHead(200, {
    'content-type': 'application/json; charset=utf-8',
    'cache-control': 'no-store',
  });
  res.end(JSON.stringify(report(), null, 2));
  console.log(`${new Date().toISOString()} ${req.method} ${req.url} -> 200`);
});

// IIS proxies to localhost, so there is no reason to listen on anything wider.
server.listen(port, '127.0.0.1', () => {
  console.log(`node-probe listening on 127.0.0.1:${port} (node ${version})`);
});
