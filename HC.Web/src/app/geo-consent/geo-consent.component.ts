import { Component } from '@angular/core';
import { VisitorLocationService } from '../services/visitor-location.service';

/**
 * The location question: one strip, once per visitor, at the foot of every page.
 *
 * It exists because a browser will not tell a page where its visitor is without that visitor's say-so, and
 * because the shop would rather ask plainly than take the answer. Two buttons and no hidden consent: 'Share my
 * location' asks the browser (which then puts its own question as well), and 'Not now' is answered with the same
 * shop as before - every page works, and each hit is placed by the address it came from.
 *
 * The strip is drawn only while the question is unanswered; who has answered, and what they answered, is
 * remembered by VisitorLocationService rather than here, so a visitor is asked once across the whole shop.
 */
@Component({
  selector: 'app-geo-consent',
  templateUrl: './geo-consent.component.html',
  standalone: false,
  styleUrl: './geo-consent.component.scss'
})
export class GeoConsentComponent {
  constructor(private visitorLocation: VisitorLocationService) {}

  /** Whether the question is still unanswered and the strip belongs on the page. */
  get isAsking(): boolean {
    return this.visitorLocation.isUndecided;
  }

  /** 'Share my location' - the visitor agreed, so the browser is asked for a fix. */
  share(): void {
    this.visitorLocation.grant();
  }

  /** 'Not now' - remembered, and nothing else about the visit changes. */
  decline(): void {
    this.visitorLocation.decline();
  }
}
