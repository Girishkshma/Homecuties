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
  cancelledOrders: number;

  // The operational order/shipment tiles. They follow the same order lifecycle and parcel mapping the
  // order screens read, so a count here can never disagree with the order it counts.
  ordersToDispatch: number;
  shipmentsInProgress: number;
  outForDelivery: number;
  deliveredOrders: number;
  shipmentsNeedingAttention: number;
  refundsDue: number;

  // The returns side of the same 'what needs doing now' work, in the vocabulary the Return card uses
  // (OrderReturnStatus): an ask waiting for the team's answer, a parcel on its way back, and one that is back
  // with the shop waiting to be looked over and closed. 'returnedOrders' is the other end of it - the orders
  // whose units came back - and is counted on its own tile, exactly like 'cancelledOrders'.
  returnsAwaitingDecision: number;
  returnsComingBack: number;
  returnsReceived: number;
  returnedOrders: number;
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
  /** Customers.CustomerID - the customer's identity, shown so same-named customers can be told apart. */
  customerId: number;
  customerEmail: string;
  statusId: number;
  status: string;
  isPaid: boolean;
  totalAmount: number;
  itemCount: number;

  /**
   * True while the order's money is owed back: a paid order the customer cancelled, so the refund is
   * waiting for the shop team to approve it (plus the refunds Razorpay refused). The list flags these
   * as 'Refund due'.
   */
  refundPending: boolean;

  /**
   * Where the order's return has got to ('Requested', 'Arranged', 'Received', 'Closed', 'Rejected',
   * 'Withdrawn'), or null when the order never had one. The newest return counts.
   */
  returnStatus?: string | null;

  /** True while the order's return is live - waiting for an answer, or with its parcel on the way back. */
  returnPending: boolean;
}

export interface AdminOrderDetail {
  orderId: number;
  orderNumber: string;
  orderDate: Date;
  customerName: string;
  customerId: number;
  customerEmail: string;
  customerMobile: string;
  statusId: number;
  status: string;
  isPaid: boolean;
  totalAmount: number;
  sellerName: string;
  billingAddress: AdminAddress;
  shippingAddress: AdminAddress;
  items: AdminOrderItem[];
  history: AdminOrderHistory[];
  availableStatuses: AdminOrderStatusOption[];

  /**
   * The state of the money when the order was cancelled after payment. True while the refund is owed -
   * 'Approve refund' sends it to Razorpay and 'Mark refunded' records one the team made by hand.
   */
  refundPending: boolean;
  refundRequestedOn?: Date;
  refundRequestedComment?: string;
  refundAmount?: number;
  refundId?: string;
  refundStatus?: string;
  refundedOn?: Date;
  refundFailureReason?: string;
  /** The captured Razorpay payment a refund is sent against; absent means it must be refunded by hand. */
  razorpayPaymentId?: string;

  /**
   * The parcel of this order as it was last written down, or absent while the shop has not recorded one.
   * The Shipment card records it ('Save parcel') and asks the courier about it ('Track now'), which is
   * also the only thing that moves the order for a parcel.
   */
  shipment?: AdminOrderShipment;

  /**
   * The return this order carries, or absent while it has never had one. The Return card is built from
   * it: the ask, the shop team's answer, the pickup once one is booked, and - later - the parcel being
   * back and the return being closed (see HC.Business.OrderReturnFlow).
   */
  return?: AdminOrderReturn;
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
  statusId: number;
  status: string;
  comments: string;
}

/** One step of the order lifecycle (Orders.OrderStatusID + its name). */
export interface AdminOrderStatusOption {
  statusId: number;
  status: string;
}

/** Body of "move this order to another status" in the admin order screen. */
/**
 * Body of "move this order to another status" in the admin order screen.
 *
 * Shipped is the one step that carries more than a status: the order is dispatched with the parcel it
 * went out as, so the provider it was booked with and the consignment number that provider gave can be
 * sent with the move. Neither is required - an order that went out without a courier (handed over in
 * person, or given to a delivery service this shop is not set up with) has no parcel and is simply
 * dispatched - and when they are sent, the server records them against the order and every later courier
 * call is made through that provider's own adapter.
 */
