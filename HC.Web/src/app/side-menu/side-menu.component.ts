import { Component, OnInit, OnDestroy, Inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { Subscription } from 'rxjs';
import { ProductService } from '../services/product.service';
import { CartService } from '../services/cart.service';
import { FavoritesService } from '../services/favorites.service';
import { AuthService, CustomerInfo } from '../services/auth.service';
import { Category } from '../models/product.model';

@Component({
  selector: 'app-side-menu',
  templateUrl: './side-menu.component.html',
  standalone: false,
  styleUrl: './side-menu.component.scss'
})
export class SideMenuComponent implements OnInit, OnDestroy {
  categories: Category[] = [];
  isMenuOpen = false;
  cartItemCount = 0;
  favoritesCount = 0;
  currentCustomer: CustomerInfo | null = null;
  private isBrowser: boolean;
  private authSubscription?: Subscription;

  constructor(
    private productService: ProductService,
    private cartService: CartService,
    private favoritesService: FavoritesService,
    private authService: AuthService,
    @Inject(PLATFORM_ID) private platformId: Object
  ) {
    this.isBrowser = isPlatformBrowser(this.platformId);
  }

  ngOnInit(): void {
    this.loadCategories();
    // Subscribe to local cart state changes (updated by cart component)
    this.cartService.cartItems$.subscribe(items => {
      this.cartItemCount = items.reduce((sum, item) => sum + item.quantity, 0);
    });
    // Subscribe to local wishlist count changes (updated by favorites component)
    this.favoritesService.wishListCount$.subscribe(count => {
      this.favoritesCount = count;
    });
    // Also load cart count from backend on init
    this.loadCartCount();
    this.authSubscription = this.authService.currentCustomer$.subscribe(customer => {
      this.currentCustomer = customer;
      this.loadCartCount();
      this.loadFavoritesCount();
    });
  }

  ngOnDestroy(): void {
    this.authSubscription?.unsubscribe();
  }

  toggleMenu(): void {
    this.isMenuOpen = !this.isMenuOpen;
  }

  closeMenu(): void {
    this.isMenuOpen = false;
  }

  logout(): void {
    this.authService.clearSession();
    this.closeMenu();
  }

  private loadCategories(): void {
    this.productService.getCategories().subscribe({
      next: (data) => this.categories = data,
      error: () => console.error('Failed to load categories')
    });
  }

  private loadCartCount(): void {
    const customer = this.authService.getCurrentCustomer();
    const customerId = customer ? customer.CustomerID : 0;
    const isGuest = !customer;
    this.cartService.getCartItemsCount(customerId, isGuest).subscribe({
      next: (data) => {
        if (data && data.Count !== undefined) {
          this.cartItemCount = data.Count;
        }
      },
      error: () => {}
    });
  }

  private loadFavoritesCount(): void {
    const customer = this.authService.getCurrentCustomer();
    const customerId = customer ? customer.CustomerID : 0;
    const isGuest = !customer;
    if (isGuest && !customerId) {
      const storedId = this.isBrowser ? localStorage.getItem('guestCustomerId') : null;
      if (storedId) {
        this.favoritesService.getWishList(Number(storedId), true).subscribe({
          next: (items) => this.favoritesCount = items.length,
          error: () => this.favoritesCount = 0
        });
      } else {
        this.favoritesCount = 0;
      }
      return;
    }
    this.favoritesService.getWishList(customerId, isGuest).subscribe({
      next: (items) => this.favoritesCount = items.length,
      error: () => this.favoritesCount = 0
    });
  }
}
