import { Injectable } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivate, Router } from '@angular/router';
import { Observable, catchError, map, of } from 'rxjs';
import { PermissionsService } from '../services/permissions.service';

/**
 * Route level section authorization. The route declares the section it belongs to
 * ('data: { section: \"/orders\" }') and the guard allows it only when the admin's role grants that
 * section - the same answer the API gives, because both read it from 'AdminMenusRoles'.
 */
@Injectable({
  providedIn: 'root'
})
export class MenuAccessGuard implements CanActivate {
  constructor(
    private permissionsService: PermissionsService,
    private router: Router
  ) {}

  canActivate(route: ActivatedRouteSnapshot): Observable<boolean> {
    const section = (route.data?.['section'] as string) ?? '';

    if (!section || this.permissionsService.canOpenSection(section)) {
      return of(true);
    }

    // The menus may not be loaded yet (page reload) - ask the API and decide again.
    return this.permissionsService.loadMenus().pipe(
      map(() => {
        if (this.permissionsService.canOpenSection(section)) {
          return true;
        }

        this.router.navigate(['/dashboard']);
        return false;
      }),
      catchError(() => {
        // The API refused the request; let it surface the error instead of looping on a redirect.
        this.router.navigate(['/dashboard']);
        return of(false);
      })
    );
  }
}
