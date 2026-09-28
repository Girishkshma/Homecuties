import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { MyOrder, PaymentService } from '../services/payment.service';
import { AuthService } from '../services/auth.service';
import { UtilityService } from '../services/utility.service';

@Component({
  selector: 'app-my-orders',
  templateUrl: './my-orders.component.html',
  standalone: false,
  styleUrl: './my-orders.component.scss'
})
export class MyOrdersComponent implements OnInit {
  orders: MyOrder[] = [];
  loading = true;
  error = '';
  notice = '';

  /** Order a cancel / payment check is running for - keeps the buttons of that card disabled. */
  busyOrderId = 0;

  /** Order the customer asked to cancel, waiting for the inline confirmation. */
  confirmingOrderId = 0;

  /** Order whose items / address / timeline are expanded. */
  expandedOrderId = 0;

  UtilityService = UtilityService;

  constructor(
    private paymentService: PaymentService,
    private authService: AuthService,
    private router: Router
  ) {}

  ngOnInit(): void {
    const customer = this.authService.getCurrentCustomer();
    if (!customer || !customer.CustomerID) {
      this.router.navigate(['/login'], { queryParams: { returnUrl: '/my-orders' } });
      return;
    }

    this.loadOrders();
  }

  loadOrders(): void {
    this.loading = true;
    this.error = '';

    this.paymentService.getMyOrders().subscribe({
      next: (data) => {
        this.orders = data || [];
        this.loading = false;
      },
      error: (err) => {
        this.error = 'We could not load your orders. Please try again in a moment.';
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
   * Cancels an unpaid order: the backend returns the reserved units to the available pool so they
   * can be sold again. A paid order cannot be cancelled here and the customer is told to contact
   * support - refunds are handled by the shop team.
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

    this.paymentService.syncPayment(order.OrderId).subscribe({
      next: (result) => {
        this.busyOrderId = 0;

        if (result.Result === 1) {
          this.notice = result.Messages?.join(' ') || 'Your payment has been received.';
          this.refreshOrders();
          return;
        }

        this.error = result.Messages?.join(' ') || 'We could not find a payment for this order.';
      },
      error: (err) => {
        this.busyOrderId = 0;
        this.error = 'We could not check the payment right now. Please try again in a moment.';
        console.error(err);
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
}
