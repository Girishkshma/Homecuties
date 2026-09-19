import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { CustomerService } from '../services/customer.service';

@Component({
  selector: 'app-forgot-password',
  templateUrl: './forgot-password.component.html',
  standalone: false,
  styleUrl: './forgot-password.component.scss'
})
export class ForgotPasswordComponent {
  email = '';
  isLoading = false;
  errorMessage = '';
  successMessage = '';
  resetToken = '';

  constructor(
    private customerService: CustomerService,
    private router: Router
  ) { }

  forgotPassword(): void {
    this.errorMessage = '';
    this.successMessage = '';
    this.resetToken = '';

    if (!this.email) {
      this.errorMessage = 'Please enter your email address.';
      return;
    }

    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(this.email)) {
      this.errorMessage = 'Please enter a valid email address.';
      return;
    }

    this.isLoading = true;

    this.customerService.forgotPassword(this.email).subscribe({
      next: (response) => {
        this.isLoading = false;
        if (response.Result === 1) {
          // Extract token from message if present (for development purposes)
          const msg = response.Messages?.[0] || '';
          const tokenMatch = msg.match(/Token: ([a-f0-9]+)/i);
          if (tokenMatch) {
            this.resetToken = tokenMatch[1];
          }
          this.successMessage = msg;
        } else {
          this.errorMessage = response.Messages?.[0] || 'An error occurred. Please try again.';
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
