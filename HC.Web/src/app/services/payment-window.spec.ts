import { NgZone } from '@angular/core';
import { fakeAsync, tick } from '@angular/core/testing';
import { PaymentWindowWatcher } from './payment-window';

/**
 * The overlay Razorpay Checkout builds when its window opens - a container holding the checkout frame,
 * visible while the window is up.
 */
function openRazorpayWindow(): HTMLElement {
  const container = document.createElement('div');
  container.className = 'razorpay-container';
  container.style.display = 'block';
  container.appendChild(document.createElement('iframe'));
  document.body.appendChild(container);

  return container;
}

/** What the widget does when the window closes: it hides its overlay (which stays in the page). */
function closeRazorpayWindow(container: HTMLElement): void {
  container.style.display = 'none';
}

/**
 * 'modal.ondismiss' is the documented way to hear that the customer closed the payment window, and the
 * pages rely on it - but a page whose customer closes the window and never hears about it keeps saying
 * 'Processing...' for ever, so the window is watched as well. These tests pin down that watch: it
 * reports a window it saw close, it never reports one it knows nothing about, and it leaves a payment
 * that is still being confirmed alone.
 */
describe('PaymentWindowWatcher', () => {
  afterEach(() => {
    document.querySelectorAll('.razorpay-container').forEach(container => container.remove());
  });

  it('should report a window that was closed without a payment result', fakeAsync(() => {
    const container = openRazorpayWindow();
    let closed = 0;
    let ranInsideAngularZone = false;

    const watcher = new PaymentWindowWatcher(
      new NgZone({}),
      () => false,
      () => {
        closed++;
        ranInsideAngularZone = NgZone.isInAngularZone();
      });

    watcher.start();
    tick(1000);

    // However long the customer takes, an open window is left alone.
    expect(closed).toBe(0);

    closeRazorpayWindow(container);
    tick(1000);

    // The window is gone, but the payment result gets its moment first: Razorpay closes the window
    // before it hands a successful payment over.
    expect(closed).toBe(0);

    tick(2000);

    expect(closed).toBe(1);

    // The page has to be told inside the Angular zone, or the button would keep saying 'Processing...'
    // even though the watcher did its job.
    expect(ranInsideAngularZone).toBeTrue();

    // And only once: the watch is over, so a long wait cannot report the same closure again.
    tick(5000);
    expect(closed).toBe(1);

    watcher.stop();
  }));

  it('should stay silent about a window that was never seen open', fakeAsync(() => {
    let closed = 0;
    const watcher = new PaymentWindowWatcher(new NgZone({}), () => false, () => closed++);

    watcher.start();
    tick(10000);

    // Nothing on the page looks like a payment window. Reporting a closure here would cancel a payment
    // this watch knows nothing about, which is worse than saying nothing.
    expect(closed).toBe(0);

    watcher.stop();
  }));

  it('should leave a payment alone once Razorpay reported a result', fakeAsync(() => {
    const container = openRazorpayWindow();
    let resultReported = false;
    let closed = 0;
    const watcher = new PaymentWindowWatcher(
      new NgZone({}),
      () => resultReported,
      () => closed++);

    watcher.start();
    tick(1000);

    // The payment went through: the widget closes its window and calls the success callback a moment
    // later. The window closing must not be mistaken for a cancellation.
    closeRazorpayWindow(container);
    resultReported = true;
    tick(5000);

    expect(closed).toBe(0);

    watcher.stop();
  }));
});
