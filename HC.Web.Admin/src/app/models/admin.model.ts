export interface AdminLoginRequest {
  loginId: string;
  password: string;
}

export interface AdminLoginResponse {
  result: number;
  messages: string[];
  user?: AdminUser;
  token?: string;
  expiresOn?: Date;
}

export interface AdminUser {
  userId: number;
  loginId: string;
  firstName: string;
  middleName?: string;
  lastName?: string;
  emailId?: string;
  mobileNumber?: string;
  isActive: boolean;
  roles: AdminRole[];
}

export interface AdminRole {
  roleId: number;
  roleName: string;
  roleDescription?: string;
}

export interface AdminMenu {
  menuId: number;
  menuTitle: string;
  menuDescription?: string;
  menuUrl: string;
  parentMenuId?: number;
  isActive: boolean;
  children: AdminMenu[];
  activities: AdminActivity[];
}

export interface AdminActivity {
  activityId: number;
  activityTitle: string;
  menuId: number;
  isActive: boolean;
}

export interface DashboardStats {
  totalProducts: number;
  totalOrders: number;
  totalCustomers: number;
  totalPartners: number;
  totalVendors: number;
  pendingOrders: number;
  todayRevenue: number;
  monthlyRevenue: number;
}

export interface AdminProduct {
  productId: number;
  productName: string;
  productTitle: string;
  unitPrice: number;
  status: string;
  displayOnHomePage: boolean;
  createdOn: Date;
  createdBy: string;
}

export interface AdminProductDetail {
  productId: number;
  productName: string;
  productTitle: string;
  productDescription: string;
  displayOnHomePage: boolean;
  productStatusId: number;
  unitPrice: number;
  hsncode?: string;
  packagingCharge: number;
  storageCharge: number;
  discountPercent: number;
  additionalDiscountPercent: number;
  deliveryCharge: number;
  profitMarginPercent: number;
  cgstpercent: number;
  sgstpercent: number;
  igstpercent: number;
  categoryIds: number[];
  features: AdminProductFeature[];
  images: AdminProductImage[];
}

export interface AdminProductFeature {
  productFeatureId?: number;
  feature: string;
  isActive: boolean;
}

export interface AdminProductImage {
  productImageId?: number;
  imageUrl: string;
  imageTypeId: number;
  imageIndex: number;
  isPromoImage: boolean;
  isActive: boolean;
}

export interface CreateProductRequest {
  productName: string;
  productTitle: string;
  productDescription: string;
  displayOnHomePage: boolean;
  productStatusId: number;
  unitPrice: number;
  hsncode?: string;
  packagingCharge: number;
  storageCharge: number;
  discountPercent: number;
  additionalDiscountPercent: number;
  deliveryCharge: number;
  profitMarginPercent: number;
  cgstpercent: number;
  sgstpercent: number;
  igstpercent: number;
  categoryIds: number[];
  features: AdminProductFeature[];
  images: AdminProductImage[];
}

export interface ProductStatusOption {
  productStatusId: number;
  productStatusName: string;
}

export interface ImageTypeOption {
  imageTypeId: number;
  imageTypeName: string;
  shortCode: string;
}

export interface ProductFormOptions {
  statuses: ProductStatusOption[];
  imageTypes: ImageTypeOption[];
}

export interface ImageUploadResult {
  result: number;
  messages: string[];
  fileName: string;
  url: string;
}

export interface AdminOrder {
  orderId: number;
  orderNumber: string;
  orderDate: Date;
  customerName: string;
  status: string;
  totalAmount: number;
  itemCount: number;
}

export interface AdminOrderDetail {
  orderId: number;
  orderNumber: string;
  orderDate: Date;
  customerName: string;
  customerEmail: string;
  status: string;
  sellerName: string;
  billingAddress: AdminAddress;
  shippingAddress: AdminAddress;
  items: AdminOrderItem[];
  history: AdminOrderHistory[];
}

export interface AdminAddress {
  addressTitle: string;
  contactName: string;
  addressLine1: string;
  addressLine2?: string;
  city: string;
  state: string;
  zipcode: string;
  mobileNumber: string;
}

export interface AdminOrderItem {
  sku: string;
  productName: string;
  productTitle: string;
  unitPrice: number;
  discountPercent: number;
  additionalDiscountPercent: number;
  deliveryCharge: number;
  packagingCharge: number;
  storageCharge: number;
  profitMarginPercent: number;
  cgstpercent: number;
  sgstpercent: number;
  igstpercent: number;
}

export interface AdminOrderHistory {
  historyDate: Date;
  status: string;
  comments: string;
}

export interface AdminCustomer {
  customerId: number;
  firstName: string;
  middleName?: string;
  lastName?: string;
  emailId: string;
  mobileNumber?: string;
  mobileVerified?: boolean;
  emailVerified: boolean;
  createdOn: Date;
  modifiedOn: Date;
  customerStatusId: number;
  status: string;
}

export interface AdminCustomerDetail {
  customerId: number;
  firstName: string;
  middleName?: string;
  lastName?: string;
  emailId: string;
  mobileNumber?: string;
  mobileVerified?: boolean;
  emailVerified: boolean;
  createdOn: Date;
  modifiedOn: Date;
  customerStatusId: number;
  status: string;
  addresses: AdminCustomerAddress[];
  orderCount: number;
  totalSpent: number;
}

