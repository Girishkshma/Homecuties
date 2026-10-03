import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { ApiConfigService } from './api.service';

export interface CreateOrderRequest {
  CustomerID: number;
  IsGuest: boolean;
  ShippingAddress: string;
  City: string;
  State: string;
  ZipCode: string;
  PhoneNumber: string;
  Email: string;
  PaymentMethod: string;

  /**
   * Address book id of the shipping address picked in checkout ('My Profile' -> My Addresses). The
   * server uses that saved address as it stands and ignores the flat shipping fields below; 0 means
   * they are the address to use (and to store).
   */
  ShippingAddressId: number;
  /** Optional label for a newly typed shipping address ("Home", "Office", ...). */
  ShippingAddressTitle: string;
  /** Optional second line / landmark of a newly typed shipping address. */
  ShippingAddressLine2: string;
  /** Who receives the parcel. Optional - the customer's own name is used when left empty. */
  ShippingContactName: string;

  /** True (the default) bills the order to the delivery address. */
  BillingSameAsShipping: boolean;
  /** Address book id of the billing address (see ShippingAddressId). */
  BillingAddressId: number;
  BillingContactName: string;
  BillingAddressLine1: string;
  BillingAddressLine2: string;
  BillingCity: string;
  BillingState: string;
  BillingZipCode: string;
  BillingPhoneNumber: string;
  BillingEmail: string;
}

export interface CreateOrderResponse {
  Result: number;
  Messages: string[];
  OrderId: number;
  OrderNumber: string;
  Amount: number;
  RazorpayOrderId: string;
  RazorpayKey: string;
  RemovedItems?: string[];
  AdjustedItems?: string[];
  /**
   * Only set by the 'Pay now' retry: the earlier attempt had been paid after all, so the order was
   * confirmed instead of being given a new payment. Messages holds the good news in that case.
   */
  AlreadySettled?: boolean;
}

export interface VerifyPaymentRequest {
  OrderId: number;
  RazorpayPaymentId: string;
  RazorpayOrderId: string;
  RazorpaySignature: string;
}

/** Result of a 'manage my order' POST (cancel / re-check payment). Result === 1 means success. */
export interface OrderActionResult {
  Result: number;
  Messages: string[];
}

export interface OrderAddress {
  /** CustomerAddresses.AddressID - tells the billing address apart from the shipping one. */
  AddressId: number;
  ContactName: string;
  AddressLine1: string;
  AddressLine2: string;
  City: string;
  State: string;
  Zipcode: string;
  MobileNumber: string;
  EmailId: string | null;
}

export interface OrderHistoryEntry {
  Date: string;
  Status: string;
  Comments: string;
}

export interface OrderItem {
  ProductId: number;
  ProductName: string;
  ProductTitle: string;
  Quantity: number;
  Price: number;
  Image: string;
}

/**
 * The parcel of one order as the API sends it ('OrderShipments'): the AWB the shop booked with the
 * courier, the courier's own latest wording, and the shop's own wording derived from it.
 *
 * The fields are camelCase here even though the rest of an order is PascalCase - the API sends this
 * shape with explicit JSON names ('shipment' on the order, 'awbNumber' inside it), because the same
 * shape is used by the admin order screen (see HC.Business.Dtos.OrderShipmentDto).
 *
 * Null on an order the shop has not recorded a parcel for yet, so a card is only drawn when there is
 * something to show.
 */
export interface OrderShipment {
  /** 1 = there is an answer to show; 0 = nothing could be done (see 'messages'). */
  result: number;
  messages: string[];

