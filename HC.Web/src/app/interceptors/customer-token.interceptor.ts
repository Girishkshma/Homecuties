import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { isPlatformBrowser } from '@angular/common';
import { inject, PLATFORM_ID } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

/**
 * Attaches the signed customer token (issued on login/registration) to every API call, and when
 * the API rejects it with 401 it clears the session and sends the visitor to the login page so
 * they can sign in again and come straight back.
 */
export const customerTokenInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  const token = authService.getToken();
  const authorizedRequest = token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(authorizedRequest).pipe(
    catchError((error: unknown) => {
      // 401 means the token was missing, expired or is no longer accepted. The stored customer
      // object alone is not a session (see AuthService.hasValidSession), so it is always thrown
      // away here - otherwise a browser with a stale session keeps pretending to be signed in and
      // 'My Orders' just looks empty instead of asking the customer to sign in again.
      if (error instanceof HttpErrorResponse && error.status === 401 && isBrowser) {
        authService.clearSession();

        // Navigating to '/login' from '/login' (or '/set-password') would only reload the page.
        if (!router.url.startsWith('/login')) {
          router.navigate(['/login'], { queryParams: { returnUrl: router.url } });
        }
      }

      return throwError(() => error);
    })
  );
};
