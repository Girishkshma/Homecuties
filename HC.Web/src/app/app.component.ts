import { Component, OnDestroy, OnInit, Inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { NavigationEnd, Router } from '@angular/router';
import { Subscription, filter } from 'rxjs';
import { UtilityService, AccessTrackingRequest } from './services/utility.service';
import { VisitorLocationService } from './services/visitor-location.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  standalone: false,
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit, OnDestroy {
  /** The router's own news that a page was opened - the only page view a single-page shop announces. */
  private navigationSubscription?: Subscription;

  constructor(
    private utilityService: UtilityService,
    private visitorLocation: VisitorLocationService,
    private router: Router,
    @Inject(PLATFORM_ID) private platformId: Object
  ) {}

  /**
   * Tracks every page the shop shows, not only the first one it opens with.
   *
   * The shop is one page as far as the browser is concerned: a link swaps the view and rewrites the address
   * instead of asking for the page again, so 'ngOnInit' runs once for a whole visit and the page the reader
   * landed on would be the only one the shop ever heard about. The router's 'NavigationEnd' is what a page view
   * looks like here, and the router announces it for the first page too - which is why nothing is tracked from
   * 'ngOnInit' itself, or the page the reader arrived on would be counted twice.
   *
   * Server-side rendering is left out: there is no reader there, and the address on that connection is the
   * server's own.
   */
  ngOnInit(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    this.navigationSubscription = this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe((event: NavigationEnd) => this.trackAccess(event.urlAfterRedirects));
  }

  ngOnDestroy(): void {
    this.navigationSubscription?.unsubscribe();
  }

  /**
   * Tells the API that a page was opened, so the shop can see where it is being read from and how often - a
   * location and a count, never a person (see HC.Business.AccessTrackingService).
   *
   * The page is the router's own address for it ('/product/10045?categoryId=17') and not 'window.location' read
   * at the same moment, which still names the page being left on some browsers; the query string is kept,
   * because that is what makes one page distinct from another.
   *
   * Only the page and the page before it are sent. The reader's address is NOT read here and no place is
   * claimed: the API takes the address off the connection and looks the place up itself, so nothing the
   * browser says can put a visitor in a country they are not in - and no third-party service has to be
   * reachable from the shopper's device for the shop's own figures to be right.
   *
   * The one addition is the point a visitor agreed to share (see VisitorLocationService and the strip at the
   * foot of every page). It is sent only when there is one - so a visitor who said 'Not now', whose browser
   * cannot tell, or who has never been asked sends exactly what the shop always sent, and the API places the
   * hit by the address it came from.
   *
   * It is deliberately fire-and-forget: an error is left alone, because tracking is not worth a page.
   */
  private trackAccess(pageUrl: string): void {
    const visit: AccessTrackingRequest = {
      pageUrl,
      referrerUrl: document.referrer || null
    };

    // A visitor who agreed earlier but whose device has not answered yet is asked again here, so a fix that
    // timed out on the first attempt is still picked up by a later page view.
    this.visitorLocation.refresh();

    const sharedPoint = this.visitorLocation.sharedPoint;

    if (sharedPoint) {
      visit.latitude = sharedPoint.latitude;
      visit.longitude = sharedPoint.longitude;
      visit.accuracyMeters = sharedPoint.accuracyMeters;
    }

    this.utilityService
      .addAccessTracking(visit)
      .subscribe({
        error: () => {
          // The page is already drawn; a visit that was not recorded is the shop's loss, not the shopper's.
        }
      });
  }
}


