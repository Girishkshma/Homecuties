import { NgZone } from '@angular/core';

/**
 * Watches the Razorpay Checkout window for the page that opened it.
 *
 * 'modal.ondismiss' is the documented way to hear that the customer closed the payment window and is
 * the one used first - but it is raised by the widget's own code (`setTimeout` inside its iframe
 * bridge) and it is not the only way the window can go away: an interrupted UPI app, a frame an
 * extension blocked or a widget build that hides the modal without reporting it all leave the page
 * thinking a payment is still running.
 *
 * While its window is open, the widget keeps an overlay of its own in the page (a 'razorpay-container'
 * holding the checkout frame) and hides it the moment the window closes, so that overlay can be
 * watched as an independent backstop. The customer is then never left staring at 'Processing...' for
 * a window that is not there any more - and, just as important, a payment that IS being confirmed is
 * never turned into a cancellation: the watch only ever reports a closure it has seen happen, and only
 * after a grace period in which the payment result had its chance to arrive (Razorpay hides the window
 * first and hands the success callback over a moment later).
 */
export class PaymentWindowWatcher {
  /** The overlay the widget builds when the window opens and hides when it closes. */
  private static readonly OverlaySelector = '.razorpay-container';

  /** How often the overlay is looked for. The widget shows and hides it synchronously, so this is plenty. */
  private static readonly PollIntervalMs = 500;

  /**
   * How long the result is waited for after the window has gone. Razorpay closes the window before it
   * reports a successful payment, so without this a paid order would briefly look cancelled.
   */
  private static readonly ResultGraceMs = 1500;

  private poll: any = null;
  private closingCheck: any = null;
  private windowSeen = false;

  /**
   * @param zone the Angular zone the callback is handed back to (a timer must not run change detection)
   * @param hasResult true once Razorpay reported a payment result for this window - while that is the
   *        case the closure the watch sees is the widget tidying up, not a cancellation, and it stays
   *        quiet.
   * @param onClosed called once, inside the Angular zone, when the window closed without a result.
   */
  constructor(
    private readonly zone: NgZone,
    private readonly hasResult: () => boolean,
    private readonly onClosed: () => void
  ) {}

  /** Starts watching - call it right before the window is opened. */
  start(): void {
    this.stop();
    this.windowSeen = false;

    // Outside Angular: a poll twice a second must not run change detection.
    this.zone.runOutsideAngular(() => {
      this.poll = setInterval(() => this.checkWindow(), PaymentWindowWatcher.PollIntervalMs);
    });
  }

  /** Stops watching - the result arrived, the window was reported closed, or the page is going away. */
  stop(): void {
    if (this.poll) {
      clearInterval(this.poll);
      this.poll = null;
    }

    if (this.closingCheck) {
      clearTimeout(this.closingCheck);
      this.closingCheck = null;
    }
  }

  private checkWindow(): void {
    if (this.isWindowOpen()) {
      this.windowSeen = true;
      return;
    }

    // Only a window this watch has seen open may be reported as closed: should Razorpay ever change
    // how its overlay is built, the watch stays silent instead of cancelling a payment that is sitting
    // in front of the customer.
    if (!this.windowSeen || this.closingCheck) {
      return;
    }

    // The window is gone, but the result is not always in yet - it gets its moment first.
    this.closingCheck = setTimeout(() => {
      this.closingCheck = null;

      if (this.hasResult()) {
        this.stop();
        return;
      }

      this.stop();
      this.zone.run(this.onClosed);
    }, PaymentWindowWatcher.ResultGraceMs);
  }

  /** True while the widget's overlay is in the page and visible. */
  private isWindowOpen(): boolean {
    // The window only exists in a browser; without a document there is nothing to watch.
    if (typeof document === 'undefined') {
      return true;
    }

    const overlay = document.querySelector(PaymentWindowWatcher.OverlaySelector) as HTMLElement | null;

    // The widget hides the overlay (display: none) when the window closes and shows it again the next
    // time one is opened, so 'hidden' is as good as 'gone'.
    return !!overlay && overlay.style.display !== 'none';
  }
}
