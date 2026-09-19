import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CustomerService } from '../services/customer.service';

/**
 * Shown right after a first Google sign-in (or any sign-in by an account that has no internal
 * password): the customer already has a session, and this step lets them set a password so they
 * can also sign in with e-mail/password - and use "Forgot password" - later.
 */
@Component({
  selector: 'app-set-password',
  templateUrl: './set-password.component.html',
  standalone: false,
  styleUrl: './set-password.component.scss'
})
export class SetPasswordComponent implements OnInit {
  newPassword = '';
  confirmPassword = '';
  isLoading = false;
  errorMessage = '';
  successMessage = '';
  /** Page the customer was heading for before signing in. */
  returnUrl = '';

  constructor(
    private customerService: CustomerService,
    private route: ActivatedRoute,
    private router: Router
  ) { }

  ngOnInit(): void {
    this.returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') || '';
  }

  savePassword(): void {
    this.errorMessage = '';
    this.successMessage = '';

    if (!this.newPassword) {
      this.errorMessage = 'Please enter a new password.';
      return;
    }

    if (this.newPassword.length < 6) {
      this.errorMessage = 'Password must be at least 6 characters long.';
      return;
    }

    if (this.newPassword !== this.confirmPassword) {
      this.errorMessage = 'Passwords do not match.';
      return;
    }

    this.isLoading = true;

    this.customerService.setPassword(this.newPassword).subscribe({
      next: (response) => {
        this.isLoading = false;
        if (response.Result === 1) {
          this.successMessage = response.Messages?.[0] || 'Password set successfully!';
          setTimeout(() => this.continue(), 1500);
        } else {
          this.errorMessage = response.Messages?.[0] || 'Failed to set your password. Please try again.';
        }
      },
      error: (err) => {
        this.isLoading = false;
        this.errorMessage = err?.status === 401
          ? 'Your session has expired. Please sign in again.'
          : 'An error occurred. Please try again later.';
        console.error('Set password error:', err);
      }
    });
  }

  /** Continues without a password - the account still works through Google sign-in. */
  skip(): void {
    this.continue();
  }

  /** Back to the page the customer wanted (e.g. /checkout), otherwise the home page. */
  private continue(): void {
    this.router.navigateByUrl(this.returnUrl || '/');
  }
}
