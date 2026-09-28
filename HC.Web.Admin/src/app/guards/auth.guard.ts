import { Injectable } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivate, Router, RouterStateSnapshot } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Keeps visitors without a valid admin session out of the admin shell. The API validates the token
 * again on every call, so this only saves a round-trip and shows the login page straight away.
 */
@Injectable({
  providedIn: 'root'
})
export class AuthGuard implements CanActivate {
  constructor(
    private authService: AuthService,
    private router: Router
  ) {}

  canActivate(_route: ActivatedRouteSnapshot, state: RouterStateSnapshot): boolean {
    if (this.authService.isLoggedIn()) {
      return true;
    }

    const hadToken = !!this.authService.getToken();
    this.authService.clearSession();
    this.router.navigate(['/login'], {
      queryParams: {
        ...(hadToken ? { sessionExpired: true } : {}),
        returnUrl: state.url
      }
    });

    return false;
  }
}
