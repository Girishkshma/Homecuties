import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, RouterModule, Routes } from '@angular/router';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { AppComponent } from './app.component';
import { ApiConfigService } from './services/api.service';
import { VisitorLocationService } from './services/visitor-location.service';

/** A page with nothing drawn in it: the root component only cares that the router opened one. */
@Component({ template: '', standalone: false })
class BlankPageComponent {}

/** The addresses these tests open, all of them served by the blank page above. */
const routes: Routes = [
  { path: '', component: BlankPageComponent },
  { path: 'shop/:id', component: BlankPageComponent },
  { path: 'product/:id', component: BlankPageComponent }
];

/** What the shop was told about one page, as it went out over the wire. */
interface VisitSent {
  pageUrl: string;
  referrerUrl: string | null;
  latitude?: number;
  longitude?: number;
  accuracyMeters?: number | null;
}

describe('AppComponent', () => {
  let http: HttpTestingController;
  let router: Router;
  let trackingUrl: string;

  beforeEach(async () => {
    // The location question's answer is remembered by the browser (see VisitorLocationService), and one test's
    // answer must not decide the next one's.
    localStorage.clear();

    await TestBed.configureTestingModule({
      imports: [
        RouterModule.forRoot(routes),
        // The root component tracks visits through UtilityService, which needs an HttpClient.
        HttpClientTestingModule
      ],
      declarations: [
        AppComponent,
        BlankPageComponent
      ],
      // The template renders the header/footer components declared by AppModule, which these tests do
      // not load.
      schemas: [CUSTOM_ELEMENTS_SCHEMA]
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    trackingUrl = TestBed.inject(ApiConfigService).getBaseServUrl() + 'Utilities/AddAccessTracking';
  });


  afterEach(() => http.verify());

  /**
   * The visits reported since the last call, taken off the wire and answered, so each page opened can be
   * counted on its own. Answering them is also what keeps 'http.verify()' from failing on the
   * fire-and-forget call.
   */
  function visitsSent(): VisitSent[] {
    return http.match(request => request.url.startsWith(trackingUrl))
      .map(request => {
        const visit = request.request.body as VisitSent;

        request.flush({ Result: 1, Messages: ['Access tracked'] });

        return visit;
      });
  }

  // The placeholder 'title' tests that used to live here were left over from the CLI template and
  // broke the whole test build ('AppComponent' has no 'title'), so nothing could be executed.
  it('should create the app', () => {
    const fixture = TestBed.createComponent(AppComponent);
    expect(fixture.componentInstance).toBeTruthy();

    // The router's own first navigation may already have run: that is the shop's first page, not this
    // test's.
    visitsSent();
  });

  it('records a visit for every page the reader opens, not only the first', async () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    // The page the shop opened on, counted by the first navigation rather than by the component's start-up.
    visitsSent();

    await router.navigateByUrl('/shop/7');

    const shop = visitsSent();
    expect(shop.length).toBe(1);
    expect(shop[0].pageUrl).toBe('/shop/7');

    await router.navigateByUrl('/product/10045?categoryId=17');

    // The second page of one visit - what the shop never heard about while only the landing page was
    // tracked - and the query string that makes one product page distinct from another.
    const product = visitsSent();
    expect(product.length).toBe(1);
    expect(product[0].pageUrl).toBe('/product/10045?categoryId=17');
  });

  it('sends the visit as a JSON object, which is the only body the API reads', async () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    visitsSent();

    await router.navigateByUrl('/shop/7');

    const sent = http.match(request => request.url.startsWith(trackingUrl));
    expect(sent.length).toBe(1);

    // A stringified body would leave without a content type of its own and the API would answer 415 -
    // the visit would be lost in silence, because the call is fire-and-forget.
    expect(typeof sent[0].request.body).toBe('object');
    expect(Object.keys(sent[0].request.body).sort()).toEqual(['pageUrl', 'referrerUrl']);

    sent[0].flush({ Result: 1, Messages: ['Access tracked'] });
  });

  /**
   * A visitor who agreed to share is described by their device as well as by their address: the point rides along
   * with every page view recorded after they answered, and nothing else about the page view changes. The visitor
   * who has not answered sends exactly what the shop always sent - which is what makes the API's fallback (place
   * the hit by the address) the ordinary path rather than an error path.
   */
  it('carries the point a visitor agreed to share, and nothing at all when they did not', async () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    visitsSent();

    await router.navigateByUrl('/shop/7');

    const unshared = visitsSent();
    expect(unshared.length).toBe(1);
    expect(unshared[0].latitude).toBeUndefined();
    expect(unshared[0].longitude).toBeUndefined();
    expect(unshared[0].accuracyMeters).toBeUndefined();

    // What 'Share my location' leaves behind for the page views that follow (VisitorLocationService).
    spyOnProperty(VisitorLocationService.prototype, 'sharedPoint', 'get').and.returnValue({
      latitude: 13.0826802,
      longitude: 80.2707184,
      accuracyMeters: 40
    });

    await router.navigateByUrl('/product/10045');

    const shared = visitsSent();
    expect(shared.length).toBe(1);
    expect(shared[0].pageUrl).toBe('/product/10045');
    expect(shared[0].latitude).toBe(13.0826802);
    expect(shared[0].longitude).toBe(80.2707184);
    expect(shared[0].accuracyMeters).toBe(40);
  });
});

