import { PLATFORM_ID } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { GeoConsentComponent } from './geo-consent.component';
import { VisitorLocationService } from '../services/visitor-location.service';

/** A Chennai fix, as a device reports it: the point, and how sure the device is of it in metres. */
const chennaiFix = {
  coords: { latitude: 13.0826802, longitude: 80.2707184, accuracy: 40 },
  timestamp: 0
} as GeolocationPosition;

/** The failure the browser's own prompt hands back when it was answered 'block'. */
const refused = {
  code: 1,
  message: 'User denied Geolocation',
  PERMISSION_DENIED: 1,
  POSITION_UNAVAILABLE: 2,
  TIMEOUT: 3
} as GeolocationPositionError;

/** The failure it hands back when it simply could not get a fix in time - which is not a refusal. */
const timedOut = {
  code: 3,
  message: 'Timeout expired',
  PERMISSION_DENIED: 1,
  POSITION_UNAVAILABLE: 2,
  TIMEOUT: 3
} as GeolocationPositionError;

/**
 * The location question, tested against the one thing it talks to: the browser. The shop asks nothing of the
 * device until the visitor agrees, and whatever the device says - a fix, a refusal, a timeout, or nothing at
 * all - the page carries on.
 */
describe('GeoConsentComponent', () => {
  let fixture: ComponentFixture<GeoConsentComponent>;
  let service: VisitorLocationService;
  let asked: jasmine.Spy;

  beforeEach(async () => {
    // The answer is remembered by the browser, and one test's answer must not decide the next one's.
    localStorage.clear();

    await TestBed.configureTestingModule({
      declarations: [GeoConsentComponent]
    }).compileComponents();

    service = TestBed.inject(VisitorLocationService);

    // The browser is stood up here, because the geolocation call is the only thing the strip ever asks of it.
    asked = spyOn(navigator.geolocation, 'getCurrentPosition');

    fixture = TestBed.createComponent(GeoConsentComponent);
  });

  /** Whether the strip is on the page, drawn from the template rather than from the component's own state. */
  function stripIsDrawn(): boolean {
    return fixture.nativeElement.querySelector('.geo-consent') !== null;
  }

  function click(selector: string): void {
    fixture.nativeElement.querySelector(selector).click();
    fixture.detectChanges();
  }

  it('asks an undecided visitor, and asks the device nothing until they agree', () => {
    fixture.detectChanges();

    expect(stripIsDrawn()).toBe(true);
    expect(asked).not.toHaveBeenCalled();
    expect(service.sharedPoint).toBeNull();

    // 'Not now' is an answer, not a delay: the strip goes, the device is never asked, and the page views that
    // follow are exactly the page views a visitor sent before the shop asked at all.
    click('.geo-consent-decline');

    expect(stripIsDrawn()).toBe(false);
    expect(asked).not.toHaveBeenCalled();
    expect(service.sharedPoint).toBeNull();

    // A later visit reads the answer the browser remembered and does not put the question again.
    const laterVisit = new VisitorLocationService(TestBed.inject(PLATFORM_ID));
    expect(laterVisit.isUndecided).toBe(false);
    expect(laterVisit.sharedPoint).toBeNull();
    expect(asked).not.toHaveBeenCalled();
  });

  it('takes the point when the visitor shares, and keeps it for the pages that follow', () => {
    asked.and.callFake((success: PositionCallback) => success(chennaiFix));

    fixture.detectChanges();
    click('.geo-consent-share');

    expect(asked).toHaveBeenCalledTimes(1);
    expect(stripIsDrawn()).toBe(false);
    expect(service.isUndecided).toBe(false);
    expect(service.sharedPoint).toEqual({
      latitude: 13.0826802,
      longitude: 80.2707184,
      accuracyMeters: 40
    });

    // A second page view does not ask the device again, because the point is already known.
    service.refresh();
    expect(asked).toHaveBeenCalledTimes(1);
  });

  it('proceeds with the ordinary page view when the browser refuses, and stops asking', () => {
    asked.and.callFake((_success: PositionCallback, failure?: PositionErrorCallback) => failure?.(refused));

    fixture.detectChanges();
    click('.geo-consent-share');

    // Nothing about the page changed: no point, and the visitor is not asked again - the device has said no,
    // which is the same answer as 'Not now' as far as the shop is concerned.
    expect(service.sharedPoint).toBeNull();
    expect(service.isUndecided).toBe(false);

    const laterVisit = new VisitorLocationService(TestBed.inject(PLATFORM_ID));
    expect(laterVisit.isUndecided).toBe(false);
    expect(laterVisit.sharedPoint).toBeNull();
  });

  it('treats a fix that never arrives as "not yet", not as a refusal', () => {
    asked.and.callFake((_success: PositionCallback, failure?: PositionErrorCallback) => failure?.(timedOut));

    fixture.detectChanges();
    click('.geo-consent-share');

    expect(service.sharedPoint).toBeNull();
    expect(service.isUndecided).toBe(false);

    // The device could not answer this time; a later page view asks it again rather than giving up on a visitor
    // who did agree.
    service.refresh();
    expect(asked).toHaveBeenCalledTimes(2);
  });
});

