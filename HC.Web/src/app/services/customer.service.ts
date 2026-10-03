import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResult, Customer, CustomerAddress } from '../models/product.model';
import { ApiConfigService } from './api.service';

/** 'Customer/SaveAddress' answer: Result === 1 means the address is in the book. */
export interface CustomerAddressResult extends ApiResult {
  Address: CustomerAddress | null;
}

@Injectable({
  providedIn: 'root'
})
export class CustomerService {
  constructor(private http: HttpClient, private config: ApiConfigService) { }

  getCustomer(customerId: number): Observable<any> {
    return this.http.post(this.config.getBaseServUrl() + 'Customer/GetCustomer', {
      CustomerID: customerId
    });
  }

  getCustomerJWT(customer: Customer, ipAddress: string): Observable<any> {
    return this.http.post(this.config.getBaseServUrl() + 'Customer/GetCustomerJWT', {
      Customer: customer,
      IPAddress: ipAddress
    });
  }

  validateCustomerJWT(jwt: string, ipAddress: string): Observable<any> {
    return this.http.post(this.config.getBaseServUrl() + 'Customer/ValidateCustomerJWT', {
      JWT: jwt,
      IPAddress: ipAddress
    });
  }

  createGuestCustomer(): Observable<any> {
    return this.http.post(this.config.getBaseServUrl() + 'Customer/CreatetGuestCustomer', {});
  }

  createCustomer(firstName: string, lastName: string, email: string, password: string): Observable<any> {
    return this.http.post(this.config.getBaseServUrl() + 'Customer/CreateCustomer', {
      FirstName: firstName,
      LastName: lastName,
      Email: email,
      Password: password
    });
  }

  forgotPassword(email: string): Observable<any> {
    return this.http.post(this.config.getBaseServUrl() + 'Customer/ForgotPassword', {
      Email: email
    });
  }

  resetPassword(token: string, newPassword: string): Observable<any> {
    return this.http.post(this.config.getBaseServUrl() + 'Customer/ResetPassword', {
      Token: token,
      NewPassword: newPassword
    });
  }

  /**
   * Sets the internal password of the signed-in customer. The customer is identified by the
   * Authorization token, which customerTokenInterceptor attaches automatically.
   * CurrentPassword is only needed when the account already has a password.
   */
  setPassword(newPassword: string, currentPassword?: string): Observable<any> {
    return this.http.post(this.config.getBaseServUrl() + 'Customer/SetPassword', {
      CurrentPassword: currentPassword || '',
      NewPassword: newPassword
    });
  }

  /**
   * The signed-in customer's address book, newest first. The customer is taken from the token on the
   * server, so nothing identifying is sent from here and one customer can never see another's.
   */
  getAddresses(): Observable<CustomerAddress[]> {
    return this.http.post<CustomerAddress[]>(
      this.config.getBaseServUrl() + 'Customer/GetAddresses',
      {}
    );
  }

  /**
   * Adds an address to the address book (AddressId 0) or updates one that is already in it. The
   * saved address is what the customer can then pick as the shipping or the billing address in
   * checkout.
   */
  saveAddress(address: CustomerAddress): Observable<CustomerAddressResult> {
    return this.http.post<CustomerAddressResult>(
      this.config.getBaseServUrl() + 'Customer/SaveAddress',
      {
        AddressId: address.AddressId || 0,
        AddressTitle: address.AddressTitle || '',
        ContactName: address.ContactName || '',
        AddressLine1: address.AddressLine1 || '',
        AddressLine2: address.AddressLine2 || '',
        City: address.City || '',
        State: address.State || '',
        Country: address.Country || '',
        Zipcode: address.Zipcode || '',
        MobileNumber: address.MobileNumber || '',
        EmailId: address.EmailId || ''
      }
    );
  }

  /**
   * Removes an address from the book. The server refuses this for an address an order was placed
   * with - the order would lose the address it was shipped to.
   */
  deleteAddress(addressId: number): Observable<ApiResult> {
    return this.http.post<ApiResult>(
      this.config.getBaseServUrl() + 'Customer/DeleteAddress',
      { AddressId: addressId }
    );
  }
}
