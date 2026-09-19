import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { AdminService } from '../services/admin.service';

@Component({
  selector: 'app-forgot-password',
  templateUrl: './forgot-password.component.html',
  styleUrls: ['./forgot-password.component.scss'],
  standalone: false
})
export class ForgotPasswordComponent {
  loginId = '';
  isLoading = false;
  errorMessage = '';
  successMessage = '';
  resetToken = '';

  constructor(
    private adminService: AdminService,
    private router: Router
  ) { }

  forgotPassword(): void {
    this.errorMessage = '';
    this.successMessage = '';
    this.resetToken = '';

    if (!this.loginId) {
      this.errorMessage = 'Please enter your login ID.';
      return;
    }

    this.isLoading = true;

    this.adminService.forgotPassword(this.loginId).subscribe({
      next: (response) => {
        this.isLoading = false;
        if (response.result === 1) {
          const msg = response.messages?.[0] || '';
          const tokenMatch = msg.match(/Token: ([a-f0-9]+)/i);
          if (tokenMatch) {
            this.resetToken = tokenMatch[1];
          }
          this.successMessage = msg;
        } else {
          this.errorMessage = response.messages?.[0] || 'An error occurred. Please try again.';
        }
      },
      error: (err) => {
        this.isLoading = false;
        this.errorMessage = 'An error occurred. Please try again later.';
        console.error('Forgot password error:', err);
      }
    });
  }

  goToResetPassword(): void {
    if (this.resetToken) {
      this.router.navigate(['/reset-password', this.resetToken]);
    }
  }
}
