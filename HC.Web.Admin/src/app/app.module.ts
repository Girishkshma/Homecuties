import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { HTTP_INTERCEPTORS, HttpClientModule } from '@angular/common/http';
import { FormsModule } from '@angular/forms';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { LoginComponent } from './login/login.component';
import { ForgotPasswordComponent } from './forgot-password/forgot-password.component';
import { ResetPasswordComponent } from './reset-password/reset-password.component';
import { DashboardComponent } from './dashboard/dashboard.component';
import { SidebarComponent } from './layout/sidebar/sidebar.component';
import { MainLayoutComponent } from './layout/main-layout/main-layout.component';
import { ProductsComponent } from './products/products.component';
import { ProductFormComponent } from './product-form/product-form.component';
import { OrdersComponent } from './orders/orders.component';
import { OrderDetailComponent } from './order-detail/order-detail.component';
import { CustomersComponent } from './customers/customers.component';
import { PartnersComponent } from './partners/partners.component';
import { VendorsComponent } from './vendors/vendors.component';
import { PurchasesComponent } from './purchases/purchases.component';
import { AdminUsersComponent } from './admin-users/admin-users.component';
import { AdminTokenInterceptor } from './interceptors/admin-token.interceptor';

@NgModule({
  declarations: [
    AppComponent,
    LoginComponent,
    ForgotPasswordComponent,
    ResetPasswordComponent,
    DashboardComponent,
    SidebarComponent,
    MainLayoutComponent,
    ProductsComponent,
    ProductFormComponent,
    OrdersComponent,
    OrderDetailComponent,
    CustomersComponent,
    PartnersComponent,
    VendorsComponent,
    PurchasesComponent,
    AdminUsersComponent
  ],
  imports: [
    BrowserModule,
    AppRoutingModule,
    HttpClientModule,
    FormsModule
  ],
  providers: [
    // Sends the admin JWT with every API call and signs the admin out when the API answers 401.
    { provide: HTTP_INTERCEPTORS, useClass: AdminTokenInterceptor, multi: true }
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }


