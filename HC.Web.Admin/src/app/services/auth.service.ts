import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { AdminUser } from '../models/admin.model';

/**
 * Claims the API puts into the admin JWT when an admin logs in
 * (see 'AdminJwtTokenService' in HC.Business/Security). All times are Unix seconds (UTC).
 */
export interface AdminTokenPayload {
  sub?: string;
  userId?: string;
  loginId?: string;
  name?: string;
  email?: string;
  role?: string | string[];
  roleId?: string | string[];
  /** When the admin logged in. */
  iat?: number;
  /** When the session expires - the API refuses the token after this instant. */
  exp?: number;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly TOKEN_KEY = 'admin_token';
  private readonly USER_KEY = 'admin_user';

  constructor(private router: Router) { }

  setToken(token: string): void {
    localStorage.setItem(this.TOKEN_KEY, token);
  }

  getToken(): string | null {
    return localStorage.getItem(this.TOKEN_KEY);
  }

  setUser(user: AdminUser): void {
    localStorage.setItem(this.USER_KEY, JSON.stringify(user));
  }

  getUser(): AdminUser | null {
    const userStr = localStorage.getItem(this.USER_KEY);
    if (userStr) {
      return JSON.parse(userStr);
    }
    return null;
  }

  /** Claims of the stored token, or null when there is none or it cannot be read. */
  getTokenPayload(): AdminTokenPayload | null {
    const token = this.getToken();
    if (!token) {
      return null;
    }

    const parts = token.split('.');
    if (parts.length !== 3) {
      return null;
    }

    try {
      const normalized = parts[1].replace(/-/g, '+').replace(/_/g, '/');
      const padded = normalized.padEnd(Math.ceil(normalized.length / 4) * 4, '=');
      return JSON.parse(atob(padded)) as AdminTokenPayload;
    } catch {
      return null;
    }
  }

  /** Instant at which the session ends (from the token), or null. */
  getExpiresOn(): Date | null {
    const exp = this.getTokenPayload()?.exp;
    return exp ? new Date(exp * 1000) : null;
  }

  /** Instant at which the admin logged in (from the token), or null. */
  getLoggedInOn(): Date | null {
    const iat = this.getTokenPayload()?.iat;
    return iat ? new Date(iat * 1000) : null;
  }

  /** Role names carried by the token; fall back to the roles returned by the login response. */
  getRoleNames(): string[] {
    const claims = this.getTokenPayload()?.role;
    if (typeof claims === 'string') {
      return [claims];
    }
    if (Array.isArray(claims)) {
      return claims;
    }
    return (this.getUser()?.roles ?? []).map(role => role.roleName);
  }

  hasRole(roleName: string): boolean {
    const wanted = (roleName ?? '').toLowerCase();
    return this.getRoleNames().some(role => role.toLowerCase() === wanted);
  }

  /**
   * A session counts as valid while a token is stored and it has not expired. The API re-validates
   * every request, so this is only used to keep an obviously stale session out of the admin shell.
   */
  isLoggedIn(): boolean {
    const token = this.getToken();
    if (!token) {
      return false;
    }

    const expiresOn = this.getExpiresOn();
    if (expiresOn && expiresOn.getTime() <= Date.now()) {
      return false;
    }

    return true;
  }

  /** Drops the token and the cached user without navigating (used on 401/expiry). */
  clearSession(): void {
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.USER_KEY);
  }

  logout(): void {
    this.clearSession();
    this.router.navigate(['/login']);
  }
}

