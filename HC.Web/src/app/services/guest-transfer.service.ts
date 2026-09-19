import { Injectable } from '@angular/core';
import { forkJoin, Observable, of } from 'rxjs';
import { catchError, map, switchMap, tap } from 'rxjs/operators';
import { CartService } from './cart.service';
import { FavoritesService } from './favorites.service';

/**
 * Moves everything a guest collected while browsing (cart + wishlist) onto their account
 * right after they sign in or register, then refreshes the local badge state.
 *
 * The guest cart / wishlist are keyed by the guest customer id kept in localStorage, so the
 * transfers must happen before that id is cleared.
 */
@Injectable({ providedIn: 'root' })
export class GuestTransferService {
  constructor(
    private cartService: CartService,
    private favoritesService: FavoritesService
  ) {}

  /**
   * Transfers the guest cart and guest wishlist to the signed-in customer and refreshes the
   * local cart/wishlist state. Completes even when one of the transfers fails, so callers can
   * always navigate on afterwards.
   */
  transferGuestDataTo(customerId: number): Observable<void> {
    const guestId = this.cartService.getStoredGuestCustomerId();

    const cartTransfer$ = guestId
      ? this.cartService.transferGuestCart(guestId, customerId).pipe(catchError(() => of(null)))
      : of(null);

    const wishListTransfer$ = guestId
      ? this.favoritesService.transferGuestWishList(guestId, customerId).pipe(catchError(() => of(null)))
      : of(null);

    return forkJoin([cartTransfer$, wishListTransfer$]).pipe(
      tap(() => {
        if (guestId) {
          this.cartService.clearGuestCustomerId();
        }
      }),
      switchMap(() => forkJoin([this.refreshLocalCart(customerId), this.refreshLocalWishList(customerId)])),
      map(() => undefined)
    );
  }

  private refreshLocalCart(customerId: number): Observable<void> {
    return this.cartService.getCart(customerId, false).pipe(
      map((data) => {
        const items = (data?.Items || []).map((item: any) => ({
          productId: item.ProductID,
          productName: item.ProductName,
          quantity: item.Quantity,
          price: item.Price,
          image: item.PromoImage
        }));
        this.cartService.updateLocalCart(items);
      }),
      catchError(() => {
        this.cartService.updateLocalCart([]);
        return of(undefined);
      })
    );
  }

  private refreshLocalWishList(customerId: number): Observable<void> {
    return this.favoritesService.getWishList(customerId, false).pipe(
      map((items) => this.favoritesService.updateLocalWishList(items)),
      catchError(() => {
        this.favoritesService.clearLocalWishList();
        return of(undefined);
      })
    );
  }
}
