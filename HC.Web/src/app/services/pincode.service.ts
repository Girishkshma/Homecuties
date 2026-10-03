import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiConfigService } from './api.service';

/**
 * One place a PIN code covers - what the address forms fill the city, the state and the area with.
 * A PIN code usually covers several post offices, so a lookup answers with a list of these; the ones
 * that deliver come first.
 */
export interface PincodeArea {
  /** The post office name - what becomes the area of the address ("Sowcarpet"). */
  Area: string;
  /** The district the post office sits in, which is the closest thing the directory has to a city. */
  City: string;
  State: string;
  /** "Head Post Office", "Sub Post Office" or "Branch Post Office", in the directory's words. */
  BranchType: string;
  /**
   * Whether mail for this PIN code is delivered from here. Most places a PIN covers only handle mail -
   * for 600001 the office that delivers is the GPO, while "Sowcarpet" and "Flower Bazaar" do not - so
   * this is what tells the customer which of the choices is the one their mail comes from.
   */
  Delivers: boolean;
}

/**
 * Answer to 'Customer/GetPincode/{pincode}'. Result === 1 means the areas below are that PIN code's
 * places; Result === 0 means the PIN is unknown, is not a whole PIN, or the lookup could not run - and
 * 'Messages' says so - so the customer types the city and the state themselves.
 */
export interface PincodeLookupResult {
  Result: number;
  Messages: string[];
  Pincode: string;
  Areas: PincodeArea[];
}

/**
 * Looks a PIN code up so the customer never has to type the city, the state and the area that the PIN
 * already says.
 *
 * The lookup runs on the shop's own API, which reads the public India Post directory and caches the
 * answers: the browser never talks to a third party, and the directory is never asked twice for the
 * same PIN code. Being open to guests matters - checkout does not require an account.
 */
@Injectable({
  providedIn: 'root'
})
export class PincodeService {
  constructor(private http: HttpClient, private config: ApiConfigService) { }

  lookupPincode(pincode: string): Observable<PincodeLookupResult> {
    return this.http.get<PincodeLookupResult>(
      this.config.getBaseServUrl() + 'Customer/GetPincode/' + encodeURIComponent(pincode)
    );
  }
}
