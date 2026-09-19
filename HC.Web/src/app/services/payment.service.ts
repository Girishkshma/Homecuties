import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
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
}
