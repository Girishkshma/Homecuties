import { Injectable, Inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Observable, BehaviorSubject, of } from 'rxjs';
import { tap, switchMap } from 'rxjs/operators';
import { ApiConfigService } from './api.service';

export interface CartItem {
  productId: number;
  productName: string;
  quantity: number;
  price: number;
  image: string;
}

@Injectable({
  providedIn: 'root'
})
export class CartService {
  private cartItemsSubject = new BehaviorSubject<CartItem[]>([]);
  cartItems$ = this.cartItemsSubject.asObservable();
  private isBrowser: boolean;

  constructor(
    private http: HttpClient,
    private config: ApiConfigService,
    @Inject(PLATFORM_ID) private platformId: Object
  ) {
    this.isBrowser = isPlatformBrowser(this.platformId);
  }

  /** Get or create a guest customer ID that persists in localStorage */
  private getOrCreateGuestCustomerId(): Observable<number> {
    const stored = this.getStoredGuestCustomerId();
    if (stored) {
      return of(stored);
    }
    // Create a new guest customer
    return this.http.post<any>(this.config.getBaseServUrl() + 'Customer/CreatetGuestCustomer', {}).pipe(
      tap((response) => {
        const id = response.CustomerID;
        if (this.isBrowser && id) {
          localStorage.setItem('guestCustomerId', String(id));
        }
      }),
      switchMap((response) => of(response.CustomerID))
    );
  }

  /**
   * The guest customer id created when the guest first added something to the cart
   * (stored in localStorage). Returns 0 when there is none - e.g. on the server.
   * Every guest API call must use this id, otherwise the cart is looked up under a
   * different (empty) customer.
   */
  getStoredGuestCustomerId(): number {
    if (!this.isBrowser) {
      return 0;
    }
    const storedId = localStorage.getItem('guestCustomerId');
    return storedId ? Number(storedId) : 0;
  }

  addToCart(customerId: number, isGuest: boolean, productId: number, quantity: number): Observable<any> {
    // If guest with no customerId, get/create a guest customer ID first
    if (isGuest && !customerId) {
      return this.getOrCreateGuestCustomerId().pipe(
        switchMap((guestId) => {
          return this.http.post(this.config.getBaseServUrl() + 'Cart/AddToCart', {
            CustomerID: guestId,
            IsGuest: true,
            ProductID: productId,
            Quantity: quantity
          });
        })
      );
    }
    return this.http.post(this.config.getBaseServUrl() + 'Cart/AddToCart', {
      CustomerID: customerId,
      IsGuest: isGuest,
      ProductID: productId,
      Quantity: quantity
    });
  }

  getCart(customerId: number, isGuest: boolean): Observable<any> {
    // If guest with no customerId, try to get from localStorage
    if (isGuest && !customerId && this.isBrowser) {
      const storedId = localStorage.getItem('guestCustomerId');
      if (storedId) {
        customerId = Number(storedId);
      }
    }
    return this.http.post(this.config.getBaseServUrl() + 'Cart/GetCart?rnd=' + Math.random(), {
      CustomerID: customerId,
      IsGuest: isGuest
    });
  }

  getCartItemsCount(customerId: number, isGuest: boolean): Observable<any> {
    if (isGuest && !customerId && this.isBrowser) {
      const storedId = localStorage.getItem('guestCustomerId');
      if (storedId) {
        customerId = Number(storedId);
      }
    }
    return this.http.post(this.config.getBaseServUrl() + 'cart/GetItemsCount', {
      CustomerID: customerId,
      IsGuest: isGuest
    });
  }

  transferGuestCart(guestCustomerId: number, customerId: number): Observable<any> {
    return this.http.post(this.config.getBaseServUrl() + 'Cart/TransferGuestCart', {
      GuestCustomerID: guestCustomerId,
      CustomerID: customerId
    });
  }

  updateCartItemQuantity(customerId: number, isGuest: boolean, productId: number, quantity: number): Observable<any> {
    if (isGuest && !customerId && this.isBrowser) {
      const storedId = localStorage.getItem('guestCustomerId');
      if (storedId) {
        customerId = Number(storedId);
      }
    }
    return this.http.post(this.config.getBaseServUrl() + 'Cart/UpdateQuantity', {
      CustomerID: customerId,
      IsGuest: isGuest,
      ProductID: productId,
      Quantity: quantity
    });
  }

  removeFromCart(customerId: number, isGuest: boolean, productId: number): Observable<any> {
    if (isGuest && !customerId && this.isBrowser) {
      const storedId = localStorage.getItem('guestCustomerId');
      if (storedId) {
        customerId = Number(storedId);
      }
    }
    return this.http.post(this.config.getBaseServUrl() + 'Cart/RemoveItem', {
      CustomerID: customerId,
      IsGuest: isGuest,
      ProductID: productId
    });
  }

  /** Clear the stored guest customer ID (e.g., after login/transfer) */
  clearGuestCustomerId(): void {
    if (this.isBrowser) {
      localStorage.removeItem('guestCustomerId');
    }
  }

  // Local cart management for UI state
  updateLocalCart(items: CartItem[]): void {
    this.cartItemsSubject.next(items);
  }

  addToLocalCart(item: CartItem): void {
    const current = this.cartItemsSubject.value;
    const existing = current.find(i => i.productId === item.productId);
    if (existing) {
      existing.quantity += item.quantity;
    } else {
      current.push(item);
    }
    this.cartItemsSubject.next([...current]);
  }

  removeFromLocalCart(productId: number): void {
    const current = this.cartItemsSubject.value.filter(i => i.productId !== productId);
    this.cartItemsSubject.next(current);
  }

  clearLocalCart(): void {
    this.cartItemsSubject.next([]);
  }
}
