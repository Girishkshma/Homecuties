import { Component, OnInit, OnDestroy, Inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { GuestTransferService } from '../services/guest-transfer.service';

// Google Identity Services type
declare const google: any;

// Facebook SDK type
declare const FB: any;

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  standalone: false,
  styleUrl: './login.component.scss'
})
export class LoginComponent implements OnInit, OnDestroy {
  email = '';
  password = '';
  isLoading = false;
  errorMessage = '';
  /** Page the guest was trying to reach (e.g. /checkout); set by AuthGuard. */
  returnUrl = '';
  private isBrowser: boolean;

  constructor(
    private authService: AuthService,
    private guestTransfer: GuestTransferService,
    private route: ActivatedRoute,
    private router: Router,
    @Inject(PLATFORM_ID) private platformId: Object
  ) {
    this.isBrowser = isPlatformBrowser(this.platformId);
  }

  ngOnInit(): void {
    this.returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') || '';

    if (this.isBrowser) {
      this.loadGoogleScript();
      this.loadFacebookScript();
    }
  }

  ngOnDestroy(): void {
    // Cleanup if needed
  }

  login(): void {
    if (!this.email || !this.password) {
      this.errorMessage = 'Please enter email and password.';
      return;
    }
    this.isLoading = true;
    this.errorMessage = '';

    this.authService.login(this.email, this.password).subscribe({
      next: (response) => {
        this.isLoading = false;
        if (response.Result === 1 && response.Customer) {
          // Keep the signed token alongside the customer - the API requires it on order calls.
          this.authService.setSession(response.Customer, response.Token);

          // Move the guest cart + wishlist onto the account, then continue to the page the
          // customer was heading for (e.g. /checkout) or the home page.
          this.guestTransfer.transferGuestDataTo(response.Customer.CustomerID).subscribe({
            complete: () => this.redirectAfterLogin(),
            error: () => this.redirectAfterLogin()
          });
        } else {
          this.errorMessage = response.Messages?.[0] || 'Login failed. Please try again.';
        }
      },
      error: (err) => {
        this.isLoading = false;
        this.errorMessage = 'An error occurred. Please try later.';
        console.error('Login error:', err);
      }
    });
  }

  /** Back to the page the customer wanted (e.g. /checkout), otherwise the home page. */
  private redirectAfterLogin(): void {
    this.router.navigateByUrl(this.returnUrl || '/');
  }

  // NOTE: not reachable from the UI (the buttons are hidden in login.component.html) until the API
  // verifies the social token signature - see GoogleLoginService.
  loginWithGoogle(): void {
    this.isLoading = true;
    this.errorMessage = '';

    try {
      if (typeof google !== 'undefined' && google.accounts && google.accounts.id) {
        google.accounts.id.initialize({
          client_id: 'YOUR_GOOGLE_CLIENT_ID.apps.googleusercontent.com',
          callback: (response: any) => this.handleGoogleResponse(response)
        });
        google.accounts.id.prompt();
      } else {
        // Fallback: redirect-based OAuth
        const clientId = 'YOUR_GOOGLE_CLIENT_ID.apps.googleusercontent.com';
        const redirectUri = window.location.origin + '/login';
        const scope = 'email profile';
        const authUrl = `https://accounts.google.com/o/oauth2/v2/auth?client_id=${clientId}&redirect_uri=${redirectUri}&response_type=id_token&scope=${scope}`;
        window.location.href = authUrl;
      }
    } catch (err) {
      this.errorMessage = 'Google sign-in failed. Please try again.';
      this.isLoading = false;
      console.error('Google login error:', err);
    }
  }

  private handleGoogleResponse(response: any): void {
    if (response && response.credential) {
      this.authService.validateGoogleToken(response.credential).subscribe({
        next: (result) => {
          console.log('Google login success:', result);
          // TODO: Handle successful login (store token, redirect)
          this.isLoading = false;
        },
        error: (err) => {
          this.errorMessage = 'Google sign-in validation failed.';
          this.isLoading = false;
          console.error(err);
        }
      });
    } else {
      this.isLoading = false;
    }
  }

  loginWithFacebook(): void {
    this.isLoading = true;
    this.errorMessage = '';

    try {
      if (typeof FB !== 'undefined') {
        FB.login((response: any) => {
          if (response.authResponse) {
            const accessToken = response.authResponse.accessToken;
            const userId = response.authResponse.userID;
            this.authService.validateFacebookToken(userId, accessToken).subscribe({
              next: (result) => {
                console.log('Facebook login success:', result);
                // TODO: Handle successful login (store token, redirect)
                this.isLoading = false;
              },
              error: (err) => {
                this.errorMessage = 'Facebook sign-in validation failed.';
                this.isLoading = false;
                console.error(err);
              }
            });
          } else {
            this.errorMessage = 'Facebook sign-in was cancelled.';
            this.isLoading = false;
          }
        }, { scope: 'email,public_profile' });
      } else {
        this.errorMessage = 'Facebook SDK is not loaded. Please try again.';
        this.isLoading = false;
      }
    } catch (err) {
      this.errorMessage = 'Facebook sign-in failed. Please try again.';
      this.isLoading = false;
      console.error('Facebook login error:', err);
    }
  }

  private loadGoogleScript(): void {
    if (typeof google !== 'undefined' && google.accounts) {
      return; // Already loaded
    }
    const script = document.createElement('script');
    script.src = 'https://accounts.google.com/gsi/client';
    script.async = true;
    script.defer = true;
    document.body.appendChild(script);
  }

  private loadFacebookScript(): void {
    if (typeof FB !== 'undefined') {
      return; // Already loaded
    }
    const script = document.createElement('script');
    script.src = 'https://connect.facebook.net/en_US/sdk.js';
    script.async = true;
    script.defer = true;
    script.onload = () => {
      (window as any).fbAsyncInit = function() {
        const fb = (window as any).FB;
        if (fb) {
          fb.init({
            appId: 'YOUR_FACEBOOK_APP_ID',
            cookie: true,
            xfbml: true,
            version: 'v18.0'
          });
        }
      };
    };
    document.body.appendChild(script);
  }
}
