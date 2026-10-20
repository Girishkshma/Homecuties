import { Component, OnInit, OnDestroy, Inject, PLATFORM_ID, HostListener, ViewChild, ElementRef } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { Router } from '@angular/router';
import {
  Subject, Subscription, catchError, debounceTime, map, of, switchMap, takeUntil
} from 'rxjs';
import { ProductService } from '../services/product.service';
import { CartService } from '../services/cart.service';
import { FavoritesService } from '../services/favorites.service';
import { AuthService, CustomerInfo } from '../services/auth.service';
import { CategoryHeading, CategoryRow, groupedByHeading } from '../models/category-tree';
import { SearchSuggestions } from '../models/product.model';
import { SuggestionRow, rowsFor, SHORTEST_SUGGESTED_QUERY } from '../models/search-suggestions';

/**
 * How long typing has to pause before the header's box asks the shop what the words suggest: long enough that the words
 * are not asked about letter by letter while they are being typed, short enough that the list is there by the time a
 * shopper looks down from the box.
 */
const SUGGESTION_PAUSE_MS = 250;

/** What the shop answers with when it has nothing to offer: the panel is told by the rows it is drawn from. */
const NO_SUGGESTIONS: SearchSuggestions = { Categories: [], Products: [] };

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

  /**
   * Whether the header's search box is open. The box is a panel under the bar rather than a field always on show, so
   * the bar reads the same whether or not a shopper is searching.
   */
  isSearchOpen = false;

  /** What is being typed in the header's search box; the shop is asked for it when the form is submitted. */
  searchTerm = '';

  /**
   * The rows the panel offers under the box: the words themselves, the collections they name and the pieces whose names
   * they appear in, as the shop answered (see ProductService.getSearchSuggestions). Empty until the shop has answered,
   * and while there is nothing to offer - the panel draws a list of rows, not of answers.
   */
  suggestionRows: SuggestionRow[] = [];

  /** Which row the keyboard is on, or -1 when it is on none of them and Enter searches. */
  activeSuggestion = -1;

  /** The words as they are typed; they are asked about once typing pauses (see ngOnInit). */
  private readonly typedWords = new Subject<string>();

  /** Ends the asking when the component goes, so no answer outlives the panel. */
  private readonly stopSuggesting = new Subject<void>();

  /** The button that opens the box, so closing it from the keyboard can put the caret back where it came from. */
  @ViewChild('searchToggle') private searchToggle?: ElementRef<HTMLButtonElement>;

  /** The box itself, which is drawn by the panel being open (see toggleSearch). */
  @ViewChild('searchInput') private searchInput?: ElementRef<HTMLInputElement>;

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
    private router: Router,
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

    // The words are asked about a pause after typing stops rather than on every key, and only the answer to the words
    // still in the box is kept (switchMap drops the one that was on its way when they moved on): a list that answered
    // an earlier keystroke would be a list of places the shopper did not ask for. The same words are asked about again
    // when the panel is opened on them, so no repeat of a search is folded away here.
    this.typedWords.pipe(
      map(text => text.trim()),
      debounceTime(SUGGESTION_PAUSE_MS),
      switchMap(text => this.suggestionsFor(text)),
      takeUntil(this.stopSuggesting)
    ).subscribe(({ text, suggestions }) => {
      // An answer that arrives after the panel was closed or the search submitted is let go: nothing is drawn.
      if (!this.isSearchOpen) {
        return;
      }

      this.suggestionRows = rowsFor(text, suggestions);
      this.activeSuggestion = -1;
    });
  }

  ngOnDestroy(): void {
    this.authSubscription?.unsubscribe();
    this.stopSuggesting.next();
    this.stopSuggesting.complete();
  }

  /**
   * What the shop offers for the words in the box, carried together with them so the rows are drawn for the text they
   * were asked about. Words too short to ask about, and a page drawn on the server, answer with nothing.
   */
  private suggestionsFor(text: string) {
    if (!this.isBrowser || text.length < SHORTEST_SUGGESTED_QUERY) {
      return of({ text, suggestions: NO_SUGGESTIONS });
    }

    return this.productService.getSearchSuggestions(text).pipe(
      // A search that cannot be answered is the panel's to keep quiet about: the shopper can still submit it.
      catchError(() => of(NO_SUGGESTIONS)),
      map(suggestions => ({ text, suggestions }))
    );
  }

  toggleMenu(): void {
    this.isMenuOpen = !this.isMenuOpen;
  }

  closeMenu(): void {
    this.isMenuOpen = false;
  }

  /**
   * Opens or closes the header's search box. What was typed is kept when the box is closed with the button, so a
   * shopper who shut it by accident finds their words again; the form is submitted to go anywhere.
   *
   * Opening it puts the caret in the box, so a shopper can type at once rather than having to find the field first -
   * and asks the shop about the words already there, so the list a shopper had is there when the panel is.
   */
  toggleSearch(): void {
    if (this.isSearchOpen) {
      this.closeSearch();
      return;
    }

    this.isSearchOpen = true;
    this.typedWords.next(this.searchTerm);

    // The box is drawn by the panel being open, so it is not in the page yet: the focus waits for it to be drawn,
    // which is one turn of the browser's own loop rather than one of Angular's.
    if (this.isBrowser) {
      setTimeout(() => this.searchInput?.nativeElement.focus());
    }
  }

  /**
   * Hands the words to the shop as they are typed (see ngOnInit): the list under the box is the shop's answer to them,
   * a pause after typing stops. The answer already there stays until the new one is in, so the list does not blink away
   * between keystrokes - only the row the keyboard was on is let go, because the words it was chosen for have moved on.
   */
  onSearchInput(text: string): void {
    this.activeSuggestion = -1;
    this.typedWords.next(text);
  }

  /**
   * The arrows walk the list under the box and Enter takes the row the keyboard is on - a list of places to go has to
   * be usable without a mouse. With no row chosen, Enter falls through to the form and searches, which is what the box
   * does on its own.
   */
  onSearchKeydown(event: KeyboardEvent): void {
    const rowCount = this.suggestionRows.length;

    if (rowCount === 0) {
      return;
    }

    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        this.activeSuggestion = (this.activeSuggestion + 1) % rowCount;
        break;
      case 'ArrowUp':
        event.preventDefault();
        this.activeSuggestion = this.activeSuggestion <= 0 ? rowCount - 1 : this.activeSuggestion - 1;
        break;
      case 'Enter':
        if (this.activeSuggestion >= 0) {
          event.preventDefault();
          this.openSuggestion(this.suggestionRows[this.activeSuggestion]);
        }
        break;
    }
  }

  /**
   * Takes the shopper where a suggestion leads: the collection opened (the shop on it, which shows everything filed
   * beneath it), the piece's own page, or the shop searched for the words - the same search the form submits.
   */
  openSuggestion(row: SuggestionRow): void {
    if (row.kind === 'category' && row.categoryId !== undefined) {
      this.closeSearch();
      this.closeMenu();
      this.router.navigate(['/shop', row.categoryId]);
      return;
    }

    if (row.kind === 'product' && row.productId !== undefined) {
      this.closeSearch();
      this.closeMenu();
      this.router.navigate(['/product', row.productId]);
      return;
    }

    this.submitSearch();
  }

  /**
   * Sends the shopper to the shop searched for what they typed. The words are carried in the address rather than kept
   * here, so a search is a place a shopper can be sent to, keep, or come back to - and the shop reads them itself (see
   * ShopComponent), which is what lets a search be linked to at all.
   */
  submitSearch(): void {
    const term = this.searchTerm.trim();

    if (!term) {
      return;
    }

    this.searchTerm = term;
    this.closeSearch();
    this.closeMenu();
    this.router.navigate(['/shop'], { queryParams: { q: term } });
  }

  /** Empties the search box without leaving the page it is open on, and puts the caret back in it to type again. */
  clearSearch(): void {
    this.searchTerm = '';

    // The box was emptied by code, so nothing was typed: the list drawn for the words that were there is let go here.
    this.suggestionRows = [];
    this.activeSuggestion = -1;

    if (this.isBrowser) {
      this.searchInput?.nativeElement.focus();
    }
  }

  /**
   * Closes the box and lets go of the list under it, so opening it again starts from what is in the box rather than
   * from the answer to words that are no longer being typed.
   */
  private closeSearch(): void {
    this.isSearchOpen = false;
    this.suggestionRows = [];
    this.activeSuggestion = -1;
  }

  /**
   * Escape closes the search box, so a keyboard alone can back out of it - and leaves the caret on the button that
   * opened it rather than nowhere at all.
   */
  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (!this.isSearchOpen) {
      return;
    }

    this.closeSearch();

    if (this.isBrowser) {
      this.searchToggle?.nativeElement.focus();
    }
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
