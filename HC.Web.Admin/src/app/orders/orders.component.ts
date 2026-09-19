import { Component, OnInit } from '@angular/core';
import { AdminService } from '../services/admin.service';
import { AdminOrder } from '../models/admin.model';

@Component({
  selector: 'app-orders',
  templateUrl: './orders.component.html',
  styleUrls: ['./orders.component.scss'],
  standalone: false
})
export class OrdersComponent implements OnInit {
  orders: AdminOrder[] = [];
  isLoading = true;

  // Pagination
  pageSize = 10;
  currentPage = 1;
  pageSizeOptions = [5, 10, 25, 50];

  constructor(private adminService: AdminService) {}

  ngOnInit(): void {
    this.loadOrders();
  }

  loadOrders(): void {
    this.isLoading = true;
    this.adminService.getOrders().subscribe({
      next: (data) => {
        this.orders = data;
        this.isLoading = false;
        this.currentPage = 1;
      },
      error: () => this.isLoading = false
    });
  }

  // Pagination helpers
  get paginatedOrders(): AdminOrder[] {
    const start = (this.currentPage - 1) * this.pageSize;
    return this.orders.slice(start, start + this.pageSize);
  }

  get totalPages(): number {
    return Math.ceil(this.orders.length / this.pageSize);
  }

  get startRecord(): number {
    return this.orders.length === 0 ? 0 : (this.currentPage - 1) * this.pageSize + 1;
  }

  get endRecord(): number {
    return Math.min(this.currentPage * this.pageSize, this.orders.length);
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
