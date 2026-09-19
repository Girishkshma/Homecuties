import { Component, OnInit } from '@angular/core';
import { AdminService } from '../services/admin.service';
import { AuthService } from '../services/auth.service';
import {
  AdminProduct,
  AdminPurchase,
  AdminPurchaseComment,
  AdminPurchaseDetail,
  AdminPurchaseFormItem,
  AdminPurchaseFormRequest,
  AdminPurchaseItem,
  AdminPurchaseItemSave,
  AdminPurchaseStatus,
  AdminPurchaseUpdateRequest,
  AdminPurchaser,
  AdminVendor
} from '../models/admin.model';

@Component({
  selector: 'app-purchases',
  templateUrl: './purchases.component.html',
  styleUrls: ['./purchases.component.scss'],
  standalone: false
})
export class PurchasesComponent implements OnInit {
  purchases: AdminPurchase[] = [];
  filteredPurchases: AdminPurchase[] = [];
  selectedPurchase: AdminPurchaseDetail | null = null;
  isLoading = true;
  isLoadingDetail = false;
  showDetailModal = false;
  searchTerm = '';

  // Add Purchase modal
  showFormModal = false;
  isSaving = false;
  formMessage = '';
  formError = false;
  isLoadingOptions = false;
  vendors: AdminVendor[] = [];
  purchasers: AdminPurchaser[] = [];
  products: AdminProduct[] = [];
  purchaseForm: AdminPurchaseFormRequest = this.emptyForm();

  // Edit / status workflow modal
  showEditModal = false;
  editingPurchaseId: number | null = null;
  editPurchaseNumber = '';
  isLoadingEdit = false;
  isSavingEdit = false;
  editMessage = '';
  editError = false;
  editForm: AdminPurchaseUpdateRequest = this.emptyEditForm();
  editItems: AdminPurchaseItemSave[] = [];
  editComments: AdminPurchaseComment[] = [];
  purchaseStatuses: AdminPurchaseStatus[] = [];
  currentEditStatusId = 0;
  currentEditStatusName = '';
  editStatusId = 0;
  editStatusComment = '';
  newComment = '';

  // Pagination
  pageSize = 10;
  currentPage = 1;
  pageSizeOptions = [5, 10, 25, 50];

  constructor(
    private adminService: AdminService,
    private authService: AuthService
  ) {}

  ngOnInit(): void {
    this.loadPurchases();
  }

  loadPurchases(): void {
    this.isLoading = true;
    this.adminService.getPurchases().subscribe({
      next: (data) => {
        this.purchases = data;
        this.applyFilter();
        this.isLoading = false;
      },
      error: () => this.isLoading = false
    });
  }

  applyFilter(): void {
    const term = this.searchTerm.trim().toLowerCase();
    if (!term) {
      this.filteredPurchases = [...this.purchases];
    } else {
      this.filteredPurchases = this.purchases.filter(p =>
        (p.purchaseNumber || '').toLowerCase().includes(term) ||
        p.vendorName.toLowerCase().includes(term) ||
        p.purchaserName.toLowerCase().includes(term) ||
        p.status.toLowerCase().includes(term)
      );
    }
    this.currentPage = 1;
  }

  search(): void {
    this.applyFilter();
  }

  clearSearch(): void {
    this.searchTerm = '';
    this.applyFilter();
  }

