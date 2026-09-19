import { Component, OnInit, OnDestroy, Inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { Subscription } from 'rxjs';
import { FavoritesService, WishListItem } from '../services/favorites.service';
import { AuthService } from '../services/auth.service';
import { CartService, CartItem } from '../services/cart.service';
import { UtilityService } from '../services/utility.service';
import { Router } from '@angular/router';

@Component({
  selector: 'app-favorites',
  templateUrl: './favorites.component.html',
  standalone: false,
  styleUrl: './favorites.component.scss'
})
export class FavoritesComponent implements OnInit, OnDestroy {
  wishList: WishListItem[] = [];
  loading = true;
  error = '';
  UtilityService = UtilityService;
  addedToCart: { [productId: number]: boolean } = {};
  cartQuantities: Map<number, number> = new Map();
  private isBrowser: boolean;
  private cartSubscription?: Subscription;

  constructor(
    private favoritesService: FavoritesService,
    private authService: AuthService,
    private cartService: CartService,
    private router: Router,
    @Inject(PLATFORM_ID) private platformId: Object
  ) {
    this.isBrowser = isPlatformBrowser(this.platformId);
  }

  ngOnInit(): void {
    const customer = this.authService.getCurrentCustomer();
    const isGuest = !customer;
    const customerId = customer ? customer.CustomerID : 0;

    if (!isGuest && !customerId) {
      this.router.navigate(['/login']);
      return;
    }

    this.loadWishList(customerId, isGuest);
    this.cartSubscription = this.cartService.cartItems$.subscribe((items: CartItem[]) => {
      this.cartQuantities.clear();
      items.forEach(item => {
        this.cartQuantities.set(item.productId, item.quantity);
      });
    });
  }

  ngOnDestroy(): void {
    this.cartSubscription?.unsubscribe();
  }

  private loadWishList(customerId: number, isGuest: boolean): void {
    this.loading = true;
    if (isGuest && !customerId) {
      const storedId = this.isBrowser ? localStorage.getItem('guestCustomerId') : null;
      if (storedId) {
        customerId = Number(storedId);
      } else {
        this.wishList = [];
        this.loading = false;
        return;
      }
    }
    this.favoritesService.getWishList(customerId, isGuest).subscribe({
      next: (data) => {
        this.wishList = data;
        this.loading = false;
      },
      error: (err) => {
        this.error = 'Failed to load wishlist. Please try again later.';
        this.loading = false;
        console.error('Error loading wishlist:', err);
      }
    });
  }

  removeFromWishList(productId: number): void {
    const customer = this.authService.getCurrentCustomer();
    const isGuest = !customer;
    const customerId = customer ? customer.CustomerID : 0;

    if (!isGuest && !customerId) return;

    this.favoritesService.removeFromWishList(customerId, productId, isGuest).subscribe({
      next: () => {
        this.wishList = this.wishList.filter(item => item.ProductId !== productId);
        this.favoritesService.removeFromLocalWishList(productId);
      },
      error: (err) => console.error('Error removing from wishlist:', err)
    });
  }

  addToCart(productId: number): void {
    const customer = this.authService.getCurrentCustomer();
    const isGuest = !customer;
    const customerId = customer ? customer.CustomerID : 0;

    if (!isGuest && !customerId) return;

    this.cartService.addToCart(customerId, isGuest, productId, 1).subscribe({
      next: () => {
        this.addedToCart[productId] = true;
        this.cartService.addToLocalCart({
          productId: productId,
          productName: '',
          quantity: 1,
          price: 0,
          image: ''
        });
        setTimeout(() => this.addedToCart[productId] = false, 3000);
      },
      error: (err) => console.error('Error adding to cart:', err)
    });
  }
}
