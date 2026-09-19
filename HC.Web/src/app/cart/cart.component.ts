import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { CartService } from '../services/cart.service';
import { AuthService } from '../services/auth.service';
import { FavoritesService } from '../services/favorites.service';
import { UtilityService } from '../services/utility.service';

@Component({
  selector: 'app-cart',
  templateUrl: './cart.component.html',
  standalone: false,
  styleUrl: './cart.component.scss'
})
export class CartComponent implements OnInit {
  cartItems: any[] = [];
  loading = true;
  error = '';
  updatingItems = new Set<number>();
  itemToRemove: any = null;
  toastMessage = '';
  toastType: 'success' | 'error' = 'success';
  favoriteProductIds = new Set<number>();
  UtilityService = UtilityService;

  constructor(
    private cartService: CartService,
    private authService: AuthService,
    private favoritesService: FavoritesService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadCart();
  }

  getSubtotal(): number {
    return this.cartItems.reduce((sum, item) => sum + (item.Price * item.Quantity), 0);
  }

  /**
   * True when any cart line cannot be fulfilled: the product is out of stock, or the
   * requested quantity is more than the quantity currently available.
   */
  get hasStockIssue(): boolean {
    return this.cartItems.some(item => !item.IsInStock || item.Quantity > item.AvailableQty);
  }

  /** True when at least one line requests more units than are available. */
  get hasQuantityMismatch(): boolean {
    return this.cartItems.some(item => item.IsInStock && item.AvailableQty > 0 && item.Quantity > item.AvailableQty);
  }

  /** Out-of-stock lines (these must be removed or moved to favorites). */
  get hasOutOfStockItem(): boolean {
    return this.cartItems.some(item => !item.IsInStock);
  }

  /** The '+' button is capped at the stock available for that product. */
  canIncreaseQuantity(item: any): boolean {
    return item.AvailableQty <= 0 || item.Quantity < item.AvailableQty;
  }

  /** True when nobody is signed in - the guest will be asked to sign in before checkout. */
  get isGuestUser(): boolean {
    return !this.authService.getCurrentCustomer();
  }

  /** Checkout is blocked until every line matches the available stock. */
  proceedToCheckout(): void {
    if (this.cartItems.length === 0 || this.hasStockIssue) {
      return;
    }
    this.router.navigate(['/checkout']);
  }

  increaseQuantity(item: any): void {
    const newQty = item.Quantity + 1;
    this.updateQuantity(item, newQty);
  }

  decreaseQuantity(item: any): void {
    if (item.Quantity <= 1) {
      this.promptRemoveItem(item);
      return;
    }
    const newQty = item.Quantity - 1;
    this.updateQuantity(item, newQty);
  }

  private getCustomerInfo(): { customerId: number; isGuest: boolean } {
    const customer = this.authService.getCurrentCustomer();
    if (customer) {
      return { customerId: customer.CustomerID, isGuest: false };
    }
    return { customerId: 0, isGuest: true };
  }

  private updateQuantity(item: any, newQuantity: number): void {
    this.updatingItems.add(item.ProductID);
    const { customerId, isGuest } = this.getCustomerInfo();
    this.cartService.updateCartItemQuantity(customerId, isGuest, item.ProductID, newQuantity).subscribe({
      next: () => {
        item.Quantity = newQuantity;
        this.updatingItems.delete(item.ProductID);
        this.syncLocalCart();
      },
      error: (err) => {
        this.updatingItems.delete(item.ProductID);
        this.showToast('Failed to update quantity. Please try again.', 'error');
        console.error('Failed to update quantity', err);
      }
    });
  }

  promptRemoveItem(item: any): void {
    this.itemToRemove = item;
  }

  cancelRemove(): void {
    this.itemToRemove = null;
  }

