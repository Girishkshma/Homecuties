import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { LoginComponent } from './login/login.component';
import { ForgotPasswordComponent } from './forgot-password/forgot-password.component';
import { ResetPasswordComponent } from './reset-password/reset-password.component';
import { MainLayoutComponent } from './layout/main-layout/main-layout.component';
import { DashboardComponent } from './dashboard/dashboard.component';
import { ProductsComponent } from './products/products.component';
import { ProductFormComponent } from './product-form/product-form.component';
import { OrdersComponent } from './orders/orders.component';
import { OrderDetailComponent } from './order-detail/order-detail.component';
import { CustomersComponent } from './customers/customers.component';
import { PartnersComponent } from './partners/partners.component';
import { VendorsComponent } from './vendors/vendors.component';
import { PurchasesComponent } from './purchases/purchases.component';
import { AdminUsersComponent } from './admin-users/admin-users.component';
import { AuthGuard } from './guards/auth.guard';
import { MenuAccessGuard } from './guards/menu-access.guard';

// Every page of the admin shell requires the JWT issued at login ('AuthGuard'), and each page
// declares the section it belongs to so that 'MenuAccessGuard' can refuse the sections the admin's
// role does not grant (the API enforces the same rule).
const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'forgot-password', component: ForgotPasswordComponent },
  { path: 'reset-password/:token', component: ResetPasswordComponent },
  {
    path: '',
    component: MainLayoutComponent,
    canActivate: [AuthGuard],
    children: [
      { path: 'dashboard', component: DashboardComponent },
      { path: 'products', component: ProductsComponent, canActivate: [MenuAccessGuard], data: { section: '/products' } },
      { path: 'products/new', component: ProductFormComponent, canActivate: [MenuAccessGuard], data: { section: '/products' } },
      { path: 'products/:id', component: ProductFormComponent, canActivate: [MenuAccessGuard], data: { section: '/products' } },
      { path: 'orders', component: OrdersComponent, canActivate: [MenuAccessGuard], data: { section: '/orders' } },
      { path: 'orders/:id', component: OrderDetailComponent, canActivate: [MenuAccessGuard], data: { section: '/orders' } },
      { path: 'customers', component: CustomersComponent, canActivate: [MenuAccessGuard], data: { section: '/customers' } },
      { path: 'partners', component: PartnersComponent, canActivate: [MenuAccessGuard], data: { section: '/partners' } },
      { path: 'vendors', component: VendorsComponent, canActivate: [MenuAccessGuard], data: { section: '/vendors' } },
      { path: 'purchases', component: PurchasesComponent, canActivate: [MenuAccessGuard], data: { section: '/purchases' } },
      { path: 'users', component: AdminUsersComponent, canActivate: [MenuAccessGuard], data: { section: '/users' } },
      { path: '', redirectTo: '/dashboard', pathMatch: 'full' }
    ]
  },
  { path: '**', redirectTo: '/dashboard' }
];


@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
