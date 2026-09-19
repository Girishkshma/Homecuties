import {
  Component,
  OnInit,
  AfterViewInit,
  OnDestroy,
  ChangeDetectorRef,
  ElementRef,
  Inject,
  PLATFORM_ID,
  ViewChild
} from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { GuestTransferService } from '../services/guest-transfer.service';
import { environment } from '../../environments/environment';

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
export class LoginComponent implements OnInit, AfterViewInit, OnDestroy {
  email = '';
  password = '';
  isLoading = false;
  errorMessage = '';
  /** Page the guest was trying to reach (e.g. /checkout); set by AuthGuard. */
  returnUrl = '';

  /** Google OAuth client id; when it is not configured the Google button is not shown. */
  readonly googleClientId = environment.googleClientId;
  /** True once Google Identity Services has loaded and its button has been rendered. */
  googleReady = false;

  /** Slot Google Identity Services renders its official button into. */
  @ViewChild('googleButton') private googleButtonRef?: ElementRef<HTMLDivElement>;

  private isBrowser: boolean;

  constructor(
    private authService: AuthService,
    private guestTransfer: GuestTransferService,
    private route: ActivatedRoute,
    private router: Router,
    private cdr: ChangeDetectorRef,
    @Inject(PLATFORM_ID) private platformId: Object
  ) {
    this.isBrowser = isPlatformBrowser(this.platformId);
  }

  ngOnInit(): void {
    this.returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') || '';

    if (this.isBrowser) {
      this.loadFacebookScript();
    }
  }

  /**
   * Google Identity Services is initialized after the login card is rendered so its official
   * button can be measured and drawn into the slot. Skipped on the server (SSR).
   */
  async ngAfterViewInit(): Promise<void> {
    if (!this.isBrowser || !this.googleClientId) {
      return;
    }

    const loaded = await this.loadGoogleScript();
    if (!loaded) {
      console.warn('Google Identity Services could not be loaded - Google sign-in is hidden.');
      return;
    }

    // Reveal the slot first so it has a width to measure, then render Google's button into it.
    this.googleReady = true;
    this.cdr.detectChanges();
    this.renderGoogleButton();
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

  /**
   * Continues after a Google sign-in: customers whose account has no internal password yet are
   * sent to the "set your password" step first (they can skip it), everyone else goes straight to
   * the page they were heading for.
   */
  private continueAfterGoogleSignIn(requiresPasswordSetup: boolean): void {
    if (requiresPasswordSetup) {
      this.router.navigate(['/set-password'], {
        queryParams: this.returnUrl ? { returnUrl: this.returnUrl } : {}
      });
      return;
    }

    this.redirectAfterLogin();
  }

  /**
   * Draws Google's official Sign-In button (Google Identity Services) into the slot. Using the
   * rendered button (instead of a hand-made one) is what makes the popup / FedCM account chooser
   * reliable, and it is the only widget Google still supports for this flow.
   */
  private renderGoogleButton(): void {
    const container = this.googleButtonRef?.nativeElement;
    if (!container || typeof google === 'undefined' || !google.accounts?.id) {
      return;
    }

    google.accounts.id.initialize({
      client_id: this.googleClientId,
      callback: (response: any) => this.handleGoogleCredential(response?.credential)
    });

    const width = Math.max(200, Math.min(400, Math.floor(container.clientWidth || 320)));

    google.accounts.id.renderButton(container, {
      type: 'standard',
      theme: 'outline',
      size: 'large',
      text: 'continue_with',
      shape: 'rectangular',
      logo_alignment: 'left',
      width
    });
  }

  /**
   * Called by Google with the ID token (JWT) once the customer picked a Google account.
   * The token is verified by the API before the customer is signed in - never trust it here.
   */
  private handleGoogleCredential(credential?: string): void {
    if (!credential) {
      this.errorMessage = 'Google sign-in was cancelled. Please try again.';
      return;
    }

    this.isLoading = true;
    this.errorMessage = '';

    this.authService.validateGoogleToken(credential).subscribe({
      next: (response) => {
        if (response?.Result === 1 && response.Customer) {
          // Same session + guest transfer flow as the email/password form.
          this.authService.setSession(response.Customer, response.Token);

          // A brand new Google account has no internal password yet - the API flags that so the
          // customer is asked to set one before continuing.
          const requiresPasswordSetup = response.RequiresPasswordSetup === true;

          this.guestTransfer.transferGuestDataTo(response.Customer.CustomerID).subscribe({
            complete: () => this.continueAfterGoogleSignIn(requiresPasswordSetup),
            error: () => this.continueAfterGoogleSignIn(requiresPasswordSetup)
          });
        } else {
          this.isLoading = false;
          this.errorMessage = response?.Messages?.[0] || 'Google sign-in failed. Please try again.';
        }
      },
      error: (err) => {
        this.isLoading = false;
        this.errorMessage = 'Google sign-in failed. Please try again.';
        console.error('Google login error:', err);
      }
    });
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

  /**
   * Injects the Google Identity Services client once and resolves when it is actually usable.
   * Reuses a tag that already exists (e.g. after a route change back to the login page).
   */
  private loadGoogleScript(): Promise<boolean> {
    if (typeof google !== 'undefined' && google.accounts?.id) {
      return Promise.resolve(true);
    }

    const existing = document.querySelector<HTMLScriptElement>('script[src="https://accounts.google.com/gsi/client"]');
    if (existing) {
      return new Promise<boolean>((resolve) => {
        existing.addEventListener('load', () => resolve(typeof google !== 'undefined' && !!google.accounts?.id));
        existing.addEventListener('error', () => resolve(false));
      });
    }

    return new Promise<boolean>((resolve) => {
      const script = document.createElement('script');
      script.src = 'https://accounts.google.com/gsi/client';
      script.async = true;
      script.defer = true;
      script.onload = () => resolve(typeof google !== 'undefined' && !!google.accounts?.id);
      script.onerror = () => resolve(false);
      document.body.appendChild(script);
    });
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
