import { Injectable } from '@angular/core';
import { ActivatedRouteSnapshot, Data, RouterStateSnapshot, TitleStrategy } from '@angular/router';
import { PageMetadata } from './page-metadata';
import { SeoService } from './seo.service';

/**
 * Turns the router's own title into the whole head of the page being opened.
 *
 * Every route in app-routing.module.ts carries a `title` and, in `data`, the description it should be listed with and
 * whether it is a page anyone may index. Angular calls a TitleStrategy once per navigation with the routes it just
 * activated; this one reads that leaf route and hands the result to SeoService, so the head is written in one place
 * instead of by every component.
 *
 * A page that knows more than its route does - a product page knows which piece, the shop knows which collection - is
 * not left out: it calls SeoService itself once the answer arrives, and corrects what is written here. The route's
 * values are what the head says in between, and what it says when the answer never comes.
 */
@Injectable()
export class PageTitleStrategy extends TitleStrategy {

  constructor(private readonly seo: SeoService) {
    super();
  }

  override updateTitle(snapshot: RouterStateSnapshot): void {
    // buildTitle reads the Route.title of the deepest primary outlet, and is the reason this class extends
    // TitleStrategy rather than reaching into route data by hand (the router keeps route titles under a symbol).
    const title = this.buildTitle(snapshot);

    // A route that sets no title - a redirect, a wildcard - leaves the head standing rather than blanking it.
    if (!title) {
      return;
    }

    const data: Data = deepestRoute(snapshot.root)?.data ?? {};

    this.seo.apply({
      title,
      description: data['description'] as string | undefined,
      noindex: data['noindex'] === true,
      path: snapshot.url
    } satisfies PageMetadata);
  }
}

/** The route a shopper is actually looking at: the last one down the primary outlet chain. */
function deepestRoute(route: ActivatedRouteSnapshot | null): ActivatedRouteSnapshot | null {
  let deepest = route;

  while (deepest?.firstChild) {
    deepest = deepest.firstChild;
  }

  return deepest;
}
