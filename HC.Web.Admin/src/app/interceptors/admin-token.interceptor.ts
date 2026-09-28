import { HttpErrorResponse, HttpEvent, HttpHandler, HttpInterceptor, HttpRequest } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';
/**
 * Puts the JWT that 'POST /api/admin/login' issued on every admin API call as
 * 'Authorization: Bearer <token>' - the API refuses any admin request without it.
 *
 * When the API answers 401 (missing, forged or expired token, or an account that was deactivated)
 * the session is dropped and the admin is sent back to the login page.
 */
@Injectable()
export class AdminTokenInterceptor implements HttpInterceptor {
  constructor(
    private authService: AuthService,
    private router: Router
  ) {}

  intercept(request: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    const token = this.authService.getToken();
    const authorizedRequest = token
      ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : request;

    return next.handle(authorizedRequest).pipe(
      catchError((error: unknown) => {
        if (error instanceof HttpErrorResponse && error.status === 401 && token) {
          this.authService.clearSession();

          if (!this.router.url.startsWith('/login')) {
            this.router.navigate(['/login'], { queryParams: { sessionExpired: true, returnUrl: this.router.url } });
          }
        }

        return throwError(() => error);
      })
    );
  }
}