export interface AdminOrderStatusUpdateRequest {
  statusId: number;
  comments: string;

  /** Registry name of the provider the parcel was booked with; blank means the shop's default. */
  provider?: string | null;
  /** The consignment number (AWB) the provider gave this parcel; blank means no courier was involved. */
  awbNumber?: string | null;
  /** The courier the provider handed the parcel to, when the team knows it (optional). */
  courierName?: string | null;
  /**
   * What the courier billed the shop for this parcel, when the team has it (optional) - kept on the
   * parcel for the books and never shown to the customer. A blank box sends nothing, which leaves
   * whatever the Shipment card already recorded.
   */
  freightCharge?: number | null;
}

/**
 * Body of "record that this refund was made by hand" in the admin order screen: the refund went out
 * in the Razorpay dashboard, so the note says how (it ends up on the order's payment row and history).
 */
export interface AdminOrderRefundRequest {
  comment: string;
}

/**
 * One parcel of an order, as the admin API sends it (the API's OrderShipmentDto - camelCase, unlike the
 * older PascalCase storefront payloads). It is what the Shipment card shows: what was recorded, where
 * the courier says it is ('status'/'stage'), and which order status that mapped to ('orderStatus').
 */
export interface AdminOrderShipment {
  result: number;
  messages: string[];

  orderId: number;
  /** False while no parcel has been recorded, in which case the rest is empty. */
  hasShipment: boolean;
  /** Registry name of the provider, e.g. 'Shiprocket' or 'Custom' (the shop's own service). */
  provider: string;
  /**
   * False when the parcel went out with a service the shop arranges itself: there is no courier to ask, so
   * no 'Track now' is offered for it and the order is moved along by hand. The AWB is a real reference
   * either way - the shop's own one in that case.
   */
  reportsTracking: boolean;
  courierName: string;
  awbNumber: string;
  trackingUrl: string;
  /**
   * What the courier billed the shop for this parcel, when the team recorded it with the AWB - the
   * shop's own figure for the books ('OrderShipments.FreightCharge'). The customer's page is answered
   * without it, so it is shown here and nowhere else. Null while nobody has recorded it.
   */
  freightCharge?: number | null;

  /** The courier's own wording for where the parcel is, exactly as the provider sent it. */
  providerStatus: string;
  providerStatusCode?: number | null;
  /** The shop's wording derived from the courier's status (see HC.Business.ShipmentStatusFlow). */
  status: string;
  /** Booked | InTransit | OutForDelivery | Delivered | Undelivered | Refused | Rto | Cancelled | Unknown. */
  stage: string;
  delivered: boolean;
  /** True once the parcel is finished - delivered or returned - so nothing more is asked of the courier. */
  closed: boolean;
  deliveredOn?: Date | null;
  lastStatusText: string;
  lastCheckedOn?: Date | null;

  /** The order status the courier's report moved the order to, once it has been applied. */
  orderStatusId?: number | null;
  orderStatus?: string | null;
}

/**
 * A return of an order as the Return card shows it (see HC.Business.OrderReturnFlow). An ask comes either
 * from the customer ('My Orders', on a delivered order) or from the courier's own tracking report (a
 * refusal, or a parcel that could not be delivered) - 'originLabel' says which, in words, because an ask
 * nobody made in words has to read differently on the screen.
 *
 * Nothing here carries money on purpose: the refund belongs to the payment and is on the same order detail
 * ('refundPending' and the fields beside it), so it can never be described in two places.
 */
