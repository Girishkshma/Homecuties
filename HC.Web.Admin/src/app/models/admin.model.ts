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

  /**
   * What the gateway took today, less the refunds sent back today (HC.Business.OrderMoney) - read from the
   * payment rows rather than from the order lines, so it is money in the bank and not a count of open orders.
   * A refund of an older sale is a minus on the day the money went out, exactly as the bank statement reads.
   */
  todayRevenue: number;

  /** The same reading over the month so far: one rule (taken minus refunded) over two windows. */
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

/**
 * The days a settlement pull covers ('POST settlements/sync'), which is the one action that brings the money
 * tiles up to date with what the gateway really settled to the shop's bank account.
 *
 * Both days are optional: leaving them out asks for the rolling window the pull is meant to read (yesterday and
 * the seven days before it), and naming a day - or a range - is how a day the books do not explain is pulled
 * again without waiting for the next pass. The server trims whatever is asked for to the days it is willing to
 * read and reads a window that runs backwards as the single day it names.
 */
export interface AdminSettlementSyncRequest {
  /** The first day to read, 'YYYY-MM-DD'; null or absent for the start of the rolling window. */
  from?: string | null;
  /** The last day to read, 'YYYY-MM-DD'; null or absent for yesterday. */
  to?: string | null;
}

/**
 * The shop's own books over a period ('GET finance/summary' - see HC.Business.OrderMoney and
 * HC.Business.AdminDashboardService.Finance): what the customers paid, what was given back, what the gateway
 * kept for taking it, what the couriers were paid, what the shop's own declared margin on the goods was, how
 * much of that money is held for the government as GST, and what the bank side of it looked like.
 *
 * Every figure comes from a row the shop already keeps, and 'messages' says what the figures cannot say by
 * themselves - a charge the gateway never reported, a parcel with no courier bill, days the settlement pull has
 * no lines for. 'from' and 'to' are the days the server actually read (leaving both empty in the request asks
 * for the month to date), so the screen reports the period it really got rather than the one it asked for.
 */
export interface AdminFinanceSummary {
  /** The first day the report covers. */
  from: Date;
  /** The last day the report covers - today at the most. */
  to: Date;

  /**
   * The partner these books are for, or null when they are the whole shop's. Whose books come back follows the
   * signed-in admin and never the request: an admin or a super admin reads the shop's, and a user the shop has linked
   * to a partner reads their own goods' sales alone - each shared order's money cut to their own lines' share of it.
   * Null as well when the acting admin is linked to several partners and so reads them together, in which case
   * partnerName names every one of them.
   */
  partnerId?: number | null;
  /**
   * The name of that partner (all of their names when the admin reads several together), so the screen can say whose
   * books these are; null for the whole shop's.
   */
  partnerName?: string | null;

  // The customers' money, as the gateway reported it (the payment rows).
  capturedCount: number;
  /** What the gateway took - what the customers paid, before anything is taken off. */
  grossSales: number;
  refundCount: number;
  /** What was given back, including refunds still travelling. */
  refundsGiven: number;
  /** grossSales less refundsGiven. */
  netSales: number;

  // What the gateway kept for taking that money. The charges are the capture's own estimate until the
  // settlement pull corrects them, which is what the three counts are for.
  gatewayFee: number;
  gatewayTax: number;
  gatewayCharges: number;
  /** netSales less gatewayCharges - what the gateway handed over. */
  fromTheGateway: number;
  /** Payments carrying the settled figure (the one the bank was paid on). */
  settledChargeCount: number;
  /** Payments still carrying the capture's own estimate. */
  estimatedChargeCount: number;
  /** Payments with no charge recorded at all, so the fee lines are short by them. */
  unknownChargeCount: number;

  // What the parcels of that money cost (the courier bills recorded on the parcels).
  parcelsShipped: number;
  parcelsWithoutFreight: number;
  freightCost: number;
  /** fromTheGateway less freightCost. */
  afterCourier: number;

  // The shop's own declared margin - the 'Profit margin %' typed on each product line, not a purchase price.
  declaredMargin: number;
  declaredCost: number;