  orderId: number;
  /** False while the shop has not recorded an AWB for this order yet. */
  hasShipment: boolean;
  /** The shipping provider the parcel was booked with ('Shiprocket', or 'Custom' - the shop's own service). */
  provider: string;
  /**
   * False when the parcel is carried by a delivery arrangement of the shop's own: there is no courier to
   * ask, so no tracking page is offered for it, 'Refresh parcel' is left out, and the card says where it
   * stands on the shop's own word. The AWB above is a real reference either way.
   */
  reportsTracking: boolean;
  courierName: string;
  awbNumber: string;
  /** The courier's tracking page, when it reported one - else '' and no link is offered. */
  trackingUrl: string;
  /** The courier's own wording, shown when the shop has none of its own for it. */
  providerStatus: string;
  /** The provider's own status code, for support only (never used to decide anything). */
  providerStatusCode: number | null;
  /** The shop's own words for where the parcel is ('' when it has none for the courier's wording). */
  status: string;
  /** The stage behind 'status', as a name: Booked, InTransit, OutForDelivery, Delivered, ... */
  stage: string;
  delivered: boolean;
  /** True once the courier has nothing more to say (delivered, coming back, or called off). */
  closed: boolean;
  deliveredOn: string | null;
  /** The latest line of the courier's tracking trail. */
  lastStatusText: string;
  /** When the courier was last asked about this parcel (null = never). */
  lastCheckedOn: string | null;
  /** The order's status after the pull, when the caller is an order screen. */
  orderStatusId: number | null;
  orderStatus: string | null;
}

/**
 * What 'Order/RefreshShipments' answers: the fresh snapshot of every parcel that was actually looked
 * up. Only the stale ones are in 'shipments' - a parcel checked a moment ago, or one the courier is
 * done with, is left alone, which is what keeps opening 'My Orders' from being a courier call.
 */
export interface RefreshOrderShipments {
  result: number;
  /**
   * What the pull has to say. A courier that could not be reached is still result 1: its sentence is
   * here and the last known status stays on the page, because the customer cannot act on it either way.
   */
  messages: string[];
  shipments: OrderShipment[];
}

/** One order as shown on the 'My Orders' page. */
export interface MyOrder {
  OrderId: number;
  OrderNumber: string;
  OrderDate: string;
  TotalAmount: number;
  /** StatusId: 1 = Pending, 2 = Confirmed, 3 = Shipped, 4 = Delivered, 5 = Cancelled. */
  StatusId: number;
  Status: string;
  PaymentStatus: string;
  IsPaid: boolean;
  /**
   * True while the order's money is owed back to the customer: a paid order that was cancelled. The
   * cancellation only asked for the refund - the shop team approves it, so 'My Orders' shows this as
   * 'Refund pending' until the money is on its way back.
   */
  RefundPending: boolean;
  CanCancel: boolean;
  /** True while the order still owes money, so 'Pay now' can be offered on it. */
  CanPay: boolean;
  ItemCount: number;
  ShippingAddress: OrderAddress;
  /**
   * Where the order is billed. It is the delivery address unless a different one was chosen in
   * checkout, so 'My Orders' only shows it when the two differ.
   */
  BillingAddress: OrderAddress;
  Items: OrderItem[];
  History: OrderHistoryEntry[];
  /**
   * The parcel the shop booked with the courier, or null while the order has none. It rides along with
   * the order, so 'My Orders' can show where the parcel is without asking the courier first - the fresh
   * wording is pulled separately ('Order/RefreshShipments').
   */
  Shipment: OrderShipment | null;
}

/**
 * What 'Order/GetOrders' answers: everything 'MyOrder' needs, except that a server which has not been
 * updated yet can leave the newer fields (StatusId, IsPaid, CanCancel, CanPay, ItemCount, the address
 * and the history) out - 'normalizeMyOrder' fills them in.
 */
export type RawMyOrder = Partial<MyOrder>;

/** Orders.OrderStatusID as seeded by 'HC.Data/Scripts/SeedOrderStatuses.sql'. */
const ORDER_STATUS_IDS: { [status: string]: number } = {
  Pending: 1,
  Confirmed: 2,
  Shipped: 3,
  Delivered: 4,
  Cancelled: 5
};

/**
 * Fills in the fields an older server build may not send yet (StatusId, IsPaid, CanCancel, ItemCount,
 * the address and the history). The current API sends all of them, but without this a half-updated
 * server leaves the customer with an order that shows no items and offers no action at all - the
 * status name and the item rows are enough to work the rest out.
 */
