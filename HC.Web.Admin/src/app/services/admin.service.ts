import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';
import {
  AdminLoginRequest,
  AdminLoginResponse,
  AdminUser,
  AdminMenu,
  AdminRole,
  DashboardStats,
  AdminProduct,
  AdminProductDetail,
  ProductFormOptions,
  ImageUploadResult,
  CreateProductRequest,
  AdminOrder,
  AdminOrderDetail,
  AdminOrderStatusOption,
  AdminOrderStatusUpdateRequest,
  AdminOrderRefundRequest,
  AdminOrderReturn,
  AdminOrderShipment,
  AdminShipmentProvider,
  AdminSaveOrderShipmentRequest,
  AdminCustomer,
  AdminCustomerDetail,
  AdminPartner,
  AdminPartnerDetail,
  PartnerStatusOption,
  PartnerFormRequest,
  AdminVendor,
  AdminVendorDetail,
  VendorFormRequest,
  AdminPurchase,
  AdminPurchaseDetail,
  AdminPurchaser,
  AdminPurchaseFormRequest,
  AdminPurchaseStatus,
  AdminPurchaseStatusUpdateRequest,
  AdminPurchaseUpdateRequest,
  AdminPurchaseItemSave,
  AdminPurchaseCommentRequest,
  AdminUserList,
  AdminUserDetail,
  AdminUserFormRequest,
  AdminCategory,
  AdminResult
} from '../models/admin.model';


@Injectable({
  providedIn: 'root'
})
export class AdminService {
  private apiUrl = environment.apiUrl;

  constructor(
    private http: HttpClient,
    private authService: AuthService
  ) { }

  /**
   * The signed JWT the API issued on login. Every admin request must carry it as
   * 'Authorization: Bearer <token>' - the adminTokenInterceptor adds it to each call, and the API
   * answers 401 when it is missing, forged or expired.
   *
   * The '?userId=' values the mutating calls still send are informational only: the API always
   * records the acting admin from the token, never from the query string.
   */
  private getAuthHeaders(): HttpHeaders {
    const token = this.authService.getToken();
    return new HttpHeaders({
      'Content-Type': 'application/json',
      ...(token ? { 'Authorization': `Bearer ${token}` } : {})
    });
  }

  // Auth
  login(request: AdminLoginRequest): Observable<AdminLoginResponse> {
    return this.http.post<AdminLoginResponse>(`${this.apiUrl}/login`, request);
  }

