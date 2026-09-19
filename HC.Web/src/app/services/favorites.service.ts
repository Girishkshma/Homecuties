import { Injectable, Inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Observable, BehaviorSubject, of } from 'rxjs';
import { tap, switchMap } from 'rxjs/operators';
import { ApiConfigService } from './api.service';

export interface WishListItem {
  CustomerId: number;
  ProductId: number;
  AddedOn: string;
  ProductName: string;
  ProductTitle: string;
  ProductDescription: string;
  PromoImage: string;
  SalesPrice: number;
  PostDiscountSalesPrice: number;
  PostAdditionalDiscountSalesPrice: number;
  DiscountPercent: number;
  AdditionalDiscountPercent: number;
  IsInStock: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class FavoritesService {
  private wishListSubject = new BehaviorSubject<WishListItem[]>([]);
  wishList$ = this.wishListSubject.asObservable();
  private wishListCountSubject = new BehaviorSubject<number>(0);
  wishListCount$ = this.wishListCountSubject.asObservable();
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
    if (this.isBrowser) {
      const storedId = localStorage.getItem('guestCustomerId');
      if (storedId) {
        return of(Number(storedId));
      }
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

  /** Resolve the effective customer ID for guest users */
  private resolveCustomerId(customerId: number, isGuest: boolean): Observable<number> {
    if (isGuest) {
      if (customerId) {
        return of(customerId);
      }
      if (this.isBrowser) {
        const storedId = localStorage.getItem('guestCustomerId');
        if (storedId) {
          return of(Number(storedId));
        }
      }
      return this.getOrCreateGuestCustomerId();
    }
    return of(customerId);
  }

  getWishList(customerId: number, isGuest: boolean = false): Observable<WishListItem[]> {
    return this.resolveCustomerId(customerId, isGuest).pipe(
      switchMap((resolvedId) => {
        return this.http.post<WishListItem[]>(this.config.getBaseServUrl() + 'WishList/GetWishList', {
          CustomerId: resolvedId,
          IsGuest: isGuest
        });
      })
    );
  }

  addToWishList(customerId: number, productId: number, isGuest: boolean = false): Observable<any> {
    return this.resolveCustomerId(customerId, isGuest).pipe(
      switchMap((resolvedId) => {
        return this.http.post(this.config.getBaseServUrl() + 'WishList/AddToWishList', {
          CustomerId: resolvedId,
          ProductId: productId,
          IsGuest: isGuest
        });
      })
    );
  }

  removeFromWishList(customerId: number, productId: number, isGuest: boolean = false): Observable<any> {
    return this.resolveCustomerId(customerId, isGuest).pipe(
      switchMap((resolvedId) => {
        return this.http.post(this.config.getBaseServUrl() + 'WishList/RemoveFromWishList', {
          CustomerId: resolvedId,
          ProductId: productId,
          IsGuest: isGuest
        });
      })
    );
  }

  isInWishList(customerId: number, productId: number, isGuest: boolean = false): Observable<boolean> {
    return this.resolveCustomerId(customerId, isGuest).pipe(
      switchMap((resolvedId) => {
        return this.http.post<boolean>(this.config.getBaseServUrl() + 'WishList/IsInWishList', {
          CustomerId: resolvedId,
          ProductId: productId,
          IsGuest: isGuest
        });
      })
    );
  }

  transferGuestWishList(guestCustomerId: number, customerId: number): Observable<any> {
    return this.http.post(this.config.getBaseServUrl() + 'WishList/TransferGuestWishList', {
      GuestCustomerId: guestCustomerId,
      CustomerId: customerId
    });
  }

  // Local wishlist management for UI state
  updateLocalWishList(items: WishListItem[]): void {
    this.wishListSubject.next(items);
    this.wishListCountSubject.next(items.length);
  }

  addToLocalWishList(item: WishListItem): void {
    const current = this.wishListSubject.value;
    const existing = current.find(i => i.ProductId === item.ProductId);
    if (!existing) {
      const updated = [...current, item];
      this.wishListSubject.next(updated);
      this.wishListCountSubject.next(updated.length);
    }
  }

  removeFromLocalWishList(productId: number): void {
    const current = this.wishListSubject.value.filter(i => i.ProductId !== productId);
    this.wishListSubject.next(current);
    this.wishListCountSubject.next(current.length);
  }

  clearLocalWishList(): void {
    this.wishListSubject.next([]);
    this.wishListCountSubject.next(0);
  }
}
