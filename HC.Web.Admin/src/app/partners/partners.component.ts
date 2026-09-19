import { Component, OnInit } from '@angular/core';
import { AdminService } from '../services/admin.service';
import { AuthService } from '../services/auth.service';
import {
  AdminPartner,
  AdminPartnerDetail,
  PartnerFormRequest,
  PartnerStatusOption
} from '../models/admin.model';

@Component({
  selector: 'app-partners',
  templateUrl: './partners.component.html',
  styleUrls: ['./partners.component.scss'],
  standalone: false
})
export class PartnersComponent implements OnInit {
  partners: AdminPartner[] = [];
  partnerStatuses: PartnerStatusOption[] = [];
  selectedPartner: AdminPartnerDetail | null = null;
  isLoading = true;
  isLoadingDetail = false;
  showDetail = false;

  // Add/Edit modal
  showFormModal = false;
  editingPartnerId: number | null = null;
  isSaving = false;
  formMessage = '';
  formError = false;
  partnerForm: PartnerFormRequest = this.emptyForm();

  // Pagination
  pageSize = 10;
  currentPage = 1;
  pageSizeOptions = [5, 10, 25, 50];

  constructor(
    private adminService: AdminService,
    private authService: AuthService
  ) {}

  ngOnInit(): void {
    this.loadPartners();
    this.loadStatuses();
  }

  loadPartners(): void {
    this.isLoading = true;
    this.adminService.getPartners().subscribe({
      next: (data) => {
        this.partners = data;
        this.isLoading = false;
        this.currentPage = 1;
      },
      error: () => this.isLoading = false
    });
  }

  loadStatuses(): void {
    this.adminService.getPartnerStatuses().subscribe({
      next: (data) => this.partnerStatuses = data,
      error: () => this.partnerStatuses = []
    });
  }

  emptyForm(): PartnerFormRequest {
    return { partnerName: '', partnerStatusId: 0 };
  }

  viewDetail(partnerId: number): void {
    this.isLoadingDetail = true;
    this.showDetail = true;
    this.adminService.getPartnerDetail(partnerId).subscribe({
      next: (data) => { this.selectedPartner = data; this.isLoadingDetail = false; },
      error: () => { this.isLoadingDetail = false; this.showDetail = false; }
    });
  }

  closeDetail(): void {
    this.showDetail = false;
    this.selectedPartner = null;
  }

  openAddModal(): void {
    this.editingPartnerId = null;
    this.partnerForm = this.emptyForm();
    this.formMessage = '';
    this.formError = false;
    this.showFormModal = true;
  }

  openEditModal(partnerId: number): void {
    this.isLoadingDetail = true;
    this.formMessage = '';
    this.formError = false;
    this.adminService.getPartnerDetail(partnerId).subscribe({
      next: (partner) => {
        this.editingPartnerId = partner.partnerId;
        this.partnerForm = {
          partnerName: partner.partnerName,
          partnerStatusId: partner.partnerStatusId
        };
        this.isLoadingDetail = false;
        this.formMessage = '';
        this.formError = false;
        this.showFormModal = true;
      },
      error: () => {
        this.isLoadingDetail = false;
        this.formMessage = 'Failed to load partner details.';
        this.formError = true;
        this.showFormModal = true;
      }
    });
  }

  closeFormModal(): void {
    this.showFormModal = false;
    this.editingPartnerId = null;
    this.formMessage = '';
    this.formError = false;
  }

  get isEditMode(): boolean {
    return this.editingPartnerId !== null;
  }

  validateForm(): string | null {
    if (!this.partnerForm.partnerName.trim()) return 'Partner name is required.';
    if (this.partnerForm.partnerName.trim().length > 100) {
      return 'Partner name cannot exceed 100 characters.';
    }
    if (!this.partnerForm.partnerStatusId) return 'Select a partner status.';
    return null;
  }
  savePartner(): void {
    const error = this.validateForm();
    if (error) {
      this.formMessage = error;
      this.formError = true;
      return;
    }

    const actor = this.authService.getUser();
    const actorUserId = actor?.userId ?? 0;
    const request: PartnerFormRequest = {
      partnerName: this.partnerForm.partnerName.trim(),
      partnerStatusId: this.partnerForm.partnerStatusId
    };

    this.isSaving = true;
    this.formMessage = '';
    this.formError = false;

    if (this.editingPartnerId) {
      this.adminService.updatePartner(this.editingPartnerId, request, actorUserId).subscribe({
        next: (result) => this.handleSaveResult(result),
        error: () => this.handleSaveError()
      });
    } else {
      this.adminService.createPartner(request, actorUserId).subscribe({
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
      this.loadPartners();
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
  get paginatedPartners(): AdminPartner[] {
    const start = (this.currentPage - 1) * this.pageSize;
    return this.partners.slice(start, start + this.pageSize);
  }

  get totalPages(): number {
    return Math.ceil(this.partners.length / this.pageSize);
  }

  get startRecord(): number {
    return this.partners.length === 0 ? 0 : (this.currentPage - 1) * this.pageSize + 1;
  }

  get endRecord(): number {
    return Math.min(this.currentPage * this.pageSize, this.partners.length);
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
