import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BrowserModule, provideClientHydration, withEventReplay } from '@angular/platform-browser';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { HomeComponent } from './home/home.component';
import { ShopComponent } from './shop/shop.component';
import { ProductDetailsComponent } from './product-details/product-details.component';
import { CartComponent } from './cart/cart.component';
import { CheckoutComponent } from './checkout/checkout.component';
import { LoginComponent } from './login/login.component';
import { RegisterComponent } from './register/register.component';
import { FavoritesComponent } from './favorites/favorites.component';
import { LogoutComponent } from './logout/logout.component';
import { PrivacyPolicyComponent } from './privacy-policy/privacy-policy.component';
import { TermsOfServiceComponent } from './terms-of-service/terms-of-service.component';
import { SideMenuComponent } from './side-menu/side-menu.component';
import { PageFooterComponent } from './page-footer/page-footer.component';
import { NewsletterComponent } from './newsletter/newsletter.component';
import { ProductImagePipe } from './pipes/product-image.pipe';
import { ForgotPasswordComponent } from './forgot-password/forgot-password.component';
import { ResetPasswordComponent } from './reset-password/reset-password.component';
import { customerTokenInterceptor } from './interceptors/customer-token.interceptor';

@NgModule({
  declarations: [
    AppComponent,
    HomeComponent,
    ShopComponent,
    ProductDetailsComponent,
    CartComponent,
    CheckoutComponent,
    LoginComponent,
    RegisterComponent,
    FavoritesComponent,
    LogoutComponent,
    PrivacyPolicyComponent,
    TermsOfServiceComponent,
    SideMenuComponent,
    PageFooterComponent,
    NewsletterComponent,
    ProductImagePipe,
    ForgotPasswordComponent,
    ResetPasswordComponent
  ],
  imports: [
    BrowserModule,
    AppRoutingModule,
    CommonModule,
    RouterModule,
    FormsModule
  ],
  providers: [
    provideClientHydration(withEventReplay()),
    provideHttpClient(withFetch(), withInterceptors([customerTokenInterceptor]))
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }
