import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { AdminService } from '../services/admin.service';
import { AuthService } from '../services/auth.service';
import { AdminOrderDetail } from '../models/admin.model';

/**
 * Everything the shop team needs for one order: what was bought (one row per physical unit, with its
 * SKU), where it goes, what the customer paid and the full status history - plus the status workflow,
 * which keeps the stock in step with the order (dispatch, deliver, cancel).
 */
@Component({
  selector: 'app-order-detail',
  templateUrl: './order-detail.component.html',
  styleUrls: ['./order-detail.component.scss'],
  standalone: false
})
export class OrderDetailComponent implements OnInit {
  order: AdminOrderDetail | null = null;
  isLoading = true;
  loadError = '';

  /** Orders.OrderStatusID = 5 (Cancelled) - a cancellation needs a reason for the customer. */
  readonly cancelledStatusId = 5;

  // Status workflow
  newStatusId = 0;
  statusComment = '';
  isSavingStatus = false;
  actionMessage = '';
  actionError = false;

  constructor(
    private adminService: AdminService,
    private authService: AuthService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    const orderId = Number(this.route.snapshot.paramMap.get('id'));
    if (!orderId) {
      this.router.navigate(['/orders']);
      return;
    }

    this.loadOrder(orderId);
  }

  loadOrder(orderId: number): void {
    this.isLoading = true;
    this.loadError = '';

    this.adminService.getOrderDetail(orderId).subscribe({
      next: (data) => {
        this.order = data;
        this.newStatusId = 0;
        this.statusComment = '';
        this.isLoading = false;
      },
      error: () => {
        this.loadError = 'The order could not be loaded. Please go back to the order list and try again.';
        this.isLoading = false;
      }
    });
  }

  reload(): void {
    if (this.order) {
      this.loadOrder(this.order.orderId);
    }
  }

  /** Colour of the status chip: 1 Pending, 2 Confirmed, 3 Shipped, 4 Delivered, 5 Cancelled. */
  getStatusBadgeClass(statusId: number): string {
    switch (statusId) {
      case 1: return 'badge badge-pending';
      case 2: return 'badge badge-confirmed';
      case 3: return 'badge badge-shipped';
      case 4: return 'badge badge-delivered';
      case 5: return 'badge badge-cancelled';
      default: return 'badge';
    }
  }

  getPaymentBadgeClass(isPaid: boolean): string {
    return isPaid ? 'badge badge-paid' : 'badge badge-unpaid';
  }

  /** What the checkout charged: OrderItems holds one row per unit, each carrying its unit price. */
  get itemsTotal(): number {
    return this.order ? this.order.items.reduce((sum, item) => sum + item.unitPrice, 0) : 0;
  }

  /** The delivery/packaging/storage charges recorded on the order lines. */
  get chargesTotal(): number {
    return this.order
      ? this.order.items.reduce((sum, item) => sum + item.deliveryCharge + item.packagingCharge + item.storageCharge, 0)
      : 0;
  }

  get selectedStatusName(): string {
    if (!this.order || !this.newStatusId) {
      return '';
    }

    const status = this.order.availableStatuses.find(s => s.statusId === Number(this.newStatusId));
    return status ? status.status : '';
  }

  /**
   * Moves the order to the selected step. The shop team's own rules live on the server (only the
   * steps of 'availableStatuses' are allowed and a cancellation needs a reason), so the message
   * coming back is shown as it is.
   */
  updateStatus(): void {
    if (!this.order) {
      return;
    }

    if (!this.newStatusId) {
      this.actionMessage = 'Select the status to move this order to.';
      this.actionError = true;
      return;
    }

    const userId = this.authService.getUser()?.userId ?? 0;
    if (!userId) {
      this.actionMessage = 'Your admin session has no user id - please sign in again.';
      this.actionError = true;
      return;
    }

    this.isSavingStatus = true;
    this.actionMessage = '';
    this.actionError = false;

    this.adminService.updateOrderStatus(
      this.order.orderId,
      { statusId: Number(this.newStatusId), comments: this.statusComment },
      userId
    ).subscribe({
      next: (result) => {
        this.isSavingStatus = false;
        this.actionError = result.result !== 1;
        this.actionMessage = (result.messages || []).join(' ');

        if (result.result === 1) {
          this.loadOrder(this.order!.orderId);
        }
      },
      error: () => {
        this.isSavingStatus = false;
        this.actionError = true;
        this.actionMessage = 'The order status could not be changed. Nothing was saved - please try again.';
      }
    });
  }
}
