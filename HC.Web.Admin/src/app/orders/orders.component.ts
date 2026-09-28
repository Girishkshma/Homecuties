import { Component, OnInit } from '@angular/core';
import { AdminService } from '../services/admin.service';
import { AdminOrder, AdminOrderStatusOption } from '../models/admin.model';

@Component({
  selector: 'app-orders',
  templateUrl: './orders.component.html',
  styleUrls: ['./orders.component.scss'],
  standalone: false
})
export class OrdersComponent implements OnInit {
  orders: AdminOrder[] = [];
  filteredOrders: AdminOrder[] = [];
  statuses: AdminOrderStatusOption[] = [];
  isLoading = true;

  // Filters
  searchTerm = '';
  statusFilter = 0;

  // Pagination
  pageSize = 10;
  currentPage = 1;
  pageSizeOptions = [5, 10, 25, 50];

  constructor(private adminService: AdminService) {}

  ngOnInit(): void {
    this.loadOrders();
    this.loadStatuses();
  }

  loadOrders(): void {
    this.isLoading = true;
    this.adminService.getOrders().subscribe({
      next: (data) => {
        this.orders = data;
        this.applyFilter();
        this.isLoading = false;
      },
      error: () => this.isLoading = false
    });
  }

  loadStatuses(): void {
    this.adminService.getOrderStatuses().subscribe({
      next: (data) => (this.statuses = data),
      error: () => (this.statuses = [])
    });
  }

  applyFilter(): void {
    const term = this.searchTerm.trim().toLowerCase();

    this.filteredOrders = this.orders.filter(o => {
      const matchesStatus = !this.statusFilter || o.statusId === this.statusFilter;
      const matchesTerm =
        !term ||
        (o.orderNumber || '').toLowerCase().includes(term) ||
        (o.customerName || '').toLowerCase().includes(term) ||
        (o.status || '').toLowerCase().includes(term);

      return matchesStatus && matchesTerm;
    });

    this.currentPage = 1;
  }

  search(): void {
    this.applyFilter();
  }

  clearSearch(): void {
    this.searchTerm = '';
    this.statusFilter = 0;
    this.applyFilter();
  }

  /**
   * Colour of the status chip. The ids are Orders.OrderStatusID (1 Pending, 2 Confirmed,
   * 3 Shipped, 4 Delivered, 5 Cancelled).
   */
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

  // Pagination helpers
  get paginatedOrders(): AdminOrder[] {
    const start = (this.currentPage - 1) * this.pageSize;
    return this.filteredOrders.slice(start, start + this.pageSize);
  }

  get totalPages(): number {
    return Math.ceil(this.filteredOrders.length / this.pageSize);
  }

  get startRecord(): number {
    return this.filteredOrders.length === 0 ? 0 : (this.currentPage - 1) * this.pageSize + 1;
  }

  get endRecord(): number {
    return Math.min(this.currentPage * this.pageSize, this.filteredOrders.length);
  }

  getPageNumbers(): (number | string)[] {
    const pages: (number | string)[] = [];
    const total = this.totalPages;
    if (total <= 7) {
      for (let i = 1; i <= total; i++) pages.push(i);
    } else {
      pages.push(1);
      if (this.currentPage > 3) pages.push('...');
      const start = Math.max(2, this.currentPage - 1);
      const end = Math.min(total - 1, this.currentPage + 1);
      for (let i = start; i <= end; i++) pages.push(i);
      if (this.currentPage < total - 2) pages.push('...');
      pages.push(total);
    }
    return pages;
  }

  goToPage(page: number | string): void {
    if (typeof page === 'number' && page >= 1 && page <= this.totalPages) {
      this.currentPage = page;
    }
  }

  changePageSize(size: number): void {
    this.pageSize = size;
    this.currentPage = 1;
  }
}

