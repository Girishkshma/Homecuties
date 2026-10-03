import { Injectable, Inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable } from 'rxjs';
import { ApiConfigService } from './api.service';

export interface CustomerInfo {
  CustomerID: number;
  FirstName: string;
  MiddleName: string;
  LastName: string;
  EmailId: string;
  MobileNumber: string;
  MobileIsd: string;
  IsGuest: boolean;
}

/**
 * Payload of the signed token the API issues on login (kept under 'customerToken' in localStorage).
 * The signature is deliberately not checked here - only the API can do that - so these values are
 * used for 'is this token worth sending, and is it this customer's?' decisions only.
 */
export interface CustomerTokenClaims {
  /** Customers.CustomerID the token was issued for. */
  Cid: number;
  Email: string;
  /** Expiry, as unix seconds. */
  Exp: number;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private static readonly TokenKey = 'customerToken';

  private currentCustomerSubject = new BehaviorSubject<CustomerInfo | null>(null);
  currentCustomer$ = this.currentCustomerSubject.asObservable();
  private isBrowser: boolean;

  constructor(
    private http: HttpClient,
    private config: ApiConfigService,
    @Inject(PLATFORM_ID) private platformId: Object
  ) {
    this.isBrowser = isPlatformBrowser(this.platformId);
    // Restore customer from localStorage on app load (browser only)
    if (this.isBrowser) {
      const stored = localStorage.getItem('currentCustomer');
      if (stored) {
        try {
          this.currentCustomerSubject.next(JSON.parse(stored));
        } catch {
          localStorage.removeItem('currentCustomer');
        }
      }
    }
  }

  login(email: string, password: string): Observable<any> {
    return this.http.post(this.config.getBaseServUrl() + 'Customer/Login', {
      Email: email,
      Password: password
    });
  }

  setCurrentCustomer(customer: CustomerInfo): void {
    if (this.isBrowser) {
      localStorage.setItem('currentCustomer', JSON.stringify(customer));
    }
    this.currentCustomerSubject.next(customer);
  }

  /**
   * Stores the signed-in customer together with the signed token the API issued.
   * The token is sent as 'Authorization: Bearer ...' on every customer API call.
   */
  setSession(customer: CustomerInfo, token?: string | null): void {
    if (this.isBrowser && token) {
      localStorage.setItem(AuthService.TokenKey, token);
    }
    this.setCurrentCustomer(customer);
  }

  /** The signed token, or null when not signed in / token already cleared. */
  getToken(): string | null {
    return this.isBrowser ? localStorage.getItem(AuthService.TokenKey) : null;
  }

  /**
   * True only when the stored session can actually be used: a customer AND a signed token that
   * belongs to that customer and has not expired.
   *
   * The stored customer alone is NOT a session - the API identifies the caller by the token, so a
   * customer without a usable token would make every order/cart call answer 401 while the header
   * and the menu still show the customer as signed in. That is what used to leave 'My Orders'
   * looking empty, so a stale session like that has to be treated as signed out.
   */
  hasValidSession(): boolean {
    const customer = this.currentCustomerSubject.value;
    const claims = this.getTokenClaims();

    if (!customer || !customer.CustomerID || !claims) {
      return false;
    }

    // A token issued for a different customer must never be used for this one - the API would
    // answer for that other customer.
    return claims.Cid === customer.CustomerID && claims.Exp * 1000 > Date.now();
  }

  /**
   * The decoded payload of the stored token, or null when there is none or it cannot be read.
   * Used to detect a token that is missing, expired or belongs to another customer.
   */
  private getTokenClaims(): CustomerTokenClaims | null {
    const payloadPart = this.getToken()?.split('.')[0];
    if (!payloadPart) {
      return null;
    }

    try {
      // The payload is base64url encoded JSON - 'base64url(payloadJson).base64url(signature)'.
      const base64 = payloadPart.replace(/-/g, '+').replace(/_/g, '/');
      const padded = base64 + '='.repeat((4 - (base64.length % 4)) % 4);
      const json = new TextDecoder().decode(Uint8Array.from(atob(padded), char => char.charCodeAt(0)));
      const claims = JSON.parse(json) as CustomerTokenClaims;

      return typeof claims?.Cid === 'number' && typeof claims?.Exp === 'number' ? claims : null;
    } catch {
      return null;
    }
  }

  /** Clears both the customer and the token (logout, or a 401 from the API). */
  clearSession(): void {
    if (this.isBrowser) {
      localStorage.removeItem(AuthService.TokenKey);
      AuthService.disableGoogleAutoSelect();
    }
    this.clearCurrentCustomer();
  }

  /**
   * Asks Google Identity Services to forget the "auto select" state for this browser.
   *
   * This cannot sign the customer out of Google itself (that session belongs to google.com - only
   * the customer can end it), but it stops One Tap / FedCM from silently re-using the previously
   * chosen account after a logout. Safe to call when Google's script was never loaded.
   */
  private static disableGoogleAutoSelect(): void {
    try {
      const gis = (window as any).google;
      if (gis?.accounts?.id?.disableAutoSelect) {
        gis.accounts.id.disableAutoSelect();
      }
    } catch {
      // Google Identity Services is not present (the login page was never opened) - nothing to reset.
    }
  }

  clearCurrentCustomer(): void {
    if (this.isBrowser) {
      localStorage.removeItem('currentCustomer');
    }
    this.currentCustomerSubject.next(null);
  }

  getCurrentCustomer(): CustomerInfo | null {
    return this.currentCustomerSubject.value;
  }

  isLoggedIn(): boolean {
    return this.currentCustomerSubject.value !== null;
  }

  validateGoogleToken(idToken: string): Observable<any> {
    return this.http.post(this.config.getBaseServUrl() + 'GoogleLogin/ValidateToken', {
      IDToken: idToken
    });
  }

  validateFacebookToken(id: string, accessToken: string): Observable<any> {
    return this.http.post(this.config.getBaseServUrl() + 'FacebookLogin/ValidateToken', {
      ID: id,
      AccessToken: accessToken
    });
  }
}