  confirmRemove(): void {
    if (!this.itemToRemove) return;
    const item = this.itemToRemove;
    this.itemToRemove = null;
    this.updatingItems.add(item.ProductID);
    const { customerId, isGuest } = this.getCustomerInfo();
    this.cartService.removeFromCart(customerId, isGuest, item.ProductID).subscribe({
      next: () => {
        this.cartItems = this.cartItems.filter(i => i.ProductID !== item.ProductID);
        this.updatingItems.delete(item.ProductID);
        this.syncLocalCart();
        this.showToast('Item removed from cart.', 'success');
      },
      error: (err) => {
        this.updatingItems.delete(item.ProductID);
        this.showToast('Failed to remove item. Please try again.', 'error');
        console.error('Failed to remove item', err);
      }
    });
  }

  private showToast(message: string, type: 'success' | 'error'): void {
    this.toastMessage = message;
    this.toastType = type;
    setTimeout(() => {
      this.toastMessage = '';
    }, 3000);
  }

  private loadCart(): void {
    const { customerId, isGuest } = this.getCustomerInfo();
    this.cartService.getCart(customerId, isGuest).subscribe({
      next: (data) => {
        this.cartItems = data.Items || [];
        this.loading = false;
        this.syncLocalCart();
        this.loadFavorites();
      },
      error: (err) => {
        this.error = 'Failed to load cart.';
        this.loading = false;
        console.error(err);
      }
    });
  }

  /** Load favorites to know which items are already favorited (works for both guest and logged-in users) */
  private loadFavorites(): void {
    const { customerId, isGuest } = this.getCustomerInfo();
    this.favoritesService.getWishList(customerId, isGuest).subscribe({
      next: (items) => {
        this.favoriteProductIds = new Set(items.map(i => i.ProductId));
      },
      error: (err) => {
        console.error('Failed to load favorites', err);
      }
    });
  }

  /** Move an out-of-stock item from cart to favorites (works for both guest and logged-in users) */
  moveToFavorites(item: any): void {
    this.updatingItems.add(item.ProductID);
    const { customerId, isGuest } = this.getCustomerInfo();
    this.favoritesService.addToWishList(customerId, item.ProductID, isGuest).subscribe({
      next: () => {
        this.favoriteProductIds.add(item.ProductID);
        this.favoritesService.addToLocalWishList({
          CustomerId: customerId,
          ProductId: item.ProductID,
          AddedOn: new Date().toISOString(),
          ProductName: item.ProductName || '',
          ProductTitle: item.ProductTitle || '',
          ProductDescription: item.ProductDescription || '',
          PromoImage: item.PromoImage || '',
          SalesPrice: item.Price || 0,
          PostDiscountSalesPrice: item.Price || 0,
          PostAdditionalDiscountSalesPrice: item.Price || 0,
          DiscountPercent: 0,
          AdditionalDiscountPercent: 0,
          IsInStock: true
        });
        // Remove from cart after adding to favorites
        this.cartService.removeFromCart(customerId, isGuest, item.ProductID).subscribe({
          next: () => {
            this.cartItems = this.cartItems.filter(i => i.ProductID !== item.ProductID);
            this.updatingItems.delete(item.ProductID);
            this.syncLocalCart();
            this.showToast('Moved to favorites!', 'success');
          },
          error: (err) => {
            this.updatingItems.delete(item.ProductID);
            this.showToast('Item added to favorites, but could not remove from cart.', 'success');
            console.error('Failed to remove from cart after adding to favorites', err);
          }
        });
      },
      error: (err) => {
        this.updatingItems.delete(item.ProductID);
        this.showToast('Failed to add to favorites. Please try again.', 'error');
        console.error('Failed to add to favorites', err);
      }
    });
  }

  /** Sync cart items to local state so the header badge updates */
  private syncLocalCart(): void {
    const localItems = this.cartItems.map((item: any) => ({
      productId: item.ProductID,
      productName: item.ProductName,
      quantity: item.Quantity,
      price: item.Price,
      image: item.PromoImage
    }));
    this.cartService.updateLocalCart(localItems);
  }
}