  validateToken(token: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/validate-token`, { jwt: token }, {
      headers: this.getAuthHeaders()
    });
  }

  getMenus(roleId?: number): Observable<AdminMenu[]> {
    return this.http.post<AdminMenu[]>(`${this.apiUrl}/menus`, { roleId: roleId || null }, {
      headers: this.getAuthHeaders()
    });
  }

  // Dashboard
  getDashboardStats(): Observable<DashboardStats> {
    return this.http.get<DashboardStats>(`${this.apiUrl}/dashboard/stats`, {
      headers: this.getAuthHeaders()
    });
  }

  // Products
  getProducts(): Observable<AdminProduct[]> {
    return this.http.get<AdminProduct[]>(`${this.apiUrl}/products`, {
      headers: this.getAuthHeaders()
    });
  }

  getProductDetail(id: number): Observable<AdminProductDetail> {
    return this.http.get<AdminProductDetail>(`${this.apiUrl}/products/${id}`, {
      headers: this.getAuthHeaders()
    });
  }

  getProductFormOptions(): Observable<ProductFormOptions> {
    return this.http.get<ProductFormOptions>(`${this.apiUrl}/product-options`, {
      headers: this.getAuthHeaders()
    });
  }

  uploadProductImage(file: File): Observable<ImageUploadResult> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    const token = this.authService.getToken();
    let headers = new HttpHeaders();
    if (token) headers = headers.set('Authorization', `Bearer ${token}`);
    return this.http.post<ImageUploadResult>(`${this.apiUrl}/upload-product-image`, formData, { headers });
  }

  createProduct(request: CreateProductRequest, userId: number): Observable<AdminResult> {
    return this.http.post<AdminResult>(`${this.apiUrl}/products?userId=${userId}`, request, {
      headers: this.getAuthHeaders()
    });
  }

  updateProduct(id: number, request: CreateProductRequest, userId: number): Observable<AdminResult> {
    return this.http.put<AdminResult>(`${this.apiUrl}/products/${id}?userId=${userId}`, request, {
      headers: this.getAuthHeaders()
    });
  }

  deactivateProduct(id: number, userId: number): Observable<AdminResult> {
    return this.http.put<AdminResult>(`${this.apiUrl}/products/${id}/deactivate?userId=${userId}`, null, {
      headers: this.getAuthHeaders()
    });
  }

  // Orders
  getOrders(): Observable<AdminOrder[]> {
    return this.http.get<AdminOrder[]>(`${this.apiUrl}/orders`, {
      headers: this.getAuthHeaders()
    });
  }

  getOrderDetail(id: number): Observable<AdminOrderDetail> {
    return this.http.get<AdminOrderDetail>(`${this.apiUrl}/orders/${id}`, {
      headers: this.getAuthHeaders()
    });
  }

  getOrderStatuses(): Observable<AdminOrderStatusOption[]> {
    return this.http.get<AdminOrderStatusOption[]>(`${this.apiUrl}/order-statuses`, {
      headers: this.getAuthHeaders()
    });
  }

  updateOrderStatus(id: number, request: AdminOrderStatusUpdateRequest, userId: number): Observable<AdminResult> {
    return this.http.post<AdminResult>(`${this.apiUrl}/orders/${id}/status?userId=${userId}`, request, {
      headers: this.getAuthHeaders()
    });
  }

  /**
   * Approves the refund a cancelled paid order is waiting for: the API sends the money back through
   * Razorpay and writes it into the order history. The message coming back says what happened; when
   * the refund has to be made by hand (nothing captured on record, or Razorpay refused) it says so and
   * 'markOrderRefunded' closes it.
   */
  approveOrderRefund(id: number, userId: number): Observable<AdminResult> {
    return this.http.post<AdminResult>(`${this.apiUrl}/orders/${id}/approve-refund?userId=${userId}`, null, {
      headers: this.getAuthHeaders()
    });
  }

  /**
   * Records that the refund went out in the Razorpay dashboard instead of through the app, so the
   * order stops being flagged as owing the money. The note is kept on the order's payment row.
   */
  markOrderRefunded(id: number, request: AdminOrderRefundRequest, userId: number): Observable<AdminResult> {
    return this.http.post<AdminResult>(`${this.apiUrl}/orders/${id}/mark-refunded?userId=${userId}`, request, {
      headers: this.getAuthHeaders()
    });
  }

  /**
   * The shipping providers this shop is set up with, for the Shipment card's provider picker. It comes
   * from the API's provider registry, so nothing about providers is hard-coded here; an entry whose
   * 'configured' is false is registered but has no credentials, and the card disables it.
   */
  getShipmentProviders(): Observable<AdminShipmentProvider[]> {
    return this.http.get<AdminShipmentProvider[]>(`${this.apiUrl}/shipment-providers`, {
      headers: this.getAuthHeaders()
    });
  }

  /**
   * Records the parcel the team booked in the provider's own panel (the AWB, and the courier and link
   * when they have them). The order is not moved by this - booking is not dispatching - and the answer
   * is the parcel card again, with the message saying what was written down.
   */
  saveOrderShipment(id: number, request: AdminSaveOrderShipmentRequest, userId: number): Observable<AdminOrderShipment> {
    return this.http.post<AdminOrderShipment>(`${this.apiUrl}/orders/${id}/shipment?userId=${userId}`, request, {
      headers: this.getAuthHeaders()
    });
  }

  /**
   * 'Track now': asks the courier about this order's parcel immediately and records what it said,
   * moving the order (and its units) along when the courier's own status allows it. Never throttled,
   * because the shop team asked on purpose; an unreachable courier comes back as a message, with the
   * order left as it was.
   */
  trackOrderShipment(id: number, userId: number): Observable<AdminOrderShipment> {
    return this.http.post<AdminOrderShipment>(`${this.apiUrl}/orders/${id}/track-shipment?userId=${userId}`, null, {
      headers: this.getAuthHeaders()
    });
  }

  /**
   * The shop team's answer to a return: approve it (the parcel is coming back, so the pickup is booked
   * from the same card) or refuse it (the order is left exactly as it was). The note is required - it is
   * what the customer is told - and an ask that has already been answered is refused by the API, so one
   * return can never be approved twice.
   */
  decideOrderReturn(id: number, approved: boolean, comment: string, userId: number): Observable<AdminResult> {
    return this.http.post<AdminResult>(`${this.apiUrl}/orders/${id}/returns/decision?userId=${userId}`,
      { approved, comment }, {
      headers: this.getAuthHeaders()
    });
  }

  /**
   * 'Parcel received' on the Return card: the parcel is physically back with the shop. The return becomes
   * 'Received' and its units come off the delivery pools (off sale until the return is closed); the order and the
   * money are left exactly as they are - both move when the return is closed. The note is optional and goes on
   * the order's history, which the customer reads in 'My Orders'.
   */
  markOrderReturnReceived(id: number, comment: string, userId: number): Observable<AdminResult> {
    return this.http.post<AdminResult>(`${this.apiUrl}/orders/${id}/returns/received?userId=${userId}`,
      { comment }, {
      headers: this.getAuthHeaders()
    });
  }

  /**
   * The inspection of a parcel that came back: every SKU in 'damagedSkus' is a unit the team found broken, and
   * each one is written off - out of every sellable pool for good, so closing the return cannot put it back on
   * sale. A request with no units is still recorded ('we looked and found nothing'), which is deliberately a
   * different thing from nobody having looked at all.
   */
  markOrderReturnUnitsDamaged(id: number, damagedSkus: string[], comment: string, userId: number): Observable<AdminResult> {
    return this.http.post<AdminResult>(`${this.apiUrl}/orders/${id}/returns/inspection?userId=${userId}`,
      { damagedSkus, comment }, {
      headers: this.getAuthHeaders()
    });
  }

  /**
   * 'Close return' - the step that settles a return: the order becomes 'Returned', every unit the return brought
   * back goes on sale again (the ones written off stay written off) and the refund of what the customer paid is
   * asked for. Sending that money is a separate, deliberate step on the Refund card ('Approve refund'), so
   * nothing leaves for the gateway from here.
   */
  closeOrderReturn(id: number, comment: string, userId: number): Observable<AdminResult> {
    return this.http.post<AdminResult>(`${this.apiUrl}/orders/${id}/returns/close?userId=${userId}`,
      { comment }, {
      headers: this.getAuthHeaders()
    });
  }

  // Customers
  getCustomers(search?: string): Observable<AdminCustomer[]> {
    const params = search ? `?search=${encodeURIComponent(search)}` : '';
    return this.http.get<AdminCustomer[]>(`${this.apiUrl}/customers${params}`, {
      headers: this.getAuthHeaders()
    });
  }

  getCustomerDetail(id: number): Observable<AdminCustomerDetail> {
    return this.http.get<AdminCustomerDetail>(`${this.apiUrl}/customers/${id}`, {
      headers: this.getAuthHeaders()
    });
  }

  updateCustomerStatus(customerId: number, customerStatusId: number): Observable<AdminResult> {
    return this.http.put<AdminResult>(`${this.apiUrl}/customers/${customerId}/status`,
      { customerId, customerStatusId },
      { headers: this.getAuthHeaders() }
    );
  }

  // Partners

  getPartners(): Observable<AdminPartner[]> {
    return this.http.get<AdminPartner[]>(`${this.apiUrl}/partners`, {
      headers: this.getAuthHeaders()
    });
  }

  getPartnerDetail(id: number): Observable<AdminPartnerDetail> {
    return this.http.get<AdminPartnerDetail>(`${this.apiUrl}/partners/${id}`, {
      headers: this.getAuthHeaders()
    });
  }

  getPartnerStatuses(): Observable<PartnerStatusOption[]> {
    return this.http.get<PartnerStatusOption[]>(`${this.apiUrl}/partner-statuses`, {
      headers: this.getAuthHeaders()
    });
  }

  createPartner(request: PartnerFormRequest, userId: number): Observable<AdminResult> {
    return this.http.post<AdminResult>(`${this.apiUrl}/partners?userId=${userId}`, request, {
      headers: this.getAuthHeaders()
    });
  }

  updatePartner(id: number, request: PartnerFormRequest, userId: number): Observable<AdminResult> {
    return this.http.put<AdminResult>(`${this.apiUrl}/partners/${id}?userId=${userId}`, request, {
      headers: this.getAuthHeaders()
    });
  }

  // Vendors
  getVendors(): Observable<AdminVendor[]> {
    return this.http.get<AdminVendor[]>(`${this.apiUrl}/vendors`, {
      headers: this.getAuthHeaders()
    });
  }

  getVendorDetail(id: number): Observable<AdminVendorDetail> {
    return this.http.get<AdminVendorDetail>(`${this.apiUrl}/vendors/${id}`, {
      headers: this.getAuthHeaders()
    });
  }

  createVendor(request: VendorFormRequest, userId: number): Observable<AdminResult> {
    return this.http.post<AdminResult>(`${this.apiUrl}/vendors?userId=${userId}`, request, {
      headers: this.getAuthHeaders()
    });
  }

  updateVendor(id: number, request: VendorFormRequest, userId: number): Observable<AdminResult> {
    return this.http.put<AdminResult>(`${this.apiUrl}/vendors/${id}?userId=${userId}`, request, {
      headers: this.getAuthHeaders()
    });
  }

  // Purchases
  getPurchases(): Observable<AdminPurchase[]> {
    return this.http.get<AdminPurchase[]>(`${this.apiUrl}/purchases`, {
      headers: this.getAuthHeaders()
    });
  }

  getPurchaseDetail(id: number): Observable<AdminPurchaseDetail> {
    return this.http.get<AdminPurchaseDetail>(`${this.apiUrl}/purchases/${id}`, {
      headers: this.getAuthHeaders()
    });
  }

  getPurchasers(): Observable<AdminPurchaser[]> {
    return this.http.get<AdminPurchaser[]>(`${this.apiUrl}/purchasers`, {
      headers: this.getAuthHeaders()
    });
  }

  createPurchase(request: AdminPurchaseFormRequest, userId: number): Observable<AdminResult> {
    return this.http.post<AdminResult>(`${this.apiUrl}/purchases?userId=${userId}`, request, {
      headers: this.getAuthHeaders()
    });
  }

  getPurchaseStatuses(id: number, userId: number): Observable<AdminPurchaseStatus[]> {
    return this.http.get<AdminPurchaseStatus[]>(`${this.apiUrl}/purchases/${id}/statuses?userId=${userId}`, {
      headers: this.getAuthHeaders()
    });
  }

  updatePurchaseStatus(id: number, request: AdminPurchaseStatusUpdateRequest, userId: number): Observable<AdminResult> {
    return this.http.post<AdminResult>(`${this.apiUrl}/purchases/${id}/status?userId=${userId}`, request, {
      headers: this.getAuthHeaders()
    });
  }

  updatePurchase(id: number, request: AdminPurchaseUpdateRequest, userId: number): Observable<AdminResult> {
    return this.http.put<AdminResult>(`${this.apiUrl}/purchases/${id}?userId=${userId}`, request, {
      headers: this.getAuthHeaders()
    });
  }

  savePurchaseItems(id: number, items: AdminPurchaseItemSave[], userId: number): Observable<AdminResult> {
    return this.http.put<AdminResult>(`${this.apiUrl}/purchases/${id}/items?userId=${userId}`, { items }, {
      headers: this.getAuthHeaders()
    });
  }

  addPurchaseComment(id: number, comments: string, userId: number): Observable<AdminResult> {
    return this.http.post<AdminResult>(`${this.apiUrl}/purchases/${id}/comments?userId=${userId}`, { comments }, {
      headers: this.getAuthHeaders()
    });
  }

  // Admin Users
  getAdminUsers(): Observable<AdminUserList[]> {
    return this.http.get<AdminUserList[]>(`${this.apiUrl}/users`, {
      headers: this.getAuthHeaders()
    });
  }

  getAdminUser(id: number): Observable<AdminUserDetail> {
    return this.http.get<AdminUserDetail>(`${this.apiUrl}/users/${id}`, {
      headers: this.getAuthHeaders()
    });
  }

  getAdminRoles(): Observable<AdminRole[]> {
    return this.http.get<AdminRole[]>(`${this.apiUrl}/roles`, {
      headers: this.getAuthHeaders()
    });
  }

  createAdminUser(request: AdminUserFormRequest, userId: number): Observable<AdminResult> {
    return this.http.post<AdminResult>(`${this.apiUrl}/users?userId=${userId}`, request, {
      headers: this.getAuthHeaders()
    });
  }

  updateAdminUser(id: number, request: AdminUserFormRequest, userId: number): Observable<AdminResult> {
    return this.http.put<AdminResult>(`${this.apiUrl}/users/${id}?userId=${userId}`, request, {
      headers: this.getAuthHeaders()
    });
  }

  // Categories
  getCategories(): Observable<AdminCategory[]> {
    return this.http.get<AdminCategory[]>(`${this.apiUrl}/categories`, {
      headers: this.getAuthHeaders()
    });
  }

  // Forgot Password
  forgotPassword(loginId: string): Observable<AdminResult> {
    return this.http.post<AdminResult>(`${this.apiUrl}/forgot-password`, { loginId });
  }

  // Reset Password
  resetPassword(token: string, newPassword: string): Observable<AdminResult> {
    return this.http.post<AdminResult>(`${this.apiUrl}/reset-password`, { token, newPassword });
  }
}


