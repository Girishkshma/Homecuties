import { Component, OnInit, OnDestroy, Inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { Subscription } from 'rxjs';
import { ProductService } from '../services/product.service';
import { CartService } from '../services/cart.service';
import { FavoritesService } from '../services/favorites.service';
import { AuthService, CustomerInfo } from '../services/auth.service';
import { CategoryHeading, CategoryRow, groupedByHeading } from '../models/category-tree';

@Component({
  selector: 'app-side-menu',
  templateUrl: './side-menu.component.html',
  standalone: false,
  styleUrl: './side-menu.component.scss'
})
export class SideMenuComponent implements OnInit, OnDestroy {
  /**
   * The catalogue's headings and the shelves read under each of them, in the shop's own order - one group per
   * heading, with the rows of a heading stepped in beneath it (see groupedByHeading). The catalogue is one
   * self-referencing table and a heading is not a place a product sits, so a menu of the shelves alone would say
   * nothing about which collection each belongs to.
   */
  headings: CategoryHeading[] = [];

  /** The rows no heading leads into, if the catalogue holds any (a loop in the table): listed plainly, never dropped. */
  ungrouped: CategoryRow[] = [];

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

  /**
   * The menu is the catalogue's tree, not its table: the API sends the rows the shop sells from together with the
   * headings above them (see ProductService.GetCategoriesAsync), and they are grouped here so the two parts of the
   * menu - the desktop dropdown and the phone menu - read the same rows the same way.
   */
  private loadCategories(): void {
    this.productService.getCategories().subscribe({
      next: (data) => {
        const tree = groupedByHeading(data);
        this.headings = tree.headings;
        this.ungrouped = tree.ungrouped;
      },
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