export interface AdminOrderReturn {
  returnId: number;
  /** 'Customer' or 'Courier' (OrderReturnOrigin). */
  origin: string;
  /** What the screen says about that: 'Requested by the customer' / 'Reported by the courier'. */
  originLabel: string;
  /** OrderReturnStatus.*: Requested / Arranged / Received / Closed / Rejected / Withdrawn. */
  status: string;
  /** OrderReturnReason.* - the coded reason returns are counted by. */
  reasonCode: string;
  /** The wording for 'reasonCode' - the screen never invents its own name for a reason. */
  reasonLabel: string;
  /** Why, in the asker's own words (the courier's wording for a courier-raised ask). */
  reason: string;
  requestedOn: Date;
  /** The customer's e-mail for an ask they made, null for a courier's own report. */
  requestedBy?: string | null;
  decisionOn?: Date | null;
  /** The admin login id that approved or refused the ask. */
  decisionBy?: string | null;
  /** The note they gave with their answer - what the customer is told, which is why it is required. */
  decisionComment?: string | null;
  /** When the parcel was physically back with the shop. */
  receivedOn?: Date | null;
  /** When the return was closed: the order 'Returned', its units on the shelf, its money owed back. */
  closedOn?: Date | null;
  inspectionOn?: Date | null;
  inspectionBy?: string | null;
  /** What the inspection of the returned parcel found (the damaged units are in the stock trail). */
  inspectionComment?: string | null;
  /** True while the shop team can still answer it - exactly when the card offers Approve and Refuse. */
  canDecide: boolean;
  /** True while the parcel can be booked back in (the card offers 'Parcel received') - an approved return. */
  canMarkReceived: boolean;
  /**
   * True while the returned parcel can be looked over (the card offers the inspection): it is back with the
   * shop, so a unit that came back broken can be written off before the return is closed.
   */
  canInspect: boolean;
  /**
   * True while the return can be closed (the card offers 'Close return'): the order becomes 'Returned', the
   * returned units go back on sale and the refund is asked for.
   */
  canClose: boolean;
  /** True while the return is live - waiting for an answer, or with its parcel on the way back. */
  isOpen: boolean;
  /** The parcel coming back (recorded with 'direction: Reverse'), absent until one is booked. */
  shipment?: AdminOrderShipment;
}

/**
 * One shipping provider this shop is set up with, from the provider registry - so the card offers what
 * is really wired up. 'configured' is false when the provider is registered without credentials (or the
 * adapter is missing), which is why it is shown but cannot be picked.
 */
export interface AdminShipmentProvider {
  name: string;
  displayName: string;
  configured: boolean;
  isDefault: boolean;
  /**
   * True for a provider whose reference the system mints itself (the shop's own delivery service, which
   * has no panel to take an AWB from): the AWB boxes then accept blank and a reference is written for the
   * parcel instead.
   */
  awbGeneratedBySystem: boolean;
  /** False for a provider with no courier behind it: there is nothing to ask, so no 'Track now' is offered. */
  reportsTracking: boolean;
  apiBaseUrl: string;
  trackingPath: string;
}

/**
 * Body of "record this parcel" in the admin order screen. The AWB is the one required field - unless the
 * provider picked mints the reference itself ('awbGeneratedBySystem'), where a blank one means 'give this
 * parcel a reference'; the courier name and tracking link are taken when the shop team has them (and
 * filled in by the first 'Track now').
 */
export interface AdminSaveOrderShipmentRequest {
  provider?: string | null;
  awbNumber: string;
  courierName?: string | null;
  trackingUrl?: string | null;
  shiprocketShipmentId?: number | null;
  /**
   * What the courier billed the shop for this parcel, when the team has it (optional) - the shop's own
   * figure, kept for the books. A blank box sends nothing, which leaves what is recorded alone, so this
   * can be filled in on one visit and completed on a later one.
   */
  freightCharge?: number | null;

  /**
   * Which leg of the order this parcel is: sent as 'Reverse' from the Return card (the pickup coming
   * back), and left out everywhere else - a blank one means the parcel that went out, which is what the
   * Shipment card and the Shipped move mean.
   */
  direction?: string | null;
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
