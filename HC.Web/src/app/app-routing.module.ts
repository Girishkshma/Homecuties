import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { HomeComponent } from './home/home.component';
import { ShopComponent } from './shop/shop.component';
import { ProductDetailsComponent } from './product-details/product-details.component';
import { CartComponent } from './cart/cart.component';
import { CheckoutComponent } from './checkout/checkout.component';
import { MyOrdersComponent } from './my-orders/my-orders.component';
import { LoginComponent } from './login/login.component';
import { RegisterComponent } from './register/register.component';
import { FavoritesComponent } from './favorites/favorites.component';
import { LogoutComponent } from './logout/logout.component';
import { PrivacyPolicyComponent } from './privacy-policy/privacy-policy.component';
import { TermsOfServiceComponent } from './terms-of-service/terms-of-service.component';
import { ForgotPasswordComponent } from './forgot-password/forgot-password.component';
import { ResetPasswordComponent } from './reset-password/reset-password.component';
import { SetPasswordComponent } from './set-password/set-password.component';
import { ProfileComponent } from './profile/profile.component';
import { AuthGuard } from './guards/auth.guard';

/**
 * Each route carries what its page says about itself in the head: its `title`, and in `data` the description a
 * search result or a share card draws and whether the page is to be indexed at all (see PageTitleStrategy).
 *
 * The pages behind a sign-in, and the ones that are only ever about one shopper - a cart, an order, a profile - say
 * 'noindex'. There is nothing in them a crawler could usefully list, and listing 'Your Cart' beside the shop's own
 * pages is how a shop's results fill up with pages nobody can open.
 *
 * Two titles here are only what a page says until it knows better: the shop replaces its title with the collection
 * being browsed, and a product page with the piece it loaded (see ShopComponent and ProductDetailsComponent).
 */
const routes: Routes = [
  {
    path: '',
    component: HomeComponent,
    title: 'Homecuties — Handcrafted Home Decor, Channapatna Toys & Pots',
    data: {
      description: 'Handcrafted home decor from Homecuties: Channapatna wooden toys, painted pots and bottle vases, each made by hand and delivered across India.'
    }
  },
  {
    path: 'shop',
    component: ShopComponent,
    title: 'Shop Handcrafted Home Decor',
    data: {
      description: 'Browse every piece Homecuties makes: painted pots, bottle vases, wooden toys and decor accents, priced in rupees and delivered across India.'
    }
  },
  {
    path: 'shop/:categoryId',
    component: ShopComponent,
    title: 'Shop Handcrafted Home Decor',
    data: {
      description: 'Browse every piece Homecuties makes: painted pots, bottle vases, wooden toys and decor accents, priced in rupees and delivered across India.'
    }
  },
  {
    path: 'product/:id',
    component: ProductDetailsComponent,
    title: 'Handcrafted Home Decor Piece',
    data: {
      description: 'A handcrafted piece from the Homecuties collection: photographs, the price you pay and how it reaches you.'
    }
  },
  {
    path: 'cart',
    component: CartComponent,
    title: 'Your Cart',
    data: {
      noindex: true,
      description: 'The pieces you have chosen from Homecuties.'
    }
  },
  {
    path: 'checkout',
    component: CheckoutComponent,
    canActivate: [AuthGuard],
    title: 'Checkout',
    data: {
      noindex: true,
      description: 'Confirm where your Homecuties order goes and pay for it.'
    }
  },
  {
    path: 'my-orders',
    component: MyOrdersComponent,
    canActivate: [AuthGuard],
    title: 'My Orders',
    data: {
      noindex: true,
      description: 'The orders you have placed with Homecuties.'
    }
  },
  {
    path: 'login',
    component: LoginComponent,
    title: 'Sign In',
    data: {
      noindex: true,
      description: 'Sign in to your Homecuties account.'
    }
  },
  {
    path: 'register',
    component: RegisterComponent,
    title: 'Create an Account',
    data: {
      noindex: true,
      description: 'Create a Homecuties account to check out faster and keep the pieces you like.'
    }
  },
  {
    path: 'favorites',
    component: FavoritesComponent,
    title: 'Your Favorites',
    data: {
      noindex: true,
      description: 'The pieces you have saved from the Homecuties collection.'
    }
  },
  {
    path: 'logout',
    component: LogoutComponent,
    title: 'Signing Out',
    data: {
      noindex: true,
      description: 'Signing out of your Homecuties account.'
    }
  },
  {
    path: 'privacy-policy',
    component: PrivacyPolicyComponent,
    title: 'Privacy Policy',
    data: {
      description: 'What information Homecuties collects when you shop, how it is used and kept, and the rights you have over it.'
    }
  },
  {
    path: 'terms-of-service',
    component: TermsOfServiceComponent,
    title: 'Terms of Service',
    data: {
      description: 'The terms that apply when you shop with Homecuties: your account, orders and payments, shipping and returns.'
    }
  },
  {
    path: 'forgot-password',
    component: ForgotPasswordComponent,
    title: 'Forgot Your Password',
    data: {
      noindex: true,
      description: 'Ask for a link to choose a new Homecuties password.'
    }
  },
  {
    path: 'reset-password/:token',
    component: ResetPasswordComponent,
    title: 'Reset Your Password',
    data: {
      noindex: true,
      description: 'Choose a new password for your Homecuties account.'
    }
  },
  {
    path: 'set-password',
    component: SetPasswordComponent,
    canActivate: [AuthGuard],
    title: 'Set Your Password',
    data: {
      noindex: true,
      description: 'Choose the password you will sign in to Homecuties with.'
    }
  },
  {
    path: 'profile',
    component: ProfileComponent,
    canActivate: [AuthGuard],
    title: 'My Profile',
    data: {
      noindex: true,
      description: 'Your Homecuties account details and the addresses you have saved.'
    }
  },
  { path: '**', redirectTo: '' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes, {
    scrollPositionRestoration: 'enabled',
    anchorScrolling: 'enabled'
  })],
  exports: [RouterModule]
})
export class AppRoutingModule { }
