import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { PermissionsService } from '../../services/permissions.service';
import { AdminMenu, AdminUser } from '../../models/admin.model';

@Component({
  selector: 'app-sidebar',
  templateUrl: './sidebar.component.html',
  styleUrls: ['./sidebar.component.scss'],
  standalone: false
})
export class SidebarComponent implements OnInit {
  menus: AdminMenu[] = [];
  expandedMenus: Set<number> = new Set();
  user: AdminUser | null = null;
  /** End of the session, taken from the JWT the API issued on login. */
  sessionExpiresOn: Date | null = null;

  constructor(
    private authService: AuthService,
    private permissionsService: PermissionsService,
    public router: Router
  ) {}

  ngOnInit(): void {
    this.user = this.authService.getUser();
    this.sessionExpiresOn = this.authService.getExpiresOn();
    this.loadMenus();
  }

  /** The menus of the signed-in admin - the API derives them from the roles in its token. */
  loadMenus(): void {
    this.permissionsService.loadMenus().subscribe({
      next: (menus) => {
        // Dashboard always first, then alphabetical
        this.menus = [...menus].sort((a, b) => {
          if (a.menuTitle === 'Dashboard') return -1;
          if (b.menuTitle === 'Dashboard') return 1;
          return a.menuTitle.localeCompare(b.menuTitle);
        });
      },
      error: (err) => {
        console.error('Failed to load menus:', err);
      }
    });
  }

  toggleMenu(menuId: number): void {
    if (this.expandedMenus.has(menuId)) {
      this.expandedMenus.delete(menuId);
    } else {
      this.expandedMenus.add(menuId);
    }
  }

  isExpanded(menuId: number): boolean {
    return this.expandedMenus.has(menuId);
  }

  getMenuIcon(menuTitle: string): string {
    const iconMap: { [key: string]: string } = {
      'Products': '🛍️',
      'Orders': '📦',
      'Finance': '💰',
      'Customers': '👥',
      'Purchases': '🧾',
      'Partners': '🤝',
      'Vendors': '🏪',
      'Users': '👤',
      'Categories': '📁',
      'Settings': '⚙️',
      'Reports': '📈',
      'Content': '📝'
    };
    return iconMap[menuTitle] || '📋';
  }

  getInitials(firstName: string, lastName?: string): string {
    const first = firstName ? firstName.charAt(0).toUpperCase() : '';
    const last = lastName ? lastName.charAt(0).toUpperCase() : '';
    return first + last || 'U';
  }

  navigate(url: string): void {
    this.router.navigate([url]);
  }

  logout(): void {
    this.permissionsService.reset();
    this.authService.logout();
  }
}
