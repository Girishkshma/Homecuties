import { DOCUMENT } from '@angular/common';
import { Inject, Injectable } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import {
  BRAND_NAME,
  DEFAULT_DESCRIPTION,
  DEFAULT_KEYWORDS,
  INDEXABLE_ROBOTS,
  NOINDEX_ROBOTS,
  PageMetadata,
  SHARE_IMAGE_HEIGHT,
  SHARE_IMAGE_PATH,
  SHARE_IMAGE_WIDTH,
  absoluteUrl,
  metaDescription,
  pagePath,
  titleWithBrand
} from './page-metadata';

/**
 * Writes a page's title, description, canonical, share-card tags and structured data into the document head.
 *
 * It is called from two places, and they are deliberately the only two:
 *
 * - PageTitleStrategy, once per navigation, from the title and data the route carries. That is what makes a page's
 *   head right the moment it is opened, and - because the storefront is rendered on the server - right in the HTML a
 *   crawler is served, before any JavaScript of ours runs.
 * - The pages that cannot know what they are until they have asked: the shop (which collection, or which search) and
 *   a product page (which piece). They call it again once the answer is in.
 *
 * Nothing here reads window: the storefront renders on the server too, so absolute urls come from
 * environment.siteUrl (see page-metadata.ts) and the document is the injected one, which the server supplies.
 */
@Injectable({ providedIn: 'root' })
export class SeoService {

  /**
   * The id of the one block of page-level structured data (a Product on a product page). The site-wide graph - the
   * Organization and the WebSite - lives in index.html and is never touched: it is true of every page.
   */
  private static readonly PAGE_STRUCTURED_DATA_ID = 'page-structured-data';

  constructor(
    private readonly title: Title,
    private readonly meta: Meta,
    @Inject(DOCUMENT) private readonly document: Document
  ) { }

  /** Puts one page's metadata into the head, replacing whatever the last page left there. */
  apply(page: PageMetadata): void {
    const title = titleWithBrand(page.title);
    const description = metaDescription(page.description) || DEFAULT_DESCRIPTION;
    const url = absoluteUrl(pagePath(page.path));
    const shareCard = !page.image;
    const image = absoluteUrl(page.image ?? SHARE_IMAGE_PATH);
    const imageAlt = page.imageAlt || title;

    this.title.setTitle(title);

    this.meta.updateTag({ name: 'description', content: description });
    this.meta.updateTag({ name: 'keywords', content: (page.keywords?.length ? page.keywords : DEFAULT_KEYWORDS).join(', ') });
    this.meta.updateTag({ name: 'robots', content: page.noindex ? NOINDEX_ROBOTS : INDEXABLE_ROBOTS });

    // A page that is not to be indexed is not to be canonicalised either: the two say opposite things about the
    // same url, and a crawler that is told both believes neither.
    if (page.noindex) {
      this.removeCanonical();
    } else {
      this.setCanonical(url);
    }

    this.meta.updateTag({ property: 'og:type', content: page.type ?? 'website' });
    this.meta.updateTag({ property: 'og:site_name', content: BRAND_NAME });
    this.meta.updateTag({ property: 'og:locale', content: 'en_IN' });
    this.meta.updateTag({ property: 'og:title', content: title });
    this.meta.updateTag({ property: 'og:description', content: description });
    this.meta.updateTag({ property: 'og:url', content: url });
    this.meta.updateTag({ property: 'og:image', content: image });
    this.meta.updateTag({ property: 'og:image:alt', content: imageAlt });

    // The generated card's size and type are known; a product photograph's are not, and a share card that is told
    // the wrong size crops the piece it is showing.
    if (shareCard) {
      this.meta.updateTag({ property: 'og:image:type', content: 'image/png' });
      this.meta.updateTag({ property: 'og:image:width', content: `${SHARE_IMAGE_WIDTH}` });
      this.meta.updateTag({ property: 'og:image:height', content: `${SHARE_IMAGE_HEIGHT}` });
    } else {
      this.meta.removeTag('property="og:image:type"');
      this.meta.removeTag('property="og:image:width"');
      this.meta.removeTag('property="og:image:height"');
    }

    this.meta.updateTag({ name: 'twitter:card', content: 'summary_large_image' });
    this.meta.updateTag({ name: 'twitter:title', content: title });
    this.meta.updateTag({ name: 'twitter:description', content: description });
    this.meta.updateTag({ name: 'twitter:image', content: image });
    this.meta.updateTag({ name: 'twitter:image:alt', content: imageAlt });
  }

  /**
   * The page-level structured data: a JSON-LD block that describes what this page is about (a Product), so a result
   * can show its price and whether it is in stock. Passing null clears it, which is what a page does when it is left.
   */
  setStructuredData(data: Record<string, unknown> | null): void {
    this.removeStructuredData();

    if (!data) {
      return;
    }

    const script = this.document.createElement('script');
    script.setAttribute('type', 'application/ld+json');
    script.setAttribute('id', SeoService.PAGE_STRUCTURED_DATA_ID);
    script.textContent = JSON.stringify(data);

    this.document.head.appendChild(script);
  }

  /** Takes the page's structured data back out of the head, so it cannot be read off the page that comes next. */
  removeStructuredData(): void {
    this.document.getElementById(SeoService.PAGE_STRUCTURED_DATA_ID)?.remove();
  }

  /** The canonical address of the page being shown, creating the link the shell may not have had. */
  private setCanonical(href: string): void {
    let link = this.document.querySelector('link[rel="canonical"]');

    if (!link) {
      link = this.document.createElement('link');
      link.setAttribute('rel', 'canonical');
      this.document.head.appendChild(link);
    }

    link.setAttribute('href', href);
  }

  private removeCanonical(): void {
    this.document.querySelector('link[rel="canonical"]')?.remove();
  }
}
