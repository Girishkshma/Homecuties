# Search metadata (title, description, share cards, structured data)

Everything a search engine, a link preview or a phone's home screen reads about the storefront.

## Where each piece lives

| What | File |
| --- | --- |
| Shell head: default title/description, Open Graph, Twitter card, `Organization` + `WebSite` JSON-LD | `src/index.html` |
| Per-route title, description and `noindex` | `src/app/app-routing.module.ts` |
| The code that writes a page's head on every navigation | `src/app/seo/page-title.strategy.ts` (+ `SeoService`) |
| The rules: titles, descriptions, canonical paths, Product data | `src/app/seo/page-metadata.ts` (+ `.spec.ts`) |
| Crawler rules and the static sitemap | `public/robots.txt`, `public/sitemap.xml` |
| Install metadata and the icons it names | `public/manifest.webmanifest` |
| The favicons, the PWA icons and the share card | `scripts/generate-icons.ps1` → `public/` |
| The site's own address (canonical, `og:url`, image URLs) | `siteUrl` in `src/environments/environment*.ts` |

## How a page's head is written

1. **The shell** (`src/index.html`) carries the home page's values. It is what the client-side shell serves and what
   anything reading the document before Angular starts sees.
2. **On every navigation** `PageTitleStrategy` reads the route's `title` and its `data.description` / `data.noindex`,
   and `SeoService` writes the title, description, robots, canonical, Open Graph and Twitter tags.
3. **Pages that know more** rewrite the head once the answer is in: the shop with the collection being browsed (or the
   search words, which are `noindex`), a product page with the piece it loaded plus its `Product` JSON-LD.

The order matters. Angular calls the title strategy at the **end** of a navigation, so a page that knows better must
write after its data arrives from the API - never synchronously while it is being activated, or the route's generic
title would overwrite it. That is why `ShopComponent.updateSeo()` is called from API callbacks only.

Because the storefront is rendered on the server (`src/server.ts`), all of this is in the HTML a crawler is served,
before any JavaScript of ours runs.

## Routes

| Route | Title | Indexed |
| --- | --- | --- |
| `/` | `Homecuties — Handcrafted Home Decor, Channapatna Toys & Pots` | yes |
| `/shop` | `Shop Handcrafted Home Decor \| Homecuties` | yes |
| `/shop/:categoryId` | `«collection path» — Handcrafted Decor \| Homecuties` | yes |
| `/product/:id` | `«product title» \| Homecuties` | yes |
| `/privacy-policy`, `/terms-of-service` | `… \| Homecuties` | yes |
| `/cart`, `/checkout`, `/my-orders`, `/favorites`, `/profile`, `/login`, `/register`, `/logout`, password forms | `… \| Homecuties` | **no** (`noindex, follow`) |
| `/shop?q=…` (a search) | `Search: «words» \| Homecuties` | **no** |

A `noindex` page is also given no canonical: the two say opposite things about the same URL.

## Server-side rendering is what puts the head in the HTML

The storefront is rendered by `src/server.ts`. Angular's `CommonEngine` refuses to render for a `Host` header it does
not recognise (SSRF protection) and, while no list is configured, **quietly sends the client-side shell instead** - a
page whose title, description, canonical and `og:` tags are still the home page's. `src/server.ts` therefore names the
shop's hosts (`homecuties.com`, `www.homecuties.com`, `localhost`, `127.0.0.1`). A new host - a staging name, an IP -
is added with the `NG_ALLOWED_HOSTS` environment variable (comma separated) rather than a code change.

A useful side effect of taking the canonical and share URLs from `environment.siteUrl` (rather than from the request's
`req.protocol`/`req.host`) is that the HTML says `https://homecuties.com/...` even though TLS terminates at IIS and the
Node process only ever sees `http://` behind it.

## Adding a page

Add the route with a `title` and a `description` in `src/app/app-routing.module.ts` and nothing else - the head is
written for you. If the page is only about one shopper (a cart, an order, an account page, a form), add `noindex: true`
to its `data`. If it is a static page a crawler should find, add it to `public/sitemap.xml` as well.

A page that cannot know its own title until it has asked the API calls `SeoService.apply(...)` from the callback that
brings the answer, and `SeoService.setStructuredData(...)` if it has something to declare; leaving the page must call
`removeStructuredData()` (see `ProductDetailsComponent.ngOnDestroy`).

## Structured data

- **Site-wide** (`index.html`): `Organization` (name, url, logo, support email) and `WebSite` with a `SearchAction` on
  `https://homecuties.com/shop?q={search_term_string}` — the box a search engine may show under the result.
- **Product pages**: `Product` with name, description, images, category, brand and an `Offer` (INR, the shop's own
  listing price, and `InStock` / `OutOfStock`). A piece the shop has priced at nothing declares no offer rather than an
  offer of zero.
- **Deliberately absent**: `sameAs` (the footer's social links are still placeholders), `aggregateRating` (the shop
  holds no ratings), and any address or phone number. Add them when the shop really has them — a claim a search engine
  can check and find false is worse than no claim.

## The icons and the share card

Both come out of one script, for two different jobs.

The **icons** (`favicon.ico` and its PNG frames, `icon-192.png`, `icon-512.png`, `apple-touch-icon.png`) are the
**butterfly from the logo** — the two striped wings in orange, red, yellow and green, the black body and the two
antennae, cut out of the left of `src/assets/images/logo.png`. It is the same mark that sits beside the wordmark in the
header, not a drawing of a butterfly in the right colours: at the size a tab shows, the stripes are what identify it, and
a separate drawing reads as a different butterfly.

The cut is measured, not eyeballed. In the 1800 × 564 lockup the butterfly occupies x 4..823, y 1..557, and pixel 824 is
already the orange "C" of the Cuties script, which butts against the wing tip with no white gap — so the script cuts just
before that column, trims to the ink, and stops with an error rather than writing icons with half a wing if the mark ever
moves. Each size is resampled by repeated halving and premultiplied alpha, which is what keeps the stripes from turning
into a pale, dotted smear at 16 px.

`og-image.png` (1200 × 630) is the other job: the **whole logo lockup** on white, cut from
`src/assets/images/logo.png`. A link preview is large enough to carry the wordmark, and the wordmark is what says who the
shop is in someone else's timeline.

Re-run the script after the logo changes, and commit what it writes:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\generate-icons.ps1
```

A product page replaces the card with the piece's own first photograph, so a shared product link shows the product.

## Crawler files

- `public/robots.txt` allows everything and points at the sitemap. It deliberately has **no** `Disallow` list: the
  private pages carry `noindex` in their heads, and a blocked page is never fetched, so its `noindex` would never be
  read.
- `public/sitemap.xml` lists the pages that exist before anything is loaded from the API. Product and collection pages
  are found by following the shop's own links, not by this file — a sitemap written by hand would fill up with pieces
  that have been renamed or sold out.
- `robots.txt`, `sitemap.xml` and `manifest.webmanifest` are served with an hour of caching by `src/server.ts`; the rest
  of the build output is fingerprinted and cached for a year.

## Checking a change

```powershell
# The head as a crawler sees it (the server writes it, so no JavaScript has to run)
npm run build
node dist/hc.web/server/server.mjs
curl -s http://localhost:4000/shop | Select-String -Pattern '<title>|og:|canonical|ld\+json'
```

- Rich results: <https://search.google.com/test/rich-results> (paste a product URL).
- Share cards: <https://developers.facebook.com/tools/debug/> and the X card validator — both cache, so bump the URL
  with `?v=2` while testing.
- Tab title and favicon: a hard reload or a private window; browsers cache favicons aggressively.
