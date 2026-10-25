import { APP_BASE_HREF } from '@angular/common';
import { CommonEngine, isMainModule } from '@angular/ssr/node';
import express from 'express';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import AppServerModule from './main.server';

const serverDistFolder = dirname(fileURLToPath(import.meta.url));
const browserDistFolder = resolve(serverDistFolder, '../browser');
const indexHtml = join(serverDistFolder, 'index.server.html');

const app = express();

/**
 * The hosts the server will render a page for.
 *
 * CommonEngine refuses any other Host header (SSRF protection) and, while no list is configured, quietly falls back to
 * sending the client-side shell - which is a page whose title, description, canonical and share-card tags are still the
 * home page's, and which a crawler that does not run JavaScript reads as the whole site. Naming the shop's own hosts
 * is what makes the server render the head each route describes (see src/app/seo).
 *
 * `NG_ALLOWED_HOSTS` (comma separated) is read by CommonEngine as well and is how a host this list does not name -
 * a staging name, an IP - is added without a code change. www and the bare domain are both listed: the canonical
 * every page declares is the bare one, and a request that arrives on www must still be rendered.
 */
const commonEngine = new CommonEngine({
  allowedHosts: ['homecuties.com', 'www.homecuties.com', 'localhost', '127.0.0.1']
});

/**
 * Example Express Rest API endpoints can be defined here.
 * Uncomment and define endpoints as necessary.
 *
 * Example:
 * ```ts
 * app.get('/api/**', (req, res) => {
 *   // Handle API request
 * });
 * ```
 */

/**
 * Serve static files from /browser
 */
app.get(
  '**',
  express.static(browserDistFolder, {
    maxAge: '1y',
    index: 'index.html',
    // Everything in the browser folder is fingerprinted by the build except the few files that are not: 'robots.txt',
    // 'sitemap.xml' and 'manifest.webmanifest' are fetched from the same address for years at a time. A year of
    // caching would leave a crawler reading a sitemap the shop had stopped agreeing with, so those three are re-read
    // hourly instead. (They are served by IIS directly where the site is published that way - see public/web.config.)
    setHeaders: (res, filePath) => {
      if (/[\\/](robots\.txt|sitemap\.xml|manifest\.webmanifest)$/.test(filePath)) {
        res.setHeader('Cache-Control', 'public, max-age=3600');
      }
    }
  }),
);

/**
 * Handle all other requests by rendering the Angular application.
 */
app.get('**', (req, res, next) => {
  const { protocol, originalUrl, baseUrl, headers } = req;

  commonEngine
    .render({
      bootstrap: AppServerModule,
      documentFilePath: indexHtml,
      url: `${protocol}://${headers.host}${originalUrl}`,
      publicPath: browserDistFolder,
      providers: [{ provide: APP_BASE_HREF, useValue: baseUrl }],
    })
    .then((html) => res.send(html))
    .catch((err) => next(err));
});

/**
 * Start the server if this module is the main entry point.
 * The server listens on the port defined by the `PORT` environment variable, or defaults to 4000.
 */
if (isMainModule(import.meta.url)) {
  const port = process.env['PORT'] || 4000;
  app.listen(port, () => {
    console.log(`Node Express server listening on http://localhost:${port}`);
  });
}

export default app;
