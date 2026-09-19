import { Component, OnInit } from '@angular/core';
import { AdminService } from '../services/admin.service';
import { AuthService } from '../services/auth.service';
import {
  AdminRole,
  AdminResult,
  AdminUserFormRequest,
  AdminUserList
} from '../models/admin.model';

@Component({
  selector: 'app-admin-users',
  templateUrl: './admin-users.component.html',
  styleUrls: ['./admin-users.component.scss'],
  standalone: false
})
export class AdminUsersComponent implements OnInit {
  users: AdminUserList[] = [];
  roles: AdminRole[] = [];
  isLoading = true;
  isLoadingDetail = false;

  // Add/Edit modal
  showFormModal = false;
  editingUserId: number | null = null;
  isSaving = false;
  formMessage = '';
  formError = false;
  confirmPassword = '';
  form: AdminUserFormRequest = this.emptyForm();

  // Status filter
  statusFilter: 'all' | 'active' | 'inactive' = 'active';

  // Pagination
  pageSize = 10;
  currentPage = 1;
  pageSizeOptions = [5, 10, 25, 50];

  constructor(
    private adminService: AdminService,
    private authService: AuthService
  ) {}

  ngOnInit(): void {
    this.loadUsers();
    this.loadRoles();
  }

  loadUsers(): void {
    this.isLoading = true;
    this.adminService.getAdminUsers().subscribe({
      next: (data) => {
        this.users = data;
        this.isLoading = false;
        this.currentPage = 1;
      },
      error: () => this.isLoading = false
    });
  }

  loadRoles(): void {
    this.adminService.getAdminRoles().subscribe({
      next: (data) => this.roles = data,
      error: () => this.roles = []
    });
  }

  emptyForm(): AdminUserFormRequest {
    return {
      loginId: '',
      password: '',
      firstName: '',
      middleName: '',
      lastName: '',
      emailId: '',
      mobileNumber: '',
      isActive: true,
      mustChangePassword: false,
      roleIds: []
    };
  }

  openAddModal(): void {
    this.editingUserId = null;
    this.form = this.emptyForm();
    this.confirmPassword = '';
    this.formMessage = '';
    this.formError = false;
    this.showFormModal = true;
  }

  openEditModal(userId: number): void {
    this.isLoadingDetail = true;
    this.formMessage = '';
    this.formError = false;
    this.adminService.getAdminUser(userId).subscribe({
      next: (user) => {
        this.editingUserId = user.userId;
        this.form = {
          loginId: user.loginId,
          password: '',
          firstName: user.firstName,
          middleName: user.middleName || '',
          lastName: user.lastName || '',
          emailId: user.emailId || '',
          mobileNumber: user.mobileNumber || '',
          isActive: user.isActive,
          mustChangePassword: user.mustChangePassword,
          roleIds: user.roles.map(r => r.roleId)
        };
        this.confirmPassword = '';
        this.isLoadingDetail = false;
        this.showFormModal = true;
      },
      error: () => {
        this.isLoadingDetail = false;
        this.formMessage = 'Failed to load user details.';
        this.formError = true;
        this.showFormModal = true;
      }
    });
  }

  closeFormModal(): void {
    this.showFormModal = false;
    this.editingUserId = null;
    this.formMessage = '';
    this.formError = false;
  }
  toggleRole(roleId: number, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    if (checked) {
      if (!this.form.roleIds.includes(roleId)) {
        this.form.roleIds.push(roleId);
      }
    } else {
      this.form.roleIds = this.form.roleIds.filter(r => r !== roleId);
    }
  }

  isRoleSelected(roleId: number): boolean {
    return this.form.roleIds.includes(roleId);
  }

  validateForm(): string | null {
    const loginId = this.form.loginId.trim();
    if (!loginId) return 'Login ID is required.';
    if (loginId.length > 20) return 'Login ID cannot exceed 20 characters.';
    if (!this.form.firstName.trim()) return 'First name is required.';
    if (this.form.mobileNumber && !/^\d{0,10}$/.test(this.form.mobileNumber.trim())) {
      return 'Mobile number can only contain up to 10 digits.';
    }
    if (this.form.roleIds.length === 0) return 'Select at least one role.';
    if (!this.editingUserId && (!this.form.password || this.form.password.length < 6)) {
      return 'Password is required and must be at least 6 characters.';
    }
    if (this.editingUserId && this.form.password && this.form.password.length < 6) {
      return 'New password must be at least 6 characters.';
    }
    if (this.form.password !== this.confirmPassword) {
      return 'Password and confirm password do not match.';
    }
    return null;
  }

  saveUser(): void {
    const error = this.validateForm();
    if (error) {
      this.formMessage = error;
      this.formError = true;
      return;
    }

    const actor = this.authService.getUser();
    const actorUserId = actor?.userId ?? 0;
    const request: AdminUserFormRequest = {
      ...this.form,
      loginId: this.form.loginId.trim(),
      firstName: this.form.firstName.trim(),
      middleName: this.form.middleName?.trim() || undefined,
      lastName: this.form.lastName?.trim() || undefined,
      emailId: this.form.emailId?.trim() || undefined,
      mobileNumber: this.form.mobileNumber?.trim() || undefined,
      password: this.form.password || undefined
    };

    this.isSaving = true;
    this.formMessage = '';
    this.formError = false;

    if (this.editingUserId) {
      this.adminService.updateAdminUser(this.editingUserId, request, actorUserId).subscribe({
        next: (result) => this.handleSaveResult(result),
        error: () => this.handleSaveError()
      });
    } else {
      this.adminService.createAdminUser(request, actorUserId).subscribe({
        next: (result) => this.handleSaveResult(result),
        error: () => this.handleSaveError()
      });
    }
  }

  handleSaveResult(result: AdminResult): void {
    this.isSaving = false;
    if (result.result === 1) {
      this.formMessage = result.messages[0];
      this.formError = false;
      this.loadUsers();
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

  get isEditMode(): boolean {
    return this.editingUserId !== null;
  }

  // Status filtering
  setStatusFilter(filter: 'all' | 'active' | 'inactive'): void {
    this.statusFilter = filter;
    this.currentPage = 1;
  }

  get filteredUsers(): AdminUserList[] {
    if (this.statusFilter === 'active') {
      return this.users.filter(u => u.isActive);
    }
    if (this.statusFilter === 'inactive') {
      return this.users.filter(u => !u.isActive);
    }
    return this.users;
  }

  get activeCount(): number {
    return this.users.filter(u => u.isActive).length;
  }

  get inactiveCount(): number {
    return this.users.filter(u => !u.isActive).length;
  }

  // Pagination helpers
  get paginatedUsers(): AdminUserList[] {
    const start = (this.currentPage - 1) * this.pageSize;
    return this.filteredUsers.slice(start, start + this.pageSize);
  }

  get totalPages(): number {
    return Math.ceil(this.filteredUsers.length / this.pageSize);
  }

  get startRecord(): number {
    return this.filteredUsers.length === 0 ? 0 : (this.currentPage - 1) * this.pageSize + 1;
  }

  get endRecord(): number {
    return Math.min(this.currentPage * this.pageSize, this.filteredUsers.length);
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
