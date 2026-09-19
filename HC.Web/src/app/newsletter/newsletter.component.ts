import { Component } from '@angular/core';

@Component({
  selector: 'app-newsletter',
  templateUrl: './newsletter.component.html',
  standalone: false,
  styleUrl: './newsletter.component.scss'
})
export class NewsletterComponent {
  email = '';
  submitted = false;
  submitting = false;
  message = '';

  subscribe(): void {
    if (!this.email || !this.email.includes('@')) {
      this.message = 'Please enter a valid email address.';
      return;
    }

    this.submitting = true;
    this.message = '';

    // TODO: Implement newsletter subscription API call
    setTimeout(() => {
      this.submitted = true;
      this.submitting = false;
      this.message = 'Thank you for subscribing!';
      this.email = '';
    }, 800);
  }
}
