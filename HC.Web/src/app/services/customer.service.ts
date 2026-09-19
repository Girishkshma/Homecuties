import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Customer } from '../models/product.model';
import { ApiConfigService } from './api.service';

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
}
