import { Component, OnInit } from '@angular/core';
import { AdminService } from '../services/admin.service';
import { AuthService } from '../services/auth.service';
import { AdminVendor, AdminVendorDetail, VendorFormRequest } from '../models/admin.model';

@Component({
  selector: 'app-vendors',
  templateUrl: './vendors.component.html',
  styleUrls: ['./vendors.component.scss'],
  standalone: false
})
export class VendorsComponent implements OnInit {
  vendors: AdminVendor[] = [];
  selectedVendor: AdminVendorDetail | null = null;
  isLoading = true;
  isLoadingDetail = false;
  showDetail = false;

  // Add/Edit modal
  showFormModal = false;
  editingVendorId: number | null = null;
  isSaving = false;
  formMessage = '';
  formError = false;
  vendorForm: VendorFormRequest = this.emptyForm();

  // Pagination
  pageSize = 10;
  currentPage = 1;
  pageSizeOptions = [5, 10, 25, 50];

  constructor(
    private adminService: AdminService,
    private authService: AuthService
  ) {}

  ngOnInit(): void {
    this.loadVendors();
  }

  loadVendors(): void {
    this.isLoading = true;
    this.adminService.getVendors().subscribe({
      next: (data) => {
        this.vendors = data;
        this.isLoading = false;
        this.currentPage = 1;
      },
      error: () => this.isLoading = false
    });
  }

  emptyForm(): VendorFormRequest {
    return {
      vendorName: '',
      mobile: '',
      vendorAddress: '',
      remarks: '',
      isActive: true
    };
  }

  viewDetail(vendorId: number): void {
    this.isLoadingDetail = true;
    this.showDetail = true;
    this.adminService.getVendorDetail(vendorId).subscribe({
      next: (data) => { this.selectedVendor = data; this.isLoadingDetail = false; },
      error: () => { this.isLoadingDetail = false; this.showDetail = false; }
    });
  }

  closeDetail(): void {
    this.showDetail = false;
    this.selectedVendor = null;
  }

  openAddModal(): void {
    this.editingVendorId = null;
    this.vendorForm = this.emptyForm();
    this.formMessage = '';
    this.formError = false;
    this.showFormModal = true;
  }

  openEditModal(vendorId: number): void {
    this.isLoadingDetail = true;
    this.formMessage = '';
    this.formError = false;
    this.adminService.getVendorDetail(vendorId).subscribe({
      next: (vendor) => {
        this.editingVendorId = vendor.vendorId;
        this.vendorForm = {
          vendorName: vendor.vendorName,
          mobile: vendor.mobile,
          vendorAddress: vendor.vendorAddress || '',
          remarks: vendor.remarks || '',
          isActive: vendor.isActive
        };
        this.isLoadingDetail = false;
        this.formMessage = '';
        this.formError = false;
        this.showFormModal = true;
      },
      error: () => {
        this.isLoadingDetail = false;
        this.formMessage = 'Failed to load vendor details.';
        this.formError = true;
        this.showFormModal = true;
      }
    });
  }

  closeFormModal(): void {
    this.showFormModal = false;
    this.editingVendorId = null;
    this.formMessage = '';
    this.formError = false;
  }

  get isEditMode(): boolean {
    return this.editingVendorId !== null;
  }

  validateForm(): string | null {
    if (!this.vendorForm.vendorName.trim()) return 'Vendor name is required.';
    if (this.vendorForm.vendorName.trim().length > 50) {
      return 'Vendor name cannot exceed 50 characters.';
    }
    if (!this.vendorForm.mobile.trim()) return 'Mobile number is required.';
    if (!/^\d{1,10}$/.test(this.vendorForm.mobile.trim())) {
      return 'Mobile number must be up to 10 digits.';
    }
    return null;
  }
  saveVendor(): void {
    const error = this.validateForm();
    if (error) {
      this.formMessage = error;
      this.formError = true;
      return;
    }

    const actor = this.authService.getUser();
    const actorUserId = actor?.userId ?? 0;
    const request: VendorFormRequest = {
      vendorName: this.vendorForm.vendorName.trim(),
      mobile: this.vendorForm.mobile.trim(),
      vendorAddress: this.vendorForm.vendorAddress?.trim() || undefined,
      remarks: this.vendorForm.remarks?.trim() || undefined,
      isActive: this.vendorForm.isActive
    };

    this.isSaving = true;
    this.formMessage = '';
    this.formError = false;

    if (this.editingVendorId) {
      this.adminService.updateVendor(this.editingVendorId, request, actorUserId).subscribe({
        next: (result) => this.handleSaveResult(result),
        error: () => this.handleSaveError()
      });
    } else {
      this.adminService.createVendor(request, actorUserId).subscribe({
        next: (result) => this.handleSaveResult(result),
        error: () => this.handleSaveError()
      });
    }
  }

  handleSaveResult(result: { result: number; messages: string[] }): void {
    this.isSaving = false;
    if (result.result === 1) {
      this.formMessage = result.messages[0];
      this.formError = false;
      this.loadVendors();
      setTimeout(() => this.closeFormModal(), 1200);
    } else {
      this.formMessage = result.messages[0];
      this.formError = true;
    }
  }

  handleSaveError(): void {
    this.isSaving = false;
    this.formMessage = 'Unable to save. Please check your connection and try again.';
    this.formError = true;
  }

  // Pagination helpers
  get paginatedVendors(): AdminVendor[] {
    const start = (this.currentPage - 1) * this.pageSize;
    return this.vendors.slice(start, start + this.pageSize);
  }

  get totalPages(): number {
    return Math.ceil(this.vendors.length / this.pageSize);
  }

  get startRecord(): number {
    return this.vendors.length === 0 ? 0 : (this.currentPage - 1) * this.pageSize + 1;
  }

  get endRecord(): number {
    return Math.min(this.currentPage * this.pageSize, this.vendors.length);
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
