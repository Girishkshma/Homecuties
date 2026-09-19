import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { AdminService } from '../../services/admin.service';
import { AuthService } from '../../services/auth.service';
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

  constructor(
    private adminService: AdminService,
    private authService: AuthService,
    public router: Router
  ) {}

  ngOnInit(): void {
    this.user = this.authService.getUser();
    this.loadMenus();
  }

  loadMenus(): void {
    const user = this.authService.getUser();
    if (user && user.roles.length > 0) {
      this.adminService.getMenus(user.roles[0].roleId).subscribe({
        next: (menus) => {
          // Dashboard always first, then alphabetical
          this.menus = menus.sort((a, b) => {
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
    this.authService.logout();
  }
}
