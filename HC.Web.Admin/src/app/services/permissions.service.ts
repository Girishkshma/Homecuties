import { Injectable } from '@angular/core';
import { Observable, catchError, shareReplay, tap, throwError } from 'rxjs';
import { AdminMenu } from '../models/admin.model';
import { AdminService } from './admin.service';
import { AuthService } from './auth.service';

/**
 * The sections of the admin area the signed-in admin may open.
 *
 * The list is not hard-coded here: it comes from 'POST /api/admin/menus', which the API answers from
 * the roles inside the JWT and the role to menu mapping in the database ('AdminMenusRoles'). The
 * navigation, the route guard and the API therefore all agree on what this admin may see.
 */
@Injectable({
  providedIn: 'root'
})
export class PermissionsService {
  private menus: AdminMenu[] = [];
  /** Menu URLs (including children) the admin may open, e.g. ['/orders', '/products']. */
  private sections = new Set<string>();
  private menusRequest$?: Observable<AdminMenu[]>;

  constructor(
    private adminService: AdminService,
    private authService: AuthService
  ) {}

  /** Loads the menus of the signed-in admin (cached until 'force' is set or the call fails). */
  loadMenus(force = false): Observable<AdminMenu[]> {
    if (force) {
      this.menusRequest$ = undefined;
    }

    if (!this.menusRequest$) {
      this.menusRequest$ = this.adminService.getMenus().pipe(
        tap(menus => this.setMenus(menus)),
        catchError(error => {
          this.menusRequest$ = undefined;
          return throwError(() => error);
        }),
        shareReplay(1)
      );
    }

    return this.menusRequest$;
  }

  /** Menus that are already loaded (the sidebar renders from these). */
  getMenus(): AdminMenu[] {
    return this.menus;
  }

  /** True when the admin's roles grant the given section ('/orders'). */
  canOpenSection(section?: string): boolean {
    if (!section) {
      return true;
    }

    if (this.sections.has(section)) {
      return true;
    }

    // '/orders/10011' belongs to the '/orders' section.
    const firstSegment = PermissionsService.sectionOf(section);
    return firstSegment === section ? false : this.sections.has(firstSegment);
  }

  /**
   * True when the admin's roles grant at least one of the given sections ('/finance' or '/orders').
   *
   * A screen that reads what another section's screens act on is opened by a role holding either: the Finance
   * screen reports on the money of the orders section, so a role that may already see the orders may read the
   * books, and a role granted the finance menu on its own may open it without the order screens. The API checks
   * the same pair ('AdminPolicies.OrdersOrFinance'), from the same role to menu mapping.
   */
  canOpenAnySection(...sections: string[]): boolean {
    return sections.some(section => this.canOpenSection(section));
  }

  /** True when the admin can open at least one section of the admin area. */
  hasAnySection(): boolean {
    return this.sections.size > 0;
  }

  /** Role names carried by the token. */
  getRoleNames(): string[] {
    return this.authService.getRoleNames();
  }

  /** '/orders/10011' -> '/orders'. */
  static sectionOf(path: string): string {
    const segments = (path ?? '').split('/').filter(segment => segment.length > 0);
    return segments.length === 0 ? '' : '/' + segments[0];
  }

  /** Drops the cached menus so the next admin gets a fresh list. */
  reset(): void {
    this.menusRequest$ = undefined;
    this.setMenus([]);
  }

  private setMenus(menus: AdminMenu[]): void {
    this.menus = menus ?? [];
    this.sections = new Set<string>();
    this.collect(this.menus);
  }

  private collect(menus: AdminMenu[]): void {
    menus.forEach(menu => {
      if (menu.menuUrl) {
        this.sections.add(menu.menuUrl);
      }
      if (menu.children?.length) {
        this.collect(menu.children);
      }
    });
  }
}