export interface AdminCustomerAddress {
  addressId: number;
  addressTitle: string;
  contactName: string;
  addressLine1: string;
  addressLine2?: string;
  city: string;
  state: string;
  country: string;
  zipcode: string;
  mobileNumber: string;
}


export interface AdminPartner {
  partnerId: number;
  partnerName: string;
  partnerStatusId: number;
  status: string;
  lastModifiedOn: Date;
}

export interface AdminPartnerDetail {
  partnerId: number;
  partnerName: string;
  partnerStatusId: number;
  status: string;
  lastModifiedOn: Date;
  users: AdminPartnerUser[];
  inventoryCount: number;
  orderCount: number;
}

export interface AdminPartnerUser {
  userId: number;
  userName: string;
  loginId: string;
  emailId?: string;
  mobileNumber?: string;
  isActive: boolean;
  roles: string[];
}

export interface PartnerStatusOption {
  partnerStatusId: number;
  partnerStatus: string;
}

export interface PartnerFormRequest {
  partnerName: string;
  partnerStatusId: number;
}

export interface AdminVendor {
  vendorId: number;
  vendorName: string;
  vendorAddress?: string;
  mobile: string;
  isActive: boolean;
}

export interface AdminVendorDetail {
  vendorId: number;
  vendorName: string;
  vendorAddress?: string;
  mobile: string;
  remarks?: string;
  isActive: boolean;
  users: AdminVendorUser[];
  purchaseCount: number;
}

export interface AdminVendorUser {
  userId: number;
  userName: string;
  loginId: string;
  emailId?: string;
  mobileNumber?: string;
  isActive: boolean;
  roles: string[];
}

export interface VendorFormRequest {
  vendorName: string;
  vendorAddress?: string;
  mobile: string;
  remarks?: string;
  isActive: boolean;
}

export interface AdminPurchase {
  purchaseId: number;
  purchaseNumber: string;
  vendorId: number;
  vendorName: string;
  purchaserName: string;
  purchaseDate: Date;
  purchaseStatusId: number;
  status: string;
  itemCount: number;
  totalAmount: number;
}

export interface AdminPurchaseDetail {
  purchaseId: number;
  purchaseNumber: string;
  vendorId: number;
  vendorName: string;
  purchaserId: number;
  purchaserName: string;
  purchaseDate: Date;
  purchaseStatusId: number;
  status: string;
  invoicePath?: string;
  addedByName: string;
  addedOn: Date;
  lastModifiedByName: string;
  lastModifiedOn: Date;
  items: AdminPurchaseItem[];
  comments: AdminPurchaseComment[];
}

export interface AdminPurchaseItem {
  purchaseDetailId: number;
  productId: number;
  productName: string;
  quantity: number;
  unitPrice: number;
  gst: number;
  lineTotal: number;
}

export interface AdminPurchaseComment {
  purchaseCommentId: number;
  comments: string;
  addedByName: string;
  addedOn: Date;
}

export interface AdminPurchaser {
  purchaserId: number;
  purchaserName: string;
  partnerName: string;
}

export interface AdminPurchaseFormItem {
  productId: number;
  quantity: number;
  unitPrice: number;
  gst: number;
}

export interface AdminPurchaseFormRequest {
  vendorId: number;
  purchaserId: number;
  purchaseDate: string;
  invoicePath?: string;
  purchaseStatusId: number;
  items: AdminPurchaseFormItem[];
}

export interface AdminPurchaseStatus {
  purchaseStatusId: number;
  purchaseStatusName: string;
}

export interface AdminPurchaseStatusUpdateRequest {
  statusId: number;
  comments?: string;
}

export interface AdminPurchaseUpdateRequest {
  vendorId: number;
  purchaserId: number;
  purchaseDate: string;
  invoicePath?: string;
}

export interface AdminPurchaseItemSave {
  purchaseDetailId: number;
  productId: number;
  quantity: number;
  unitPrice: number;
  gst: number;
}

export interface AdminPurchaseCommentRequest {
  comments: string;
}

export interface AdminUserList {
  userId: number;
  loginId: string;
  firstName: string;
  lastName?: string;
  emailId?: string;
  isActive: boolean;
  roles: string[];
}

export interface AdminUserDetail {
  userId: number;
  loginId: string;
  firstName: string;
  middleName?: string;
  lastName?: string;
  emailId?: string;
  mobileNumber?: string;
  isActive: boolean;
  mustChangePassword: boolean;
  roles: AdminRole[];
}

export interface AdminUserFormRequest {
  loginId: string;
  password?: string;
  firstName: string;
  middleName?: string;
  lastName?: string;
  emailId?: string;
  mobileNumber?: string;
  isActive: boolean;
  mustChangePassword: boolean;
  roleIds: number[];
}

export interface AdminCategory {
  categoryId: number;
  categoryName: string;
  parentCategoryId?: number;
  parentCategoryName?: string;
}

export interface AdminResult {
  result: number;
  messages: string[];
}