  // The GST: the part of the customers' money that is held for the government rather than earned. The sales
  // figures above are what the customers actually paid, so they include this tax - it is shown beside them rather
  // than taken out of them. Read from the rates the order lines recorded, at the rate the checkout charged.
  /** The value the GST was charged on: the period's lines before tax (the price less both discounts). */
  taxableValue: number;
  /** The GST charged on those lines (the CGST the checkout charged, which is what the customer really paid). */
  outputGst: number;
  /** The GST given back with the period's refunds, each for its own order's share of the tax. */
  gstOnRefunds: number;
  /** outputGst less gstOnRefunds - what is still held for the government. */
  gstHeld: number;
  /**
   * gstHeld less the GST on the gateway's fee (gatewayTax, which is input credit). Negative when the credit is
   * larger than the tax held - that difference carries forward rather than being lost.
   */
  netGstPayable: number;

  // The bank's side, from the settlement ledger the pull writes; empty until the pull has run for those days.
  settledLines: number;
  settledCredit: number;
  settledDebit: number;
  settledFee: number;
  settledTax: number;
  settledOnHold: number;

  // The per-unit breakdown, read from the split the shop records (OrderItemMoney) rather than worked out at read
  // time. It is cut to the same orders the figures above are about, and to the same goods when these are a
  // partner's books.
  /**
   * The period's orders whose lines have no per-unit figures written yet - the ones the breakdown below cannot
   * speak for (they predate the table; the backfill fills them in). The figures above do not depend on them.
   */
  lineItemsWithoutMoney: number;
  /** What each SKU of this period's sales carried, money-wise (most valuable first). */
  lineItems: AdminFinanceLine[];

  /** What the figures cannot say by themselves, in plain words (empty when there is nothing to add). */
  messages: string[];
}

/**
 * What one SKU of the period's orders carried, money-wise - one row of the Finance screen's per-SKU breakdown.
 *
 * It is the split the shop records (OrderItemMoney), not a read-time share: an order's gateway charge, the GST on
 * it and the couriers' bills are cut across that order's lines by what each line was worth, so these rows add back
 * up to the order-level figures they came from.
 *
 * The gateway and courier figures are null when nothing has reported them - 'not known', never a zero that reads as
 * free - and 'chargesSource' says which word the total rests on: 'Recon' (the settlement line the bank was actually
 * paid on, which is authoritative) or 'Payment' (the capture's own report, which the settlement pull can still
 * correct). A mixture is reported as the weaker of the two.
 */
export interface AdminFinanceLine {
  sku: string;
  /** How many units of it the period's orders held. */
  units: number;
  /** What those units were worth to the customers - the weight every figure here was split by. */
  lineValue: number;
  /** The output GST inside that money: tax held for the government on these units. */
  outputGst: number;
  gatewayFee?: number | null;
  gatewayTax?: number | null;
  freightShare?: number | null;
  chargesSource?: string | null;
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
   *
   * An order can go out in MORE than one parcel, and this is the first of them - what a screen with room for one
   * parcel means by 'the parcel'. 'shipments' below is all of them, which is what the Shipment card lists.
   */
  shipment?: AdminOrderShipment;

  /**
   * Every parcel of this order, forward legs first and each in the order it was recorded: an order that went out in
   * three parcels has three AWBs, three couriers and three bills, and each is corrected on its own (the card sends
   * that parcel's 'shipmentId' with the save).
   */
  shipments?: AdminOrderShipment[];

  /**
   * What each of this order's lines really carried, money-wise - the recorded split (see AdminOrderItemMoney).
   * Absent or empty for an order whose per-unit figures have not been written yet, in which case the card says so
   * rather than showing zeros.
   */
  lineMoney?: AdminOrderItemMoney[];

  /**
   * The return this order carries, or absent while it has never had one. The Return card is built from
   * it: the ask, the shop team's answer, the pickup once one is booked, and - later - the parcel being
   * back and the return being closed (see HC.Business.OrderReturnFlow).
   */
  return?: AdminOrderReturn;

  /**
   * The money of this order as the gateway took it - what was captured, what the gateway kept for taking it and
   * what the shop therefore keeps - or absent while nothing has been paid for the order (see
   * HC.Business.OrderPaymentCharges). 'What this order made' on the order screen is built from it, together with
   * the order's lines and the freight recorded on its parcel.
   */
  payment?: AdminOrderPayment;
}