export function normalizeMyOrder(raw: RawMyOrder | null | undefined): MyOrder {
  const order = raw ?? {};

  const items = (order.Items ?? []).map(item => ({
    ProductId: item.ProductId ?? 0,
    ProductName: item.ProductName ?? '',
    ProductTitle: item.ProductTitle ?? '',
    Quantity: item.Quantity ?? 0,
    Price: item.Price ?? 0,
    Image: item.Image ?? ''
  }));

  const statusId = order.StatusId ?? ORDER_STATUS_IDS[order.Status ?? ''] ?? 0;
  const isPaid = order.IsPaid ?? (statusId >= ORDER_STATUS_IDS['Confirmed'] && statusId <= ORDER_STATUS_IDS['Delivered']);
  // A server build without the field still says 'Refund pending' in the payment state it sends.
  const paymentStatus = order.PaymentStatus ?? '';
  const refundPending = order.RefundPending ?? paymentStatus.toLowerCase() === 'refund pending';

  return {
    OrderId: order.OrderId ?? 0,
    OrderNumber: order.OrderNumber ?? '',
    OrderDate: order.OrderDate ?? '',
    TotalAmount: order.TotalAmount ?? 0,
    StatusId: statusId,
    Status: order.Status ?? '',
    PaymentStatus: paymentStatus || (isPaid ? 'Paid' : statusId === ORDER_STATUS_IDS['Cancelled'] ? 'Not charged' : 'Payment pending'),
    IsPaid: isPaid,
    RefundPending: refundPending,
    // The customer may cancel while the order is still with the shop: Pending (nothing has been paid)
    // or Confirmed (paid, and the money is refunded). The server is the one that enforces the status
    // chain, so a wrong guess here can only produce a clear refusal.
    CanCancel: order.CanCancel ?? (statusId === ORDER_STATUS_IDS['Pending'] || statusId === ORDER_STATUS_IDS['Confirmed']),
    // Paying again is only offered while the order is still waiting for its money. The server has the
    // last word (it checks the earlier attempt at Razorpay first), so a wrong guess here can only
    // produce a clear refusal - never a second charge.
    CanPay: order.CanPay ?? (statusId === ORDER_STATUS_IDS['Pending'] && !isPaid),
    ItemCount: order.ItemCount ?? items.reduce((count, item) => count + item.Quantity, 0),
    ShippingAddress: order.ShippingAddress ?? emptyOrderAddress(),
    BillingAddress: order.BillingAddress ?? emptyOrderAddress(),
    Items: items,
    History: order.History ?? [],
    Shipment: normalizeOrderShipment(order.Shipment)
  };
}

/**
 * The parcel as 'My Orders' uses it, or null when this order has none.
 *
 * Null and 'recorded but blank' are the same thing to the page: an order with no AWB has no parcel to
 * show, so the strip is simply not drawn. The other fields are filled in so a server that is a step
 * behind (or a parcel only just recorded) cannot leave the strip with holes in it.
 */
export function normalizeOrderShipment(raw: Partial<OrderShipment> | null | undefined): OrderShipment | null {
  if (!raw || !raw.hasShipment) {
    return null;
  }

  return {
    result: raw.result ?? 1,
    messages: raw.messages ?? [],
    orderId: raw.orderId ?? 0,
    hasShipment: true,
    provider: raw.provider ?? '',
    // A parcel with no courier behind it is the exception, so 'no answer from the server' counts as one
    // that has one: the strip then offers 'Refresh parcel' as it always did, and the lookup answers for
    // itself (see OrderShipment.reportsTracking).
    reportsTracking: raw.reportsTracking ?? true,
    courierName: raw.courierName ?? '',
    awbNumber: raw.awbNumber ?? '',
    trackingUrl: raw.trackingUrl ?? '',
    providerStatus: raw.providerStatus ?? '',
    providerStatusCode: raw.providerStatusCode ?? null,
    status: raw.status ?? '',
    // The courier's stage, which is what the strip colours itself by - 'Unknown' until it reports.
    stage: raw.stage ?? 'Unknown',
    delivered: raw.delivered ?? false,
    closed: raw.closed ?? false,
    deliveredOn: raw.deliveredOn ?? null,
    lastStatusText: raw.lastStatusText ?? '',
    lastCheckedOn: raw.lastCheckedOn ?? null,
    orderStatusId: raw.orderStatusId ?? null,
    orderStatus: raw.orderStatus ?? null
  };
}

