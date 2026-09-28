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
  CanCancel: boolean;
  ItemCount: number;
  ShippingAddress: OrderAddress;
  Items: OrderItem[];
  History: OrderHistoryEntry[];
}

/**
 * What 'Order/GetOrders' answers: everything 'MyOrder' needs, except that a server which has not been
 * updated yet can leave the newer fields (StatusId, IsPaid, CanCancel, ItemCount, the address and the
 * history) out - 'normalizeMyOrder' fills them in.
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

  return {
    OrderId: order.OrderId ?? 0,
    OrderNumber: order.OrderNumber ?? '',
    OrderDate: order.OrderDate ?? '',
    TotalAmount: order.TotalAmount ?? 0,
    StatusId: statusId,
    Status: order.Status ?? '',
    PaymentStatus: order.PaymentStatus || (isPaid ? 'Paid' : statusId === ORDER_STATUS_IDS['Cancelled'] ? 'Not charged' : 'Payment pending'),
    IsPaid: isPaid,
    // Only an order that has not been paid yet can be cancelled by the customer - the server is the
    // one that actually enforces this, so a wrong guess here can only produce a clear refusal.
    CanCancel: order.CanCancel ?? statusId === ORDER_STATUS_IDS['Pending'],
    ItemCount: order.ItemCount ?? items.reduce((count, item) => count + item.Quantity, 0),
    ShippingAddress: order.ShippingAddress ?? {
      ContactName: '',
      AddressLine1: '',
      AddressLine2: '',
      City: '',
      State: '',
      Zipcode: '',
      MobileNumber: '',
      EmailId: null
    },
    Items: items,
    History: order.History ?? []
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

  /** Cancels an order that has not been paid yet; its reserved units go back on sale. */
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
}
