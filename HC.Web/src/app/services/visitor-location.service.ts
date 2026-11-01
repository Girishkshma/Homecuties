import { Inject, Injectable, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

/**
 * A point the visitor chose to share: where their device says it is, and how close the device said that is.
 *
 * The shop never turns this into a place name and never asks a third party about it: the coordinate rides along
 * with the page view, and the API checks it, rounds it to about a hundred metres and stores it beside the place
 * the visitor's address was looked up to (see HC.Business.AccessTrackingRules.ResolveSharedPoint).
 */
export interface SharedPoint {
  latitude: number;
  longitude: number;
  /** How close the device said it was, in metres, or null when it did not say. */
  accuracyMeters: number | null;
}

/** What the visitor answered. There is no third answer: the strip is either still up or it is answered. */
type LocationChoice = 'granted' | 'declined';

/**
 * Where the answer is remembered. Local storage and not a cookie, because it is a preference of this browser and
 * not something the server needs to see; the point itself is never remembered that way, only the answer.
 */
const ChoiceKey = 'hc.location-choice';

/**
 * The visitor's answer to the location question, and the point that answer earns.
 *
 * Three rules shape it:
 *   - Nothing is asked of the browser until the visitor agrees, and the visitor is asked once: the answer is
 *     remembered, so a visitor who said 'Not now' is never asked again and a visitor who said yes is not asked
 *     twice.
 *   - A refused permission, a device with no geolocation and a fix that never arrives are all the same thing to
 *     the shop: no point, and the page view goes exactly as it always has - the API places the hit by the
 *     address it came from. Nothing here throws and nothing here holds a page up.
 *   - The point is kept for this visit only, in memory, and every page view opened after it is answered carries
 *     it. The page the visitor was on when they answered is already recorded by then, and is the one hit that
 *     keeps its address's place alone.
 *
 * Server-side rendering is left out: there is no visitor and no browser there (see the platform checks below).
 */
@Injectable({
  providedIn: 'root'
})
export class VisitorLocationService {
  /** The answer, or null while the question is still up. */
  private choice: LocationChoice | null;

  /** Where the visitor is, once their device has said - for this visit only. */
  private point: SharedPoint | null = null;

  /** Whether a fix is being waited on, so a second page view does not ask the browser twice. */
  private isAsking = false;

  constructor(@Inject(PLATFORM_ID) private platformId: Object) {
    this.choice = this.readChoice();

    // A visitor who agreed on an earlier visit is not asked again - the browser remembers the permission
    // itself, so the point can be taken without a prompt. If they have since taken that permission back, the
    // browser answers 'denied' and the shop stops asking (see 'takePoint').
    if (this.choice === 'granted') {
      this.takePoint();
    }
  }

  /** Whether the strip should still be shown: it is up only while the question is unanswered. */
  get isUndecided(): boolean {
    return this.choice === null;
  }

  /**
   * The point to send with a page view, or null - which is what every visitor who has not agreed sends, and what
   * every page view carried before the shop asked at all.
   */
  get sharedPoint(): SharedPoint | null {
    return this.point;
  }

  /** The visitor pressed 'Share my location': the answer is remembered and the browser is asked. */
  grant(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    this.remember('granted');
    this.takePoint();
  }

  /** The visitor pressed 'Not now': remembered, so the strip does not follow them from page to page. */
  decline(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    this.remember('declined');
  }

  /**
   * Asks again for a point when one is missing - a fix that timed out the first time, or a device that was off
   * the network when the visitor agreed. It is called with every page view, so a visitor who agreed is placed as
   * soon as their device can say where it is; a browser that has already granted the permission answers from its
   * own cache, so this is not a prompt each time.
   */
  refresh(): void {
    if (this.choice === 'granted' && !this.point) {
      this.takePoint();
    }
  }

  /** Asks the browser where the visitor is, and takes the answer - or leaves the point empty. */
  private takePoint(): void {
    if (!isPlatformBrowser(this.platformId) || this.isAsking) {
      return;
    }

    // A browser with no geolocation at all (an old one, a device with it switched off) is not an error: the page
    // views simply go without a point, which is the ordinary page view.
    if (!navigator.geolocation) {
      return;
    }

    this.isAsking = true;

    navigator.geolocation.getCurrentPosition(
      position => {
        this.isAsking = false;
        this.point = {
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
          accuracyMeters: Number.isFinite(position.coords.accuracy) ? position.coords.accuracy : null
        };
      },
      error => {
        this.isAsking = false;

        // The browser was told no - by the visitor in its own prompt, or by their settings - so the shop stops
        // asking and goes back to placing hits by their address. A timeout or a fix that is not ready yet is not
        // a refusal: it is tried again with a later page view.
        if (error && error.code === error.PERMISSION_DENIED) {
          this.remember('declined');
        }
      },
      {
        // A coarse fix is the point of this: the shop wants the area a visitor is shopping from, not their door,
        // and the point is rounded before it is stored anyway. A fix five minutes old is the same town.
        enableHighAccuracy: false,
        maximumAge: 300000,
        timeout: 10000
      }
    );
  }

  /** The answer as this browser remembers it, or null when it has never been asked. */
  private readChoice(): LocationChoice | null {
    if (!isPlatformBrowser(this.platformId)) {
      return null;
    }

    try {
      const remembered = window.localStorage.getItem(ChoiceKey);
      return remembered === 'granted' || remembered === 'declined' ? remembered : null;
    } catch {
      // Storage can be switched off (a locked-down browser, a private window with no room). The question is then
      // put again on the next visit rather than breaking the page it is drawn on.
      return null;
    }
  }

  private remember(choice: LocationChoice): void {
    this.choice = choice;

    if (choice === 'declined') {
      this.point = null;
    }

    try {
      window.localStorage.setItem(ChoiceKey, choice);
    } catch {
      // As above: not remembering is a smaller loss than a broken page.
    }
  }
}