/** An order the server has no address for (an older build, or an order stored without one). */
function emptyOrderAddress(): OrderAddress {
  return {
    AddressId: 0,
    ContactName: '',
    AddressLine1: '',
    AddressLine2: '',
    City: '',
    State: '',
    Zipcode: '',
    MobileNumber: '',
    EmailId: null
  };
}

@Injectable({
  providedIn: 'root'
})
export class PaymentService {
  constructor(
    private http: HttpClient,
    private config: ApiConfigService
  ) {}

  createOrder(request: CreateOrderRequest): Observable<CreateOrderResponse> {
    return this.http.post<CreateOrderResponse>(
      this.config.getBaseServUrl() + 'Order/CreateOrder',
      request
    );
  }

  verifyPayment(request: VerifyPaymentRequest): Observable<any> {
    return this.http.post(
      this.config.getBaseServUrl() + 'Order/VerifyPayment',
      request
    );
  }

  getOrders(customerId: number, isGuest: boolean): Observable<any> {
    return this.http.post(
      this.config.getBaseServUrl() + 'Order/GetOrders',
      { CustomerID: customerId, IsGuest: isGuest }
    );
  }

  /**
   * The signed-in customer's orders, newest first. The customer is taken from the token on the
   * server, so nothing identifying is sent from here.
   */
  getMyOrders(): Observable<MyOrder[]> {
    return this.http
      .post<RawMyOrder[]>(this.config.getBaseServUrl() + 'Order/GetOrders', {})
      .pipe(map(rows => (rows ?? []).map(normalizeMyOrder)));
  }

  /**
   * Asks the backend what the courier now says about this customer's live parcels, and answers the fresh
   * snapshot of each. The customer comes from the token, so an empty body is the whole request.
   *
   * It is what 'My Orders' calls when it opens and when 'Refresh parcel' is tapped. The backend throttles
   * it (a parcel asked about a moment ago, or one the courier is finished with, is left alone), so
   * calling it freely is safe - but it must never break the page: the answer always comes back even when
   * a courier is down, with the reason in 'messages' and the last known status left in place.
   */
  refreshShipments(): Observable<RefreshOrderShipments> {
    return this.http.post<RefreshOrderShipments>(
      this.config.getBaseServUrl() + 'Order/RefreshShipments',
      {}
    );
  }

  /**
   * Cancels an order that is still with the shop (Pending or Confirmed). A paid one asks for its money
   * back - the pending refund is what 'Refund pending' in 'My Orders' shows - and the shop team approves
   * it; the customer is told to expect the refund rather than the refund itself.
   */
  cancelOrder(orderId: number): Observable<OrderActionResult> {
    return this.http.post<OrderActionResult>(
      this.config.getBaseServUrl() + 'Order/CancelOrder',
      { OrderId: orderId }
    );
  }

  /**
   * Asks the backend to re-check the order's payment at Razorpay and confirm it when the money was
   * captured. Used when Razorpay Checkout never reported back to the browser.
   */
  syncPayment(orderId: number): Observable<OrderActionResult> {
    return this.http.post<OrderActionResult>(
      this.config.getBaseServUrl() + 'Order/SyncPayment',
      { OrderId: orderId }
    );
  }

  /**
   * Starts a fresh payment attempt for an order that is still waiting for its money ('Pay now' in
   * 'My Orders'). The backend settles any earlier attempt at Razorpay before it creates this one, so a
   * retry can never take the money twice, and the amount always comes from the order itself - nothing
   * about the payment is decided here.
   */
  retryPayment(orderId: number): Observable<CreateOrderResponse> {
    return this.http.post<CreateOrderResponse>(
      this.config.getBaseServUrl() + 'Order/RetryPayment',
      { OrderId: orderId }
    );
  }
}