  viewDetail(purchaseId: number): void {
    this.isLoadingDetail = true;
    this.showDetailModal = true;
    this.selectedPurchase = null;
    this.adminService.getPurchaseDetail(purchaseId).subscribe({
      next: (data) => {
        this.selectedPurchase = data;
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
    this.selectedPurchase = null;
  }

  getStatusBadgeClass(status: string): string {
    switch (status?.toLowerCase()) {
      case 'approved': return 'badge badge-active';
      case 'submitted': return 'badge badge-pending';
      case 'rejected': return 'badge badge-blocked';
      case 'cancelled': return 'badge badge-inactive';
      default: return 'badge';
    }
  }

  itemSubtotal(item: AdminPurchaseItem): number {
    return item.quantity * item.unitPrice;
  }

  get itemTotalAmount(): number {
    if (!this.selectedPurchase) return 0;
    return this.selectedPurchase.items.reduce((sum, it) => sum + this.itemSubtotal(it), 0);
  }

  get itemTotalGst(): number {
    if (!this.selectedPurchase) return 0;
    return this.selectedPurchase.items.reduce((sum, it) => sum + (this.itemSubtotal(it) * it.gst / 100), 0);
  }

  get itemGrandTotal(): number {
    return this.itemTotalAmount + this.itemTotalGst;
  }

  // ------------------------------------------------------------
  // Add Purchase
  // ------------------------------------------------------------
  emptyForm(): AdminPurchaseFormRequest {
    return {
      vendorId: 0,
      purchaserId: 0,
      purchaseDate: '',
      invoicePath: '',
      purchaseStatusId: 1, // 1 = ADDED
      items: []
    };
  }

  openAddModal(): void {
    this.purchaseForm = this.emptyForm();
    this.purchaseForm.purchaseDate = this.toDateInputValue(new Date());
    this.addItem();
    this.formMessage = '';
    this.formError = false;
    this.showFormModal = true;

    if (this.vendors.length === 0 || this.purchasers.length === 0 || this.products.length === 0) {
      this.loadFormOptions();
    }
  }

  closeFormModal(): void {
    this.showFormModal = false;
    this.purchaseForm = this.emptyForm();
    this.formMessage = '';
    this.formError = false;
  }

  loadFormOptions(): void {
    this.isLoadingOptions = true;
    let pending = 3;
    const done = () => {
      if (--pending === 0) this.isLoadingOptions = false;
    };

    this.adminService.getVendors().subscribe({
      next: (data) => { this.vendors = data.filter(v => v.isActive); done(); },
      error: () => done()
    });

    this.adminService.getPurchasers().subscribe({
      next: (data) => { this.purchasers = data; done(); },
      error: () => done()
    });

    this.adminService.getProducts().subscribe({
      next: (data) => { this.products = data; done(); },
      error: () => done()
    });
  }

  addItem(): void {
    this.purchaseForm.items.push({ productId: 0, quantity: 1, unitPrice: 0, gst: 0 });
  }

  removeItem(index: number): void {
    this.purchaseForm.items.splice(index, 1);
    if (this.purchaseForm.items.length === 0) {
      this.addItem();
    }
  }

  onProductChange(index: number): void {
    const item = this.purchaseForm.items[index];
    if (!item || !item.productId) return;

    const product = this.products.find(p => p.productId === item.productId);
    if (product) {
      item.unitPrice = product.unitPrice;
    }

    this.adminService.getProductDetail(item.productId).subscribe({
      next: (detail) => {
        item.unitPrice = detail.unitPrice;
        item.gst = (detail.cgstpercent || 0) + (detail.sgstpercent || 0);
      }
    });
  }

  formItemAmount(item: AdminPurchaseFormItem): number {
    return (item.quantity || 0) * (item.unitPrice || 0);
  }

  get formTotalAmount(): number {
    return this.purchaseForm.items.reduce((sum, it) => sum + this.formItemAmount(it), 0);
  }

  get formTotalGst(): number {
    return this.purchaseForm.items.reduce((sum, it) => sum + (this.formItemAmount(it) * (it.gst || 0) / 100), 0);
  }

  get formGrandTotal(): number {
    return this.formTotalAmount + this.formTotalGst;
  }

  validateForm(): string | null {
    if (!this.purchaseForm.vendorId) return 'Please select a vendor.';
    if (!this.purchaseForm.purchaserId) return 'Please select a purchaser.';
    if (!this.purchaseForm.purchaseDate) return 'Purchase date is required.';

    const items = this.purchaseForm.items.filter(i => i.productId > 0);
    if (items.length === 0) return 'Please add at least one item.';

    for (const item of items) {
      if (!item.quantity || item.quantity <= 0) return 'Each item must have a quantity greater than zero.';
      if (item.quantity > 32767) return 'Item quantity cannot exceed 32767.';
      if (item.unitPrice < 0) return 'Item unit price cannot be negative.';
      if (item.gst < 0) return 'Item GST cannot be negative.';
    }
    return null;
  }

  savePurchase(): void {
    const error = this.validateForm();
    if (error) {
      this.formMessage = error;
      this.formError = true;
      return;
    }

    const actorUserId = this.authService.getUser()?.userId ?? 0;
    if (!actorUserId) {
      this.formMessage = 'Your session has expired. Please sign in again.';
      this.formError = true;
      return;
    }

    const request: AdminPurchaseFormRequest = {
      vendorId: this.purchaseForm.vendorId,
      purchaserId: this.purchaseForm.purchaserId,
      purchaseDate: new Date(`${this.purchaseForm.purchaseDate}T00:00:00`).toISOString(),
      invoicePath: this.purchaseForm.invoicePath?.trim() || undefined,
      purchaseStatusId: this.purchaseForm.purchaseStatusId || 1,
      items: this.purchaseForm.items
        .filter(i => i.productId > 0)
        .map(i => ({
          productId: i.productId,
          quantity: i.quantity,
          unitPrice: i.unitPrice,
          gst: i.gst
        }))
    };

    this.isSaving = true;
    this.formMessage = '';
    this.formError = false;

    this.adminService.createPurchase(request, actorUserId).subscribe({
      next: (result) => {
        this.isSaving = false;
        this.formMessage = result.messages[0];
        this.formError = result.result !== 1;
        if (result.result === 1) {
          this.loadPurchases();
          setTimeout(() => this.closeFormModal(), 1200);
        }
      },
      error: () => {
        this.isSaving = false;
        this.formMessage = 'Unable to save. Please check your connection and try again.';
        this.formError = true;
      }
    });
  }

  private toDateInputValue(d: Date): string {
    const month = (d.getMonth() + 1).toString().padStart(2, '0');
    const day = d.getDate().toString().padStart(2, '0');
    return `${d.getFullYear()}-${month}-${day}`;
  }

  // ------------------------------------------------------------
  // Edit Purchase / status workflow
  // ------------------------------------------------------------
  emptyEditForm(): AdminPurchaseUpdateRequest {
    return { vendorId: 0, purchaserId: 0, purchaseDate: '', invoicePath: '' };
  }

  private actorUserId(): number {
    return this.authService.getUser()?.userId ?? 0;
  }

  openEditModal(purchaseId: number): void {
    this.editingPurchaseId = purchaseId;
    this.showEditModal = true;
    this.isLoadingEdit = true;
    this.isSavingEdit = false;
    this.editMessage = '';
    this.editError = false;
    this.editForm = this.emptyEditForm();
    this.editItems = [];
    this.editComments = [];
    this.purchaseStatuses = [];
    this.currentEditStatusId = 0;
    this.currentEditStatusName = '';
    this.editStatusId = 0;
    this.editStatusComment = '';
    this.newComment = '';

    if (this.vendors.length === 0 || this.purchasers.length === 0 || this.products.length === 0) {
      this.loadFormOptions();
    }

    this.adminService.getPurchaseDetail(purchaseId).subscribe({
      next: (data) => this.applyPurchaseToEditForm(data),
      error: () => {
        this.isLoadingEdit = false;
        this.editMessage = 'Failed to load purchase details.';
        this.editError = true;
      }
    });

    this.reloadStatuses(purchaseId);
  }

  private applyPurchaseToEditForm(data: AdminPurchaseDetail): void {
    this.editPurchaseNumber = data.purchaseNumber;
    this.currentEditStatusId = data.purchaseStatusId;
    this.currentEditStatusName = data.status;
    this.editForm = {
      vendorId: data.vendorId,
      purchaserId: data.purchaserId,
      purchaseDate: this.toDateInputValue(new Date(data.purchaseDate)),
      invoicePath: data.invoicePath || ''
    };
    this.editItems = data.items.map(i => ({
      purchaseDetailId: i.purchaseDetailId,
      productId: i.productId,
      quantity: i.quantity,
      unitPrice: i.unitPrice,
      gst: i.gst
    }));
    this.editComments = [...data.comments];
    this.isLoadingEdit = false;
  }

  private reloadEdit(): void {
    if (!this.editingPurchaseId) return;
    const id = this.editingPurchaseId;
    this.adminService.getPurchaseDetail(id).subscribe({
      next: (data) => this.applyPurchaseToEditForm(data)
    });
  }

  private reloadStatuses(purchaseId?: number): void {
    const id = purchaseId ?? this.editingPurchaseId;
    const userId = this.actorUserId();
    if (!id || !userId) {
      this.purchaseStatuses = [];
      return;
    }
    this.adminService.getPurchaseStatuses(id, userId).subscribe({
      next: (statuses) => { this.purchaseStatuses = statuses; },
      error: () => { this.purchaseStatuses = []; }
    });
  }

  closeEditModal(): void {
    this.showEditModal = false;
    this.editingPurchaseId = null;
    this.editMessage = '';
    this.editError = false;
  }

  // Header + items can only be changed while the purchase is ADDED (1) or RETURNED (5)
  get canEditPurchaseDetails(): boolean {
    return this.currentEditStatusId === 1 || this.currentEditStatusId === 5;
  }

  addEditItem(): void {
    this.editItems.push({ purchaseDetailId: 0, productId: 0, quantity: 1, unitPrice: 0, gst: 0 });
  }

  removeEditItem(index: number): void {
    this.editItems.splice(index, 1);
    if (this.editItems.length === 0) {
      this.addEditItem();
    }
  }

  onEditProductChange(index: number): void {
    const item = this.editItems[index];
    if (!item || !item.productId) return;

    const product = this.products.find(p => p.productId === item.productId);
    if (product) {
      item.unitPrice = product.unitPrice;
    }

    this.adminService.getProductDetail(item.productId).subscribe({
      next: (detail) => {
        item.unitPrice = detail.unitPrice;
        item.gst = (detail.cgstpercent || 0) + (detail.sgstpercent || 0);
      }
    });
  }

  editItemAmount(item: AdminPurchaseItemSave): number {
    return (item.quantity || 0) * (item.unitPrice || 0);
  }

  get editTotalAmount(): number {
    return this.editItems.reduce((sum, it) => sum + this.editItemAmount(it), 0);
  }

  get editTotalGst(): number {
    return this.editItems.reduce((sum, it) => sum + (this.editItemAmount(it) * (it.gst || 0) / 100), 0);
  }

  get editGrandTotal(): number {
    return this.editTotalAmount + this.editTotalGst;
  }

  saveEditDetails(): void {
    const id = this.editingPurchaseId;
    const userId = this.actorUserId();
    if (!id) return;

    if (!userId) {
      this.editMessage = 'Your session has expired. Please sign in again.';
      this.editError = true;
      return;
    }
    if (!this.editForm.vendorId) { this.editMessage = 'Please select a vendor.'; this.editError = true; return; }
    if (!this.editForm.purchaserId) { this.editMessage = 'Please select a purchaser.'; this.editError = true; return; }
    if (!this.editForm.purchaseDate) { this.editMessage = 'Purchase date is required.'; this.editError = true; return; }

    const items = this.editItems.filter(i => i.productId > 0);
    if (items.length === 0) { this.editMessage = 'Please add at least one item.'; this.editError = true; return; }

    for (const item of items) {
      if (!item.quantity || item.quantity <= 0) { this.editMessage = 'Each item must have a quantity greater than zero.'; this.editError = true; return; }
      if (item.quantity > 32767) { this.editMessage = 'Item quantity cannot exceed 32767.'; this.editError = true; return; }
      if (item.unitPrice < 0) { this.editMessage = 'Item unit price cannot be negative.'; this.editError = true; return; }
      if (item.gst < 0) { this.editMessage = 'Item GST cannot be negative.'; this.editError = true; return; }
    }

    this.isSavingEdit = true;
    this.editMessage = '';
    this.editError = false;

    this.adminService.updatePurchase(id, {
      vendorId: this.editForm.vendorId,
      purchaserId: this.editForm.purchaserId,
      purchaseDate: new Date(`${this.editForm.purchaseDate}T00:00:00`).toISOString(),
      invoicePath: this.editForm.invoicePath?.trim() || undefined
    }, userId).subscribe({
      next: (headerResult) => {
        if (headerResult.result !== 1) {
          this.isSavingEdit = false;
          this.editMessage = headerResult.messages[0];
          this.editError = true;
          return;
        }

        this.adminService.savePurchaseItems(id, items.map(i => ({
          purchaseDetailId: i.purchaseDetailId,
          productId: i.productId,
          quantity: i.quantity,
          unitPrice: i.unitPrice,
          gst: i.gst
        })), userId).subscribe({
          next: (itemsResult) => {
            this.isSavingEdit = false;
            this.editMessage = itemsResult.result === 1 ? 'Purchase details and items saved successfully.' : itemsResult.messages[0];
            this.editError = itemsResult.result !== 1;
            if (itemsResult.result === 1) {
              this.loadPurchases();
              this.reloadEdit();
            }
          },
          error: () => {
            this.isSavingEdit = false;
            this.editMessage = 'Unable to save purchase items. Please check your connection and try again.';
            this.editError = true;
          }
        });
      },
      error: () => {
        this.isSavingEdit = false;
        this.editMessage = 'Unable to save purchase details. Please check your connection and try again.';
        this.editError = true;
      }
    });
  }

  changePurchaseStatus(): void {
    const id = this.editingPurchaseId;
    const userId = this.actorUserId();
    if (!id) return;

    if (!userId) {
      this.editMessage = 'Your session has expired. Please sign in again.';
      this.editError = true;
      return;
    }
    if (!this.editStatusId) {
      this.editMessage = 'Please select a new status.';
      this.editError = true;
      return;
    }

    this.isSavingEdit = true;
    this.editMessage = '';
    this.editError = false;

    this.adminService.updatePurchaseStatus(id, {
      statusId: this.editStatusId,
      comments: this.editStatusComment?.trim() || undefined
    }, userId).subscribe({
      next: (result) => {
        this.isSavingEdit = false;
        this.editMessage = result.messages[0];
        this.editError = result.result !== 1;
        if (result.result === 1) {
          this.editStatusId = 0;
          this.editStatusComment = '';
          this.loadPurchases();
          this.reloadEdit();
          this.reloadStatuses();
        }
      },
      error: () => {
        this.isSavingEdit = false;
        this.editMessage = 'Unable to change the status. Please check your connection and try again.';
        this.editError = true;
      }
    });
  }

  addEditComment(): void {
    const id = this.editingPurchaseId;
    const userId = this.actorUserId();
    if (!id) return;

    if (!userId) {
      this.editMessage = 'Your session has expired. Please sign in again.';
      this.editError = true;
      return;
    }

    const comment = (this.newComment || '').trim();
    if (!comment) {
      this.editMessage = 'Comment is required.';
      this.editError = true;
      return;
    }

    this.isSavingEdit = true;
    this.editMessage = '';
    this.editError = false;

    this.adminService.addPurchaseComment(id, comment, userId).subscribe({
      next: (result) => {
        this.isSavingEdit = false;
        this.editMessage = result.messages[0];
        this.editError = result.result !== 1;
        if (result.result === 1) {
          this.newComment = '';
          this.reloadEdit();
        }
      },
      error: () => {
        this.isSavingEdit = false;
        this.editMessage = 'Unable to add the comment. Please check your connection and try again.';
        this.editError = true;
      }
    });
  }

  // Pagination helpers
  get paginatedPurchases(): AdminPurchase[] {
    const start = (this.currentPage - 1) * this.pageSize;
    return this.filteredPurchases.slice(start, start + this.pageSize);
  }

  get totalPages(): number {
    return Math.ceil(this.filteredPurchases.length / this.pageSize);
  }

  get startRecord(): number {
    return this.filteredPurchases.length === 0 ? 0 : (this.currentPage - 1) * this.pageSize + 1;
  }

  get endRecord(): number {
    return Math.min(this.currentPage * this.pageSize, this.filteredPurchases.length);
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
