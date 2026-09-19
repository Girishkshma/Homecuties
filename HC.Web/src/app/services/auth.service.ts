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

  /** Clears both the customer and the token (logout, or a 401 from the API). */
  clearSession(): void {
    if (this.isBrowser) {
      localStorage.removeItem(AuthService.TokenKey);
    }
    this.clearCurrentCustomer();
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