/**
 * What the gateway took for one order, and what it kept for taking it - the order's newest payment row. Every
 * amount is in rupees.
 *
 * The charge figures are absent while the gateway has not reported them (a capture that arrived before Razorpay
 * worked the fee out has none), which is NOT the same as a free payment - so the card says 'not reported' rather
 * than showing 0. 'chargesSource' says where they came from: 'Payment' is the capture's own estimate, 'Recon' is
 * the figure the settlement pull found the bank was actually settled on, which is never corrected back (see
 * HC.Business.OrderPaymentCharges).
 */
export interface AdminOrderPayment {
  /** Created/Authorized/Captured/Failed/RefundRequested/Refunded/RefundFailed (HC.Business.OrderPaymentStatus). */
  status: string;
  /**
   * True when the gateway actually took this money - a capture, including one that has since been refunded - and
   * false for an attempt that was only started, authorised or refused. The server answers this (see
   * HC.Business.OrderMoney.TookMoney) so the app does not have to spell the rule out again.
   */
  moneyTaken: boolean;
  amount: number;
  feeAmount?: number | null;
  taxAmount?: number | null;
  /** What the shop keeps: amount - fee - GST. Absent while the charges are. */
  netAmount?: number | null;
  /** Card / upi / netbanking / wallet ... when the gateway reported it. */
  paymentMethod?: string | null;
  /** When the gateway took the money - the day the sale belongs to in the books. */
  gatewayChargedOn?: Date | null;
  /** 'Payment' (the capture's estimate) or 'Recon' (the settlement's authoritative figure), absent if neither. */
  chargesSource?: string | null;
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

  /**
   * This parcel's own id ('OrderShipments.ShipmentID'). An order can go out in MORE than one parcel, and a
   * correction has to say WHICH one it is correcting - sending none means 'the parcel with this AWB', or a new
   * one when there is no such parcel (see AdminSaveOrderShipmentRequest).
   */
  shipmentId: number;
  /** 'Forward' (a parcel going out) or 'Reverse' (a return's pickup). */
  direction?: string;
  isReverse?: boolean;
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

  /**
   * What this parcel carries: the order's units the shop team picked when they recorded it, one entry per SKU
   * ('2 x HC-1042'). It is what the parcel's own courier bill is charged to for the books
   * (OrderItemMoney.FreightShare), which is why the card asks for it. Empty means the shop has not said what is
   * in the parcel - not that it is empty - and such a parcel's bill is spread over the whole order instead.
   */
  items?: AdminOrderShipmentItem[];
}

/** One SKU of a parcel and how many of its units are in it ('HC-1042 x 2'). */
export interface AdminOrderShipmentItem {
  sku: string;
  quantity: number;
}

/**
 * What one order LINE really carried, money-wise, as the order screen's per-unit table reads it - the split the
 * shop records rather than guesses (OrderItemMoney). One row per SKU of the order, with the number of units that
 * SKU is: an order line is one row per physical unit, so three of a thing is three units here.
 *
 * 'not known' is null and never 0 - a gateway that has not reported its fee yet, a parcel nobody has billed. A 0
 * therefore means the share really was nothing.
 */
export interface AdminOrderItemMoney {
  sku: string;
  units: number;
  /** The output GST inside what the customer paid for those units. */
  outputGst: number;
  gatewayFee?: number | null;
  gatewayTax?: number | null;
  freightShare?: number | null;
  /** 'Payment' (the capture's own report) or 'Recon' (the settlement the bank was paid on, which is final). */
  chargesSource?: string | null;
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
  /**
   * The parcel this call is about, when the card is correcting one it has in front of it
   * ('AdminOrderShipment.shipmentId'). Null - what booking another parcel sends - means 'the parcel with this
   * AWB, or a new one when there is none', which is how an order goes out in more than one parcel.
   */
  shipmentId?: number | null;
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

  /**
   * Which of the order's units this parcel carries, one entry per SKU - what the Shipment card's picker
   * collects, and what the parcel's own bill is charged to for the books (OrderItemMoneyWriter).
   *
   * Left out to mean 'this form is not about the contents', which keeps what the parcel already holds (the
   * Shipped move does that, so dispatching an order never wipes the picker's work). An EMPTY list is a real
   * answer and a different one: 'this parcel carries none of the order's units', which the server refuses
   * for a forward parcel - a parcel going out with nothing in it is one nobody can bill for.
   */
  items?: AdminOrderShipmentItem[] | null;
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
