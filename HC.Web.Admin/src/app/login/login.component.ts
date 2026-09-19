import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { AdminService } from '../services/admin.service';
import { AuthService } from '../services/auth.service';

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
    private router: Router
  ) {
    if (this.authService.isLoggedIn()) {
      this.router.navigate(['/dashboard']);
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
          this.isLoading = false;
          if (response.result === 1 && response.token && response.user) {
            this.authService.setToken(response.token);
            this.authService.setUser(response.user);
            this.router.navigate(['/dashboard']);
          } else {
            this.errorMessage = response.messages?.[0] || 'Login failed. Please try again.';
          }
        },
        error: (err) => {
          this.isLoading = false;
          this.errorMessage = 'Unable to connect to server. Please try again.';
          console.error('Login error:', err);
        }
      });
  }
}
