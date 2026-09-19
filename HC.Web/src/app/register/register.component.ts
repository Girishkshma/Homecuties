import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CustomerService } from '../services/customer.service';
import { AuthService } from '../services/auth.service';
import { GuestTransferService } from '../services/guest-transfer.service';

@Component({
  selector: 'app-register',
  templateUrl: './register.component.html',
  standalone: false,
  styleUrl: './register.component.scss'
})
export class RegisterComponent implements OnInit {
  firstName = '';
  lastName = '';
  email = '';
  password = '';
  confirmPassword = '';
  isLoading = false;
  errorMessage = '';
  successMessage = '';
  /** Page the guest was trying to reach (e.g. /checkout); set by AuthGuard. */
  returnUrl = '';

  constructor(
    private customerService: CustomerService,
    private authService: AuthService,
    private guestTransfer: GuestTransferService,
    private route: ActivatedRoute,
    private router: Router
  ) { }

  ngOnInit(): void {
    this.returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') || '';
  }

  register(): void {
    this.errorMessage = '';
    this.successMessage = '';

    // Validate required fields
    if (!this.firstName || !this.lastName || !this.email || !this.password || !this.confirmPassword) {
      this.errorMessage = 'Please fill in all fields.';
      return;
    }

    // Validate email format
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(this.email)) {
      this.errorMessage = 'Please enter a valid email address.';
      return;
    }

    // Validate password length
    if (this.password.length < 6) {
      this.errorMessage = 'Password must be at least 6 characters long.';
      return;
    }

    // Validate passwords match
    if (this.password !== this.confirmPassword) {
      this.errorMessage = 'Passwords do not match.';
      return;
    }

    this.isLoading = true;

    this.customerService.createCustomer(this.firstName, this.lastName, this.email, this.password)
      .subscribe({
        next: (response) => {
          this.isLoading = false;
          if (response.Result === 1) {
            this.successMessage = 'Account created successfully! Signing you in...';

            if (response.Customer) {
              // The register API returns the new customer - sign them straight in.
              this.authService.setSession(response.Customer, response.Token);
              this.completeSignIn(response.Customer.CustomerID);
            } else {
              this.signInAndFinish();
            }
          } else {
            this.errorMessage = response.Messages?.[0] || 'Registration failed. Please try again.';
          }
        },
        error: (err) => {
          this.isLoading = false;
          this.errorMessage = 'An error occurred. Please try again later.';
          console.error('Registration error:', err);
        }
      });
  }

  /**
   * Signs the just-registered customer in, moves the guest cart + wishlist onto the new
   * account, then continues to the page they were heading for (e.g. /checkout).
   */
  private signInAndFinish(): void {
    this.authService.login(this.email, this.password).subscribe({
      next: (response) => {
        if (response.Result === 1 && response.Customer) {
          this.authService.setSession(response.Customer, response.Token);
          this.completeSignIn(response.Customer.CustomerID);
        } else {
          this.goToLogin();
        }
      },
      error: () => this.goToLogin()
    });
  }

  /** Moves the guest cart + wishlist onto the new account, then continues to the intended page. */
  private completeSignIn(customerId: number): void {
    this.guestTransfer.transferGuestDataTo(customerId).subscribe({
      complete: () => this.router.navigateByUrl(this.returnUrl || '/'),
      error: () => this.router.navigateByUrl(this.returnUrl || '/')
    });
  }

  /** Fallback when auto sign-in fails - keep the intended destination. */
  private goToLogin(): void {
    this.router.navigate(['/login'], {
      queryParams: this.returnUrl ? { returnUrl: this.returnUrl } : {}
    });
  }
}
