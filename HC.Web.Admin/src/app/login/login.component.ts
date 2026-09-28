import { Component } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { AdminService } from '../services/admin.service';
import { AuthService } from '../services/auth.service';
import { PermissionsService } from '../services/permissions.service';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss'],
  standalone: false
})
export class LoginComponent {
  loginId = '';
  password = '';
  errorMessage = '';
  isLoading = false;

  constructor(
    private adminService: AdminService,
    private authService: AuthService,
    private permissionsService: PermissionsService,
    private router: Router,
    private route: ActivatedRoute
  ) {
    if (this.authService.isLoggedIn()) {
      this.router.navigate(['/dashboard']);
      return;
    }

    if (this.route.snapshot.queryParamMap.get('sessionExpired') === 'true') {
      this.errorMessage = 'Your session has ended. Please sign in again.';
    }
  }

  onSubmit(): void {
    if (!this.loginId || !this.password) {
      this.errorMessage = 'Please enter login ID and password.';
      return;
    }

    this.isLoading = true;
    this.errorMessage = '';

    this.adminService.login({ loginId: this.loginId, password: this.password })
      .subscribe({
        next: (response) => {
          if (response.result !== 1 || !response.token || !response.user) {
            this.isLoading = false;
            this.errorMessage = response.messages?.[0] || 'Login failed. Please try again.';
            return;
          }

          this.authService.setToken(response.token);
          this.authService.setUser(response.user);
          this.permissionsService.reset();

          // The roles inside the token decide which sections may be opened - ask the API for them
          // before entering the shell, so an account without any access is told straight away
          // instead of landing on pages that would answer 403.
          this.permissionsService.loadMenus(true).subscribe({
            next: (menus) => {
              this.isLoading = false;

              if (menus && menus.length > 0) {
                const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
                this.router.navigate([returnUrl || '/dashboard']);
              } else {
                this.authService.clearSession();
                this.permissionsService.reset();
                this.errorMessage = 'Your account does not have access to any section of the admin area. Please ask a Super Admin to grant you access.';
              }
            },
            error: () => {
              this.isLoading = false;
              this.authService.clearSession();
              this.permissionsService.reset();
              this.errorMessage = 'Your account does not have access to the admin area. Please ask a Super Admin to grant you access.';
            }
          });
        },
        error: (err) => {
          this.isLoading = false;
          this.errorMessage = 'Unable to connect to server. Please try again.';
          console.error('Login error:', err);
        }
      });
  }
}
