import { Injectable } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivate, Router } from '@angular/router';
import { Observable, catchError, map, of } from 'rxjs';
import { PermissionsService } from '../services/permissions.service';

/**
 * Route level section authorization. The route declares the section it belongs to
 * ('data: { section: \"/orders\" }') and the guard allows it only when the admin's role grants that
 * section - the same answer the API gives, because both read it from 'AdminMenusRoles'.
 *
 * A route may declare more than one section instead ('data: { sections: [...] }') when the screen
 * reads what another section's screens act on: the Finance screen is open to a role that may see the
 * orders as well as to one granted the finance menu on its own.
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
    const sections = MenuAccessGuard.sectionsOf(route);

    if (this.permissionsService.canOpenAnySection(...sections)) {
      return of(true);
    }

    // The menus may not be loaded yet (page reload) - ask the API and decide again.
    return this.permissionsService.loadMenus().pipe(
      map(() => {
        if (this.permissionsService.canOpenAnySection(...sections)) {
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

  /**
   * The sections a route belongs to: its single 'section' when it declares one, its list when it declares
   * several, and nothing at all when it declares neither - which the permissions service reads as 'open to
   * whoever reached this route'.
   */
  private static sectionsOf(route: ActivatedRouteSnapshot): string[] {
    const declared = route.data?.['sections'] as string[] | undefined;
    if (declared?.length) {
      return declared;
    }

    const section = (route.data?.['section'] as string) ?? '';
    return section ? [section] : [];
  }
}
