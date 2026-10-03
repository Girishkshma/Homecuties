import { Injectable } from '@angular/core';
import {
  ActivatedRouteSnapshot,
  CanActivate,
  Router,
  RouterStateSnapshot,
  UrlTree
} from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Protects pages that require a signed-in customer (e.g. checkout).
 * Guests are sent to the login page with a returnUrl so that, once they sign in or
 * register, they land back on the page they were trying to reach.
 */
@Injectable({ providedIn: 'root' })
export class AuthGuard implements CanActivate {
  constructor(
    private authService: AuthService,
    private router: Router
  ) {}

  canActivate(route: ActivatedRouteSnapshot, state: RouterStateSnapshot): boolean | UrlTree {
    // The API identifies the caller by the signed token, so a stored customer object without a
    // usable token is not a session: the page it allowed through could only answer 401 (which is
    // exactly how 'My Orders' used to look empty for a signed-in customer).
    if (this.authService.hasValidSession()) {
      return true;
    }

    // Drop the unusable leftover so the header, menu and this guard stop disagreeing about
    // whether anybody is signed in, then let the customer sign in again.
    if (this.authService.isLoggedIn()) {
      this.authService.clearSession();
    }

    return this.router.createUrlTree(['/login'], {
      queryParams: { returnUrl: state.url }
    });
  }
}
