import { Component, OnInit } from '@angular/core';
import { CartService } from '../services/cart.service';
import { AuthService } from '../services/auth.service';
import { PaymentService, CreateOrderResponse } from '../services/payment.service';
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
  cartItems: any[] = [];
  loading = true;
  placingOrder = false;
  error = '';
  orderSuccess = false;
  orderNumber = '';

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

  constructor(
    private cartService: CartService,
    private authService: AuthService,
    private paymentService: PaymentService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadCart();
    this.prefillCustomerInfo();
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
        this.verifyPayment(orderData.OrderId, paymentResponse);
      },
      modal: {
        ondismiss: () => {
          this.placingOrder = false;
          this.error = 'Payment cancelled. Your order has been saved and can be completed later.';
        }
      }
    };

    const razorpay = new Razorpay(options);
    razorpay.on('payment.failed', (response: any) => {
      this.placingOrder = false;
      this.error = `Payment failed: ${response.error.description || 'Please try again.'}`;
    });
    razorpay.open();
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
          this.orderSuccess = true;
          this.orderNumber = `HC${orderId.toString().padStart(6, '0')}`;
          this.placingOrder = false;
          this.cartService.clearLocalCart();
        } else {
          this.error = result.Messages?.join(', ') || 'Payment verification failed. Please contact support.';
          this.placingOrder = false;
        }
      },
      error: (err) => {
        this.error = 'Payment verification failed. Please contact support with your order ID.';
        this.placingOrder = false;
        console.error(err);
      }
    });
  }

  continueShopping(): void {
    this.router.navigate(['/shop']);
  }
}
