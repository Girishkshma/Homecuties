import { Component, OnInit, Inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { CartService } from '../services/cart.service';
import { AuthService } from '../services/auth.service';
import { PaymentService, CreateOrderResponse, OrderActionResult } from '../services/payment.service';
import { UtilityService } from '../services/utility.service';
import { Router } from '@angular/router';

declare var Razorpay: any;

@Component({
  selector: 'app-checkout',
  templateUrl: './checkout.component.html',
  standalone: false,
  styleUrl: './checkout.component.scss'
})
export class CheckoutComponent implements OnInit {
  /**
   * Key under which the order whose payment is in flight is remembered. Without it a customer who
   * paid and then lost the Razorpay callback (closed the window, reloaded, lost the connection) saw
   * the button spin on 'Processing...' forever, because only the callback ever cleared it.
   */
  private static readonly PendingOrderKey = 'hcPendingOrderId';

  cartItems: any[] = [];
  loading = true;
  placingOrder = false;
  error = '';
  orderSuccess = false;
  orderNumber = '';

  /** True while Razorpay Checkout has handed the payment over and a result is still on its way. */
  paymentInFlight = false;

  /**
   * True once Razorpay reported a payment result. Razorpay also raises 'dismiss' after a successful
   * payment, so a dismissal is only a cancellation while no result has been seen at all.
   */
  private paymentAttempted = false;

  /** The order the current payment belongs to - lets the customer jump to it when a check fails. */
  lastOrderId = 0;

  // Stock validation
  removedItems: string[] = [];
  adjustedItems: string[] = [];
  showStockWarning = false;

  // Shipping form
  shippingAddress = '';
  city = '';
  state = '';
  zipCode = '';
  phoneNumber = '';
  email = '';

  // Payment
  paymentMethod = 'razorpay';

  UtilityService = UtilityService;

  private isBrowser: boolean;

  constructor(
    private cartService: CartService,
    private authService: AuthService,
    private paymentService: PaymentService,
    private router: Router,
    @Inject(PLATFORM_ID) private platformId: Object
  ) {
    this.isBrowser = isPlatformBrowser(this.platformId);
  }

  ngOnInit(): void {
    this.loadCart();
    this.prefillCustomerInfo();
  }

  /**
   * Finishes a payment the browser never got the result of. The order id is remembered before
   * Razorpay Checkout opens, so coming back to this page (with an empty cart - see 'loadCart') asks
   * the backend to look the payment up and settle the order: the customer never gets stuck on
   * 'Processing...', and the payment is confirmed even when the callback never reached us.
   */
  private resumePendingPayment(): void {
    const pendingOrderId = this.getPendingOrderId();
    if (!pendingOrderId) {
      return;
    }

    this.lastOrderId = pendingOrderId;
    this.syncPayment(pendingOrderId);
  }

  private getPendingOrderId(): number {
    const stored = this.isBrowser ? localStorage.getItem(CheckoutComponent.PendingOrderKey) : null;
    return stored ? Number(stored) : 0;
  }

  private rememberPendingOrder(orderId: number): void {
    this.lastOrderId = orderId;

    if (this.isBrowser) {
      localStorage.setItem(CheckoutComponent.PendingOrderKey, String(orderId));
    }
  }

  private clearPendingOrder(): void {
    if (this.isBrowser) {
      localStorage.removeItem(CheckoutComponent.PendingOrderKey);
    }
  }

  private prefillCustomerInfo(): void {
    const customer = this.authService.getCurrentCustomer();
    if (customer) {
      this.email = customer.EmailId || '';
      this.phoneNumber = customer.MobileNumber || '';
    }
  }

  private getCustomerInfo(): { customerId: number; isGuest: boolean } {
    const customer = this.authService.getCurrentCustomer();
    if (customer) {
      return { customerId: customer.CustomerID, isGuest: false };
    }

    // Guests: reuse the guest customer id created when the item was added to the cart.
    // Sending 0 here would look up a different (empty) cart and fail with "Cart is empty".
    return { customerId: this.cartService.getStoredGuestCustomerId(), isGuest: true };
  }

  private loadCart(): void {
    const { customerId, isGuest } = this.getCustomerInfo();
    this.cartService.getCart(customerId, isGuest).subscribe({
      next: (data) => {
        this.cartItems = data.Items || [];
        this.loading = false;

        // Check for out-of-stock items and show warning
        this.validateCartStock();

        // Nothing left to check out: the customer has most likely just come back from a payment that
        // never reported back, so finish that order before showing them an empty checkout page.
        if (this.getFilteredCartItems().length === 0) {
          this.resumePendingPayment();
        }
      },
      error: (err) => {
        this.error = 'Failed to load cart. Please try again.';
        this.loading = false;
        console.error(err);
      }
    });
  }

  private validateCartStock(): void {
    const outOfStock: string[] = [];
    const adjusted: string[] = [];

    for (const item of this.cartItems) {
      if (!item.IsInStock) {
        outOfStock.push(item.ProductTitle);
      } else if (item.AvailableQty > 0 && item.Quantity > item.AvailableQty) {
        adjusted.push(`${item.ProductTitle} (only ${item.AvailableQty} available)`);
      }
    }

    this.removedItems = outOfStock;
    this.adjustedItems = adjusted;
    this.showStockWarning = outOfStock.length > 0 || adjusted.length > 0;
  }

  getFilteredCartItems(): any[] {
    return this.cartItems.filter((item: any) => item.IsInStock);
  }

  /**
   * True while the page is settling an earlier payment (see 'resumePendingPayment'): with an empty
   * cart this is the only thing the page is busy with, so it shows 'Checking your payment...'.
   */
  get checkingPayment(): boolean {
    return this.placingOrder && !this.orderSuccess && this.getFilteredCartItems().length === 0;
  }

  getSubtotal(): number {
    return this.getFilteredCartItems().reduce((sum: number, item: any) => sum + (item.Price * item.Quantity), 0);
  }

  isFormValid(): boolean {
    return this.shippingAddress.trim().length > 0 &&
      this.city.trim().length > 0 &&
      this.state.trim().length > 0 &&
      this.zipCode.trim().length > 0 &&
      this.phoneNumber.trim().length >= 10 &&
      this.email.trim().length > 0;
  }

  placeOrder(): void {
    if (!this.isFormValid()) {
      this.error = 'Please fill in all required fields.';
      return;
    }

    this.placingOrder = true;
    this.error = '';

    const { customerId, isGuest } = this.getCustomerInfo();

    this.paymentService.createOrder({
      CustomerID: customerId,
      IsGuest: isGuest,
      ShippingAddress: this.shippingAddress,
      City: this.city,
      State: this.state,
      ZipCode: this.zipCode,
      PhoneNumber: this.phoneNumber,
      Email: this.email,
      PaymentMethod: this.paymentMethod
    }).subscribe({
      next: (response: CreateOrderResponse) => {
        if (response.Result === 1) {
          // Store any removed/adjusted items info from the backend response
          if (response.RemovedItems && response.RemovedItems.length > 0) {
            this.removedItems = response.RemovedItems;
          }
          if (response.AdjustedItems && response.AdjustedItems.length > 0) {
            this.adjustedItems = response.AdjustedItems;
          }
          this.openRazorpayCheckout(response);
        } else {
          this.error = response.Messages?.join(', ') || 'Failed to create order.';
          this.placingOrder = false;
        }
      },
      error: (err) => {
        this.error = 'Failed to create order. Please try again.';
        this.placingOrder = false;
        console.error(err);
      }
    });
  }

  private openRazorpayCheckout(orderData: CreateOrderResponse): void {
    // Remember the order BEFORE the window opens: if the callback never reaches us, the next visit
    // to this page asks the backend to settle the payment (see resumePendingPayment).
    this.rememberPendingOrder(orderData.OrderId);

    // A fresh window means a fresh payment result - a dismissal of THIS window may be a cancellation.
    this.paymentAttempted = false;

    const options = {
      key: orderData.RazorpayKey,
      amount: orderData.Amount * 100, // Amount in paise
      currency: 'INR',
      name: 'Homecuties',
      description: `Order #${orderData.OrderNumber}`,
      order_id: orderData.RazorpayOrderId,
      prefill: {
        name: this.authService.getCurrentCustomer()?.FirstName || '',
        email: this.email,
        contact: this.phoneNumber
      },
      theme: {
        color: '#d4a373'
      },
      handler: (paymentResponse: any) => {
        // Payment successful - verify on backend
        this.paymentAttempted = true;
        this.paymentInFlight = true;
        this.verifyPayment(orderData.OrderId, paymentResponse);
      },
      modal: {
        ondismiss: () => {
          // Razorpay also reports 'dismiss' when it closes after a successful payment, so this is
          // only treated as a cancellation while no payment result has arrived.
          if (this.paymentAttempted || this.paymentInFlight || this.orderSuccess) {
            return;
          }

          this.placingOrder = false;
          this.clearPendingOrder();
          this.error = 'Payment cancelled. Your order has been saved - you can complete the payment from My Orders.';
        }
      }
    };

    // The checkout widget is loaded from Razorpay's CDN (index.html); when a network policy blocks it
    // the button would otherwise spin on 'Processing...' for ever.
    if (typeof Razorpay === 'undefined') {
      this.failOrder(
        orderData.OrderId,
        'The payment window could not be opened. Please check your connection and try again.'
      );
      return;
    }

    try {
      const razorpay = new Razorpay(options);
      razorpay.on('payment.failed', (response: any) => {
        // A 'failed' event is not always the last word (the money can still be captured), so the
        // backend is asked to check the payment before the button is released.
        this.paymentAttempted = true;
        this.paymentInFlight = true;
        this.error = `Payment failed: ${response.error.description || 'Please try again.'}`;
        this.syncPayment(orderData.OrderId, this.error);
      });
      razorpay.open();
    } catch (err) {
      console.error(err);
      this.failOrder(
        orderData.OrderId,
        'The payment window could not be opened. Please refresh the page and try again.'
      );
    }
  }

  private verifyPayment(orderId: number, paymentResponse: any): void {
    this.paymentService.verifyPayment({
      OrderId: orderId,
      RazorpayPaymentId: paymentResponse.razorpay_payment_id,
      RazorpayOrderId: paymentResponse.razorpay_order_id,
      RazorpaySignature: paymentResponse.razorpay_signature
    }).subscribe({
      next: (result) => {
        if (result.Result === 1) {
          this.completeOrder(orderId);
          return;
        }

        // Verification could not confirm the order (failed signature, capture not settled yet, ...).
        // Ask the backend to look the payment up at Razorpay before telling the customer anything:
        // the money may well have been taken.
        this.syncPayment(orderId, result.Messages?.join(', ') || 'Payment verification failed.');
      },
      error: (err) => {
        console.error(err);
        this.syncPayment(orderId, 'We could not confirm the payment with the server.');
      }
    });
  }

  /**
   * Safety net for every case in which the browser did not learn the payment result: the backend
   * checks the payment at Razorpay and confirms the order when the money was captured. Either way
   * the spinner is stopped - a payment is never left 'Processing...'.
   */
  private syncPayment(orderId: number, fallbackMessage?: string): void {
    this.placingOrder = true;

    this.paymentService.syncPayment(orderId).subscribe({
      next: (result: OrderActionResult) => {
        if (result.Result === 1) {
          this.completeOrder(orderId);
          return;
        }

        this.failOrder(orderId, result.Messages?.join(' ') || fallbackMessage || 'The payment could not be confirmed.');
      },
      error: (err) => {
        console.error(err);
        this.failOrder(
          orderId,
          `${fallbackMessage || 'We could not check the payment.'} Order ${this.orderNumberFor(orderId)} is saved in 'My Orders', ` +
          'where you can check its status or cancel it.'
        );
      }
    });
  }

  private completeOrder(orderId: number): void {
    this.orderSuccess = true;
    this.orderNumber = this.orderNumberFor(orderId);
    this.placingOrder = false;
    this.paymentInFlight = false;
    this.error = '';
    this.clearPendingOrder();
    this.cartService.clearLocalCart();
  }

  private failOrder(orderId: number, message: string): void {
    this.placingOrder = false;
    this.paymentInFlight = false;
    this.lastOrderId = orderId;
    this.clearPendingOrder();
    this.error = message;
  }

  private orderNumberFor(orderId: number): string {
    return `HC${orderId.toString().padStart(6, '0')}`;
  }

  continueShopping(): void {
    this.router.navigate(['/shop']);
  }

  viewMyOrders(): void {
    this.router.navigate(['/my-orders']);
  }
}
