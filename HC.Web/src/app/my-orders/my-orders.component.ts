import { Component, NgZone, OnDestroy, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { CreateOrderResponse, MyOrder, OrderShipment, PaymentService, RefreshOrderShipments } from '../services/payment.service';
import { AuthService } from '../services/auth.service';
import { UtilityService } from '../services/utility.service';
import { PaymentWindowWatcher } from '../services/payment-window';

/** Razorpay Checkout, loaded from the CDN in 'index.html'. */
declare var Razorpay: any;

@Component({
  selector: 'app-my-orders',
  templateUrl: './my-orders.component.html',
  standalone: false,
  styleUrl: './my-orders.component.scss'
})
export class MyOrdersComponent implements OnInit, OnDestroy {
  orders: MyOrder[] = [];
  loading = true;
  error = '';
  notice = '';

  /** Order a cancel / payment check is running for - keeps the buttons of that card disabled. */
  busyOrderId = 0;

  /**
   * True once Razorpay reported a payment result for the window opened by 'Pay now'. Razorpay also
   * raises 'dismiss' after a successful payment, so a dismissal is only a cancellation while no result
   * has been seen at all.
   */
  private paymentAttempted = false;

  /** Notices the payment window being closed (see PaymentWindowWatcher). */
  private windowWatcher: PaymentWindowWatcher | null = null;

  /** Order the customer asked to cancel, waiting for the inline confirmation. */
  confirmingOrderId = 0;

  /** Order whose items / address / timeline are expanded. */
  expandedOrderId = 0;

  /** True while the courier pull behind 'Refresh parcel' is running - keeps the button from repeating. */
  trackingBusy = false;

  /**
   * What the courier pull had to say. It is kept apart from 'error'/'notice' because it is not the
   * result of anything the customer did: a courier that could not be reached leaves the parcel's last
   * known status on the card and only puts its sentence here, so the list still reads normally.
   */
  parcelNote = '';

  UtilityService = UtilityService;

  constructor(
    private paymentService: PaymentService,
    private authService: AuthService,
    private router: Router,
    private zone: NgZone
  ) {}

  ngOnInit(): void {
    const customer = this.authService.getCurrentCustomer();
    if (!customer || !customer.CustomerID) {
      this.router.navigate(['/login'], { queryParams: { returnUrl: '/my-orders' } });
      return;
    }

    this.loadOrders();
  }

  /** Nothing may keep watching the page once the customer has left it. */
  ngOnDestroy(): void {
    this.windowWatcher?.stop();
  }

  loadOrders(): void {
    this.loading = true;
    this.error = '';

    this.paymentService.getMyOrders().subscribe({
      next: (data) => {
        this.orders = data || [];
        this.loading = false;

        // The parcels are pulled once the orders are on screen, so opening the page can never be held
        // up by a courier - and the statuses the order already carries are shown straight away.
        this.trackParcels();
      },
      error: (err) => {
        // A 401 means the token was missing or expired - say so plainly instead of letting the
        // customer believe they simply have no orders (the interceptor signs them out as well).
        this.error = err?.status === 401
          ? 'Your sign-in has expired. Please sign in again to see your orders.'
          : 'We could not load your orders. Please try again in a moment.';
        this.loading = false;
        console.error(err);
      }
    });
  }

  /** Re-reads the list after an action, without wiping the message that explains what happened. */
  private refreshOrders(): void {
    this.paymentService.getMyOrders().subscribe({
      next: (data) => {
        this.orders = data || [];
      },
      error: (err) => console.error('Failed to refresh orders:', err)
    });
  }

  /**
   * Asks the backend what the courier now says about this customer's live parcels and folds the answer
   * into the cards. It runs when the page opens and when 'Refresh parcel' is tapped, and the backend
   * throttles it - a parcel asked about a moment ago, or one the courier has finished with, is simply
   * not in the answer, which leaves the status already on the card untouched.
   *
   * Nothing here can break the page: a courier that could not be reached answers with its own sentence
   * and answers nothing else, so the last known status stays on the card and the sentence is put beside
   * the list instead of into the page's error line.
   */
  trackParcels(): void {
    if (this.trackingBusy) {
      return;
    }

    this.trackingBusy = true;

    this.paymentService.refreshShipments().subscribe({
      next: (result: RefreshOrderShipments) => {
        this.trackingBusy = false;
        this.parcelNote = (result?.messages || []).join(' ');

        const fresh = result?.shipments || [];
        if (fresh.length === 0) {
          return;
        }

        // The answer carries only the parcels that were actually looked up (and only their own fields,
        // never the order), so each one is put onto the order it belongs to and every other order is
        // left exactly as it was read.
        this.orders = this.orders.map(order => {
          const lookup: OrderShipment | undefined = fresh.find(shipment => shipment.orderId === order.OrderId);
          return lookup ? { ...order, Shipment: lookup } : order;
        });
      },
      error: (err) => {
        this.trackingBusy = false;
        console.error('Failed to refresh parcel statuses:', err);
      }
    });
  }


  toggleDetails(order: MyOrder): void {
    this.expandedOrderId = this.expandedOrderId === order.OrderId ? 0 : order.OrderId;
  }

  isExpanded(order: MyOrder): boolean {
    return this.expandedOrderId === order.OrderId;
  }

  askToCancel(order: MyOrder): void {
    this.notice = '';
    this.confirmingOrderId = order.OrderId;
  }

  dismissCancel(): void {
    this.confirmingOrderId = 0;
  }

  /**
   * Cancels an order that is still with the shop: the backend puts the reserved units back into the
   * available pool so they can be sold again and, when the order was already paid, asks for the money
   * back. That request is not the refund itself - the shop team approves it, and the payment is then
   * sent back to the method it came from (see 'RefundPending') - so the customer is told the refund is
   * on its way rather than that it has already gone. Whether the cancellation is allowed is decided by
   * the order's status on the server (Pending or Confirmed - a shipped order is refused there as well),
   * so whatever the server answers is what the customer is told.
   */
  cancelOrder(order: MyOrder): void {
    if (this.busyOrderId) {
      return;
    }

    this.busyOrderId = order.OrderId;
    this.confirmingOrderId = 0;
    this.error = '';
    this.notice = '';

    this.paymentService.cancelOrder(order.OrderId).subscribe({
      next: (result) => {
        this.busyOrderId = 0;

        if (result.Result === 1) {
          this.notice = result.Messages?.join(' ') || `${order.OrderNumber} has been cancelled.`;
        } else {
          this.error = result.Messages?.join(' ') || 'We could not cancel this order.';
        }

        // Either way the order may have changed (cancelled or confirmed) - show the current state.
        this.refreshOrders();
      },
      error: (err) => {
        this.busyOrderId = 0;
        this.error = 'We could not cancel the order. Nothing was changed - please try again or contact support.';
        console.error(err);
      }
    });
  }

  /**
   * Asks the backend to look the order's payment up at Razorpay and confirm the order when the money
   * was captured. This is the customer's own way out of a payment that never reported back.
   */
  checkPaymentStatus(order: MyOrder): void {
    if (this.busyOrderId) {
      return;
    }

    this.busyOrderId = order.OrderId;
    this.error = '';
    this.notice = '';

    this.settlePayment(
      order.OrderId,
      order.OrderNumber,
      'We could not find a payment for this order.'
    );
  }
  /**
   * Gives the order a fresh payment attempt ('Pay now'), which is the way out of a payment that failed
   * or was never completed. The backend asks Razorpay whether an earlier attempt actually went through
   * before it starts this one, so an order whose money was already taken is only confirmed here - the
   * customer is never charged twice.
   */
  payNow(order: MyOrder): void {
    if (this.busyOrderId) {
      return;
    }

    this.busyOrderId = order.OrderId;
    this.error = '';
    this.notice = '';

    this.paymentService.retryPayment(order.OrderId).subscribe({
      next: (orderData: CreateOrderResponse) => {
        if (orderData.Result !== 1) {
          this.busyOrderId = 0;

          // The backend checks the earlier attempt before starting a new one. When that check found the
          // money, the order has just been confirmed - that is good news, not an error - and the list is
          // re-read so the customer sees the new status.
          if (orderData.AlreadySettled) {
            this.notice = orderData.Messages?.join(' ') || `The payment for ${order.OrderNumber} has been received.`;
            this.refreshOrders();
            return;
          }

          this.error = orderData.Messages?.join(' ') || 'We could not start the payment for this order.';
          return;
        }

        this.openRazorpayCheckout(orderData);
      },
      error: (err) => {
        this.busyOrderId = 0;
        this.error = 'We could not start the payment. You have not been charged - please try again in a moment.';
        console.error(err);
      }
    });
  }

  /**
   * Opens Razorpay Checkout for the attempt the backend has just created. The order is paid for in the
   * same window the checkout page uses, so the customer sees exactly what they already know.
   */
  private openRazorpayCheckout(orderData: CreateOrderResponse): void {
    // A fresh window means a fresh payment result - a dismissal of THIS window may be a cancellation.
    this.paymentAttempted = false;

    const orderNumber = orderData.OrderNumber || `HC${orderData.OrderId.toString().padStart(6, '0')}`;
    const customer = this.authService.getCurrentCustomer();

    // 'modal.ondismiss' below is the usual way to learn that the window was closed; this notices it as
    // well, because that callback is not raised by every way the window can go away.
    this.windowWatcher = new PaymentWindowWatcher(
      this.zone,
      () => this.paymentAttempted,
      () => this.onPaymentWindowDismissed(orderData.OrderId, orderNumber)
    );

    const options = {
      key: orderData.RazorpayKey,
      amount: orderData.Amount * 100, // Amount in paise
      currency: 'INR',
      name: 'Homecuties',
      description: `Order #${orderNumber}`,
      order_id: orderData.RazorpayOrderId,
      prefill: {
        name: customer?.FirstName || '',
        email: customer?.EmailId || '',
        contact: customer?.MobileNumber || ''
      },
      theme: {
        color: '#d4a373'
      },
      // The callbacks below are called by Razorpay's own script, outside the Angular zone, where a
      // state change does not start change detection - hence 'zone.run', without which the card would
      // keep saying 'Pay now' (and the list would keep its old statuses) although it is all over.
      handler: (paymentResponse: any) => this.zone.run(() => {
        this.paymentAttempted = true;
        this.verifyPayment(orderData.OrderId, orderNumber, paymentResponse);
      }),
      modal: {
        // Razorpay also reports a dismissal when it closes the window after a successful payment, so
        // 'onPaymentWindowDismissed' itself only acts while no payment result has arrived.
        ondismiss: () => this.onPaymentWindowDismissed(orderData.OrderId, orderNumber)
      }
    };

    // The window comes from Razorpay's CDN (index.html); when a network policy blocks it the button
    // would otherwise say nothing at all about why it did not open.
    if (typeof Razorpay === 'undefined') {
      this.busyOrderId = 0;
      this.error = 'The payment window could not be opened. Please check your connection and try again.';
      return;
    }

    try {
      const razorpay = new Razorpay(options);
      razorpay.on('payment.failed', (response: any) => this.zone.run(() => {
        // A failure report is not the last word - the money can still be captured - so the backend is
        // asked what really happened before the customer is told anything.
        this.paymentAttempted = true;
        this.settlePayment(
          orderData.OrderId,
          orderNumber,
          `Payment failed: ${response.error?.description || 'Please try again.'}`
        );
      }));

      this.windowWatcher.start();
      razorpay.open();
    } catch (err) {
      console.error(err);
      this.settlePayment(
        orderData.OrderId,
        orderNumber,
        'The payment window could not be opened. Please try again.'
      );
    }
  }

  /**
   * The payment window was closed without reporting a result. Both signals - Razorpay's own
   * 'ondismiss' and the window watch (see PaymentWindowWatcher) - report the same closure, so
   * whichever comes first is used and the other is ignored. The order keeps waiting for its payment,
   * which is what the list is re-read to show: the customer is never left with a card that looks like
   * it is still working.
   */
  private onPaymentWindowDismissed(orderId: number, orderNumber: string): void {
    this.windowWatcher?.stop();

    // A result is on its way, or this closure has already been handled: the card is released and the
    // order keeps waiting either way.
    if (this.paymentAttempted || this.busyOrderId !== orderId) {
      return;
    }

    this.busyOrderId = 0;
    this.notice = `${orderNumber} is still waiting for its payment - you can pay whenever you are ready.`;
    this.refreshOrders();
  }



  /** Confirms the attempt Razorpay just reported, then leaves the last word to the backend. */
  private verifyPayment(orderId: number, orderNumber: string, paymentResponse: any): void {
    this.paymentService.verifyPayment({
      OrderId: orderId,
      RazorpayPaymentId: paymentResponse.razorpay_payment_id,
      RazorpayOrderId: paymentResponse.razorpay_order_id,
      RazorpaySignature: paymentResponse.razorpay_signature
    }).subscribe({
      next: (result) => {
        if (result.Result === 1) {
          this.busyOrderId = 0;
          this.notice = `Thank you! The payment for ${orderNumber} has been received.`;
          this.refreshOrders();
          return;
        }

        // The signature can be missing or the capture not settled yet, and the money may well have been
        // taken - the payment is looked up at Razorpay before anything is claimed.
        this.settlePayment(
          orderId,
          orderNumber,
          result.Messages?.join(' ') || 'We could not confirm the payment.'
        );
      },
      error: (err) => {
        console.error(err);
        this.settlePayment(orderId, orderNumber, 'We could not confirm the payment with the server.');
      }
    });
  }

  /**
   * Asks the backend to look the order's payment up at Razorpay and confirm the order when the money
   * was captured. Whether the money was taken is decided by that lookup, never by the browser's own
   * view of events, and the card is released with the list re-read either way.
   */
  private settlePayment(orderId: number, orderNumber: string, fallbackMessage: string): void {
    this.paymentService.syncPayment(orderId).subscribe({
      next: (result) => {
        this.busyOrderId = 0;

        if (result.Result === 1) {
          this.notice = result.Messages?.join(' ') || `The payment for ${orderNumber} has been received.`;
        } else {
          this.error = result.Messages?.join(' ') || fallbackMessage;
        }

        // Either way the order may have changed (confirmed or still waiting) - show the current state.
        this.refreshOrders();
      },
      error: (err) => {
        this.busyOrderId = 0;
        this.error = `${fallbackMessage} You can check the payment again from this page.`;
        console.error(err);
        this.refreshOrders();
      }
    });
  }

  /**
   * True when the order carries an address worth showing. An older server build can answer without
   * one, and an empty 'Shipping Address' heading would only puzzle the customer.
   */
  hasShippingAddress(order: MyOrder): boolean {
    const address = order.ShippingAddress;
    if (!address) {
      return false;
    }

    return !!(
      address.ContactName ||
      address.AddressLine1 ||
      address.AddressLine2 ||
      address.City ||
      address.State ||
      address.Zipcode ||
      address.MobileNumber ||
      address.EmailId
    );
  }

  /**
   * True when the order was billed somewhere else than it is delivered, which is the only case worth
   * showing a billing address in: with the usual 'same as the delivery address' both point at the
   * same row (same AddressId), and repeating it would only be noise.
   */
  hasBillingAddress(order: MyOrder): boolean {
    const billing = order.BillingAddress;
    if (!billing || billing.AddressId === 0) {
      return false;
    }

    return billing.AddressId !== order.ShippingAddress.AddressId;
  }

  /**
   * True when the shop has recorded a parcel (an AWB) for this order - only then is anything said about
   * one, so a confirmed order that has not been handed to a courier yet stays quiet about shipping.
   */
  hasParcel(order: MyOrder): boolean {
    return !!order.Shipment && order.Shipment.hasShipment;
  }

  /**
   * True when a courier stands behind this parcel. False for the shop's own delivery arrangement (handed
   * over in person, or a local courier the shop deals with directly): there is nobody to ask, so no
   * tracking link and no 'Refresh parcel' are offered for it, and the card says where it stands on the
   * shop's own word instead. A parcel read from a server that does not report this counts as having a
   * courier, which is the safer mistake - the lookup then answers for itself.
   */
  parcelHasCourier(order: MyOrder): boolean {
    return order.Shipment?.reportsTracking !== false;
  }

  /**
   * What the parcel's number is called on the card: the courier's own AWB, or the reference the shop gave
   * the parcel itself when no courier stands behind it. The number is real either way - only the name has
   * to be honest about where it came from.
   */
  parcelNumberLabel(order: MyOrder): string {
    return this.parcelHasCourier(order) ? 'AWB' : 'Reference';
  }

  /**
   * Where the parcel is, in the clearest words available: the shop's own wording for what the courier
   * said, and - when the shop has none for it - the courier's own sentence. Written together so the line
   * is never empty while a parcel is on its way.
   */
  parcelStatusText(order: MyOrder): string {
    const shipment = order.Shipment;
    return shipment ? (shipment.status || shipment.providerStatus) : '';
  }

  /**
   * The courier's latest sentence, shown only when the shop has wording of its own for it. When the shop
   * has none, 'parcelStatusText' is already that sentence, and repeating it would read as two updates.
   */
  parcelCourierText(order: MyOrder): string {
    const shipment = order.Shipment;
    if (!shipment || !shipment.lastStatusText) {
      return '';
    }

    return shipment.lastStatusText === shipment.providerStatus ? '' : shipment.lastStatusText;
  }

  /** Bootstrap colour of the parcel badge, from the stage the server worked out (see ShipmentStatusFlow). */
  parcelBadgeClass(order: MyOrder): string {
    switch (order.Shipment?.stage) {
      case 'Booked': return 'bg-secondary';
      case 'InTransit': return 'bg-info text-dark';
      case 'OutForDelivery': return 'bg-primary';
      case 'Delivered': return 'bg-success';
      case 'Undelivered': return 'bg-warning text-dark';
      case 'Rto': return 'bg-dark';
      case 'Cancelled': return 'bg-danger';
      // Anything else is 'Unknown' - the courier has not said anything this side recognises yet.
      default: return 'bg-warning text-dark';
    }
  }

  /** The courier's own tracking page, when it reported one - else '' and no link is offered. */
  parcelTrackingUrl(order: MyOrder): string {
    return order.Shipment?.trackingUrl || '';
  }

  /**
   * True once the courier is finished with the parcel (delivered, on its way back, or called off). Said
   * out loud, because otherwise the customer keeps asking for news that is never going to change.
   *
   * Only for a parcel a courier really carries: one on the shop's own delivery arrangement reaches 'closed'
   * too, by following the order it belongs to (see parcelHasCourier), and telling the customer that 'the
   * courier has nothing more to report' would name a courier that never had anything to do with it.
   */
  isParcelClosed(order: MyOrder): boolean {
    return this.parcelHasCourier(order) && !!order.Shipment?.closed;
  }

  /** Bootstrap colour of the status badge (Orders.OrderStatusID: 1..5). */
  statusBadgeClass(order: MyOrder): string {
    switch (order.StatusId) {
      case 2: return 'bg-primary';
      case 3: return 'bg-info text-dark';
      case 4: return 'bg-success';
      case 5: return 'bg-danger';
      default: return 'bg-warning text-dark';
    }
  }

  /** A pending order may still have a payment at Razorpay that we have not seen yet. */
  canCheckPayment(order: MyOrder): boolean {
    return order.StatusId === 1;
  }

  /** True while the order still owes money - only those orders are offered a 'Pay now' button. */
  canPay(order: MyOrder): boolean {
    return order.CanPay;
  }

  /**
   * True while the order can still be cancelled from here: while it is with the shop (Pending or
   * Confirmed - the server decides and refuses everything else, so a wrong guess here only produces a
   * clear refusal). From 'Shipped' onwards the button is not offered at all.
   */
  canCancel(order: MyOrder): boolean {
    return order.CanCancel;
  }

  /**
   * What the cancel confirmation says. A paid order asks for its money back - the shop team approves
   * that refund, so the customer is promised the refund, not an immediate credit - an unpaid one simply
   * goes away. Either way the order is told before it is confirmed, because it cannot be brought back.
   */
  cancelConfirmText(order: MyOrder): string {
    return order.IsPaid
      ? `Cancel ${order.OrderNumber}? You have already paid for it, so the amount is refunded to the ` +
        'payment method you used. Asking for it places the refund with our team straight away; the ' +
        'money reaches your account once it has been approved and processed by the bank. The items go ' +
        'back on sale and the order cannot be restored - you would have to place a new one.'
      : `Cancel ${order.OrderNumber}? The reserved items go back on sale and the order cannot be ` +
        'restored - you would have to place a new one.';
  }

  /** True while the order is waiting for the money it was paid with to come back to the customer. */
  isRefundPending(order: MyOrder): boolean {
    return order.RefundPending;
  }

  /**
   * The note on a cancelled order whose refund has been placed but not sent yet: the cancellation asked
   * for the money back and the shop team approves it (the same note covers a refund the gateway refused,
   * which the team picks up and sends again), so the customer is told where the refund stands instead of
   * being left with a cancelled order and no word about their money.
   */
  refundNote(order: MyOrder): string {
    return `Your refund of ${UtilityService.currencyFormat(order.TotalAmount)} has been placed with our ` +
      'team and goes back to the payment method you used as soon as they approve it - this usually ' +
      'takes a few working days after the approval.';
  }

  /**
   * Why 'Cancel Order' is no longer offered. The status chain ends the customer's own cancellation at
   * 'Shipped' (the server refuses it there too), so the missing button is explained instead of leaving
   * the customer to guess.
   */
  cancelUnavailableNote(order: MyOrder): string {
    switch (order.StatusId) {
      case 3: // Shipped
        return 'This order has already been shipped, so it can no longer be cancelled here. Please ' +
          'contact support@homecuties.com and we will help you with a return.';
      case 4: // Delivered
        return 'This order has been delivered, so it can no longer be cancelled here. Please contact ' +
          'support@homecuties.com if something is wrong with it.';
      default:
        return '';
    }
  }
}
