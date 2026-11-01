import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiConfigService } from './api.service';

/**
 * What a shop front tells the API about a page it has just opened. There is no address here on purpose: the API
 * reads the address off the caller's connection and looks the place up itself, so a browser cannot say it is
 * somewhere it is not (see HC.Business.AccessTrackingService).
 *
 * The three location fields are the exception, and they are the visitor's own: they are present only when that
 * visitor pressed 'Share my location' (see VisitorLocationService), and the API checks and rounds them before
 * storing them beside the place the address was looked up to - they can add to the picture, never redirect it.
 */
export interface AccessTrackingRequest {
  /**
   * The page that was opened, as the router names it - '/product/10004?ref=...'. The query string is part of the
   * page, because it is what makes one view of a page distinct from another.
   */
  pageUrl: string;
  /** The page the reader arrived from, or null when the browser names none (a bookmark, a typed address). */
  referrerUrl: string | null;
  /** The latitude the visitor agreed to share, or left out - which is what every other page view sends. */
  latitude?: number;
  /** The longitude that goes with it; neither half is sent on its own. */
  longitude?: number;
  /** How close the device said that point is, in metres, or null when it did not say. */
  accuracyMeters?: number | null;
}


@Injectable({
  providedIn: 'root'
})
export class UtilityService {
  constructor(private http: HttpClient, private config: ApiConfigService) { }

  /**
   * Records that a page was opened. Fire-and-forget: the answer is not read and the call is not retried,
   * because a visit that was not recorded is the shop's loss and not the shopper's.
   *
   * 'rnd' keeps the browser and every proxy in between from answering this POST out of a cache - a repeated
   * page view has to reach the API to be counted.
   *
   * The payload is handed over as the object it is and never stringified: Angular labels an object body
   * 'application/json', which is the only content type the API's '[FromBody]' action reads, while a stringified
   * body goes out as text and the API answers 415 - a hit lost in silence, because this call is fire-and-forget
   * and its error is deliberately ignored. Every other service here posts an object for the same reason.
   */
  addAccessTracking(data: AccessTrackingRequest): Observable<any> {
    return this.http.post(this.config.getBaseServUrl() + 'Utilities/AddAccessTracking?rnd=' + Math.random(), data);
  }

  static currencyFormat(n: number): string {
    return '₹ ' + Math.round(n) + '.00';
  }

  static sortByKeyAsc(array: any[], key: string): any[] {
    return array.sort((a, b) => {
      const x = a[key];
      const y = b[key];
      if (x < y) return -1;
      if (x > y) return 1;
      return 0;
    });
  }

  static sortByKeyDesc(array: any[], key: string): any[] {
    return array.sort((a, b) => {
      const x = a[key];
      const y = b[key];
      if (x > y) return -1;
      if (x < y) return 1;
      return 0;
    });
  }
}
