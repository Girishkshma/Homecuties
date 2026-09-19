import { Component, OnInit } from '@angular/core';
import { AdminService } from '../services/admin.service';
import { AdminCustomer, AdminCustomerDetail } from '../models/admin.model';

@Component({
  selector: 'app-customers',
  templateUrl: './customers.component.html',
  styleUrls: ['./customers.component.scss'],
  standalone: false
})
export class CustomersComponent implements OnInit {
  customers: AdminCustomer[] = [];
  selectedCustomer: AdminCustomerDetail | null = null;
  isLoading = true;
  isLoadingDetail = false;
  searchTerm = '';
  showDetailModal = false;
  showStatusModal = false;
  statusUpdateMessage = '';
  statusUpdateError = false;

  // Status management
  selectedStatusId = 0;
  isUpdatingStatus = false;

  // Pagination
  pageSize = 10;
  currentPage = 1;
  pageSizeOptions = [5, 10, 25, 50];

  constructor(private adminService: AdminService) {}

  ngOnInit(): void {
    this.loadCustomers();
  }

  loadCustomers(): void {
    this.isLoading = true;
    this.adminService.getCustomers(this.searchTerm || undefined).subscribe({
      next: (data) => {
        this.customers = data;
        this.isLoading = false;
        this.currentPage = 1;
      },
      error: () => this.isLoading = false
    });
  }

  search(): void {
    this.loadCustomers();
  }

  clearSearch(): void {
    this.searchTerm = '';
    this.loadCustomers();
  }

  viewDetail(customerId: number): void {
    this.isLoadingDetail = true;
    this.showDetailModal = true;
    this.adminService.getCustomerDetail(customerId).subscribe({
      next: (data) => {
        this.selectedCustomer = data;
        this.isLoadingDetail = false;
      },
      error: () => {
        this.isLoadingDetail = false;
        this.showDetailModal = false;
      }
    });
  }

  closeDetail(): void {
    this.showDetailModal = false;
    this.selectedCustomer = null;
  }

  openStatusModal(customer: AdminCustomer): void {
    this.selectedStatusId = customer.customerStatusId;
    this.statusUpdateMessage = '';
    this.statusUpdateError = false;
    this.showStatusModal = true;
    // Store the selected customer for the status update
    this.selectedCustomer = {
      customerId: customer.customerId,
      firstName: customer.firstName,
      middleName: customer.middleName,
      lastName: customer.lastName,
      emailId: customer.emailId,
      mobileNumber: customer.mobileNumber,
      mobileVerified: customer.mobileVerified,
      emailVerified: customer.emailVerified,
      createdOn: customer.createdOn,
      modifiedOn: customer.modifiedOn,
      customerStatusId: customer.customerStatusId,
      status: customer.status,
      addresses: [],
      orderCount: 0,
      totalSpent: 0
    };
  }

  closeStatusModal(): void {
    this.showStatusModal = false;
    this.selectedStatusId = 0;
    this.statusUpdateMessage = '';
  }

  updateStatus(): void {
    if (!this.selectedCustomer || !this.selectedStatusId) return;

    this.isUpdatingStatus = true;
    this.statusUpdateMessage = '';

    this.adminService.updateCustomerStatus(this.selectedCustomer.customerId, this.selectedStatusId).subscribe({
      next: (result) => {
        this.isUpdatingStatus = false;
        if (result.result === 1) {
          this.statusUpdateMessage = result.messages[0];
          this.statusUpdateError = false;
          // Refresh the list
          this.loadCustomers();
          setTimeout(() => this.closeStatusModal(), 1500);
        } else {
          this.statusUpdateMessage = result.messages[0];
          this.statusUpdateError = true;
        }
      },
      error: () => {
        this.isUpdatingStatus = false;
        this.statusUpdateMessage = 'Failed to update customer status.';
        this.statusUpdateError = true;
      }
    });
  }

  getStatusBadgeClass(status: string): string {
    switch (status?.toLowerCase()) {
      case 'active': return 'badge badge-active';
      case 'inactive': return 'badge badge-inactive';
      case 'blocked': return 'badge badge-blocked';
      case 'pending': return 'badge badge-pending';
      default: return 'badge';
    }
  }

  // Pagination helpers
  get paginatedCustomers(): AdminCustomer[] {
    const start = (this.currentPage - 1) * this.pageSize;
    return this.customers.slice(start, start + this.pageSize);
  }

  get totalPages(): number {
    return Math.ceil(this.customers.length / this.pageSize);
  }

  get startRecord(): number {
    return this.customers.length === 0 ? 0 : (this.currentPage - 1) * this.pageSize + 1;
  }

  get endRecord(): number {
    return Math.min(this.currentPage * this.pageSize, this.customers.length);
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
