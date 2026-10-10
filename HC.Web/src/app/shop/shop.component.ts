import { Component, OnInit, OnDestroy } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { Subscription } from 'rxjs';
import { ProductService } from '../services/product.service';
import { FavoritesService } from '../services/favorites.service';
import { AuthService } from '../services/auth.service';
import { CartService, CartItem } from '../services/cart.service';
import { Product, Category } from '../models/product.model';
import { CategoryHeading, CategoryRow, groupedByHeading, pathOf } from '../models/category-tree';
import { UtilityService } from '../services/utility.service';

@Component({
  selector: 'app-shop',
  templateUrl: './shop.component.html',
  standalone: false,
  styleUrl: './shop.component.scss'
})
export class ShopComponent implements OnInit, OnDestroy {
  products: Product[] = [];

  /** The rows the API lists, flat: what a product's own category is named by (see categoryPath). */
  categories: Category[] = [];

  /**
   * The catalogue's headings and the shelves under each of them: what the filter draws. The shop's heading is not a
   * place a product sits, so a filter of the shelves alone would give a shopper no way of browsing a collection -
   * opening a heading lists everything filed beneath it (see CategoryBranch on the API side).
   */
  headings: CategoryHeading[] = [];

  /** The rows no heading leads into, if the catalogue holds any (a loop in the table): listed plainly, never dropped. */
  ungrouped: CategoryRow[] = [];

  selectedCategoryId: number | null = null;
  loading = true;
  error = '';
  UtilityService = UtilityService;
  favoriteProductIds: Set<number> = new Set();
  cartQuantities: Map<number, number> = new Map();
  private cartSubscription?: Subscription;

  constructor(
    private productService: ProductService,
    private favoritesService: FavoritesService,
    private authService: AuthService,
    private cartService: CartService,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    this.loadCategories();
    this.route.params.subscribe(params => {
      this.selectedCategoryId = params['categoryId'] ? Number(params['categoryId']) : null;
      this.loadProducts();
    });
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

  toggleFavorite(productId: number): void {
    const customer = this.authService.getCurrentCustomer();
    const customerId = customer ? customer.CustomerID : 0;
    const isGuest = !customer;

    if (this.favoriteProductIds.has(productId)) {
      this.favoritesService.removeFromWishList(customerId, productId, isGuest).subscribe({
        next: () => {
          this.favoriteProductIds.delete(productId);
          this.favoritesService.removeFromLocalWishList(productId);
        },
        error: (err) => console.error('Error removing from wishlist:', err)
      });
    } else {
      this.favoritesService.addToWishList(customerId, productId, isGuest).subscribe({
        next: () => {
          this.favoriteProductIds.add(productId);
          this.favoritesService.addToLocalWishList({
            CustomerId: customerId,
            ProductId: productId,
            AddedOn: new Date().toISOString(),
            ProductName: '',
            ProductTitle: '',
            ProductDescription: '',
            PromoImage: '',
            // The prices of this placeholder row: the browser has only the id here (the list itself is reloaded
            // from the API), and the header badge is all these rows are read for.
            ListingPrice: 0,
            PreDiscountListingPrice: 0,
            SalesPrice: 0,
            PostDiscountSalesPrice: 0,
            PostAdditionalDiscountSalesPrice: 0,
            DiscountPercent: 0,
            AdditionalDiscountPercent: 0,
            IsInStock: true
          });
        },
        error: (err) => console.error('Error adding to wishlist:', err)
      });
    }
  }

  private loadProducts(): void {
    this.loading = true;
    const obs = this.selectedCategoryId
      ? this.productService.getProductsByCategory(this.selectedCategoryId)
      : this.productService.getActiveProducts();

    obs.subscribe({
      next: (data) => {
        this.products = data;
        this.loading = false;
        this.loadFavoriteStatus();
      },
      error: (err) => {
        this.error = 'Failed to load products.';
        this.loading = false;
        console.error(err);
      }
    });
  }

  /**
   * The filter is the catalogue's tree, not its table: the API sends the rows the shop sells from together with the
   * headings above them (see ProductService.GetCategoriesAsync), and they are grouped here so the shelves are
   * offered under the heading each one belongs to. The flat list is kept as well, because a product's own category is
   * named through it (see categoryPath).
   */
  private loadCategories(): void {
    this.productService.getCategories().subscribe({
      next: (data) => {
        const tree = groupedByHeading(data);
        this.categories = data;
        this.headings = tree.headings;
        this.ungrouped = tree.ungrouped;
      },
      error: () => console.error('Failed to load categories')
    });
  }

  /**
   * Where a product is filed, heading first: 'Decoratives / Vases'. A card that named the shelf alone would leave the
   * shopper without the collection the piece belongs to, and that is the half of the hierarchy a listing can say.
   */
  categoryPath(category: Category): string {
    return pathOf(category, this.categories);
  }

  /**
   * The trail of the category being browsed, heading first ('Decoratives / Vases'), or '' for the whole shop. The
   * filter bar names it, so a shopper who stepped into a shelf can read where they are - and what 'All Products' would
   * take them back out of - without working it out from the cards below.
   */
  get browsedPath(): string {
    const row = this.categories.find(category => category.CategoryID === this.selectedCategoryId);
    return row ? this.categoryPath(row) : '';
  }

  /**
   * Whether a heading's column is the one being browsed: the heading itself, or any shelf read under it. The column is
   * marked so the collection a shopper is inside of is said on the filter as well as in the listing.
   */
  headingIsBrowsed(heading: CategoryHeading): boolean {
    if (this.selectedCategoryId === null) {
      return false;
    }

    return this.selectedCategoryId === heading.heading.CategoryID
      || heading.rows.some(row => row.category.CategoryID === this.selectedCategoryId);
  }

  filterByCategory(categoryId: number | null): void {
    this.selectedCategoryId = categoryId;
    this.loadProducts();
  }

  /**
   * The query parameters a product link carries: the category the shopper is browsing, so the product page can put it
   * into its breadcrumb and walk the shopper back to the listing they came from. Empty on 'All Products', where no
   * single category is being browsed and so none belongs in the trail.
   */
  get productLinkQueryParams(): { categoryId?: number } {
    return this.selectedCategoryId ? { categoryId: this.selectedCategoryId } : {};
  }

  private loadFavoriteStatus(): void {
    if (this.products.length === 0) return;

    const customer = this.authService.getCurrentCustomer();
    const customerId = customer ? customer.CustomerID : 0;
    const isGuest = !customer;

    this.products.forEach(product => {
      this.favoritesService.isInWishList(customerId, product.ProductID, isGuest).subscribe({
        next: (isFavorite) => {
          if (isFavorite) {
            this.favoriteProductIds.add(product.ProductID);
          }
        },
        error: () => {}
      });
    });
  }

  addToCart(productId: number): void {
    const customer = this.authService.getCurrentCustomer();
    const customerId = customer ? customer.CustomerID : 0;
    const isGuest = !customer;

    this.cartService.addToCart(customerId, isGuest, productId, 1).subscribe({
      next: () => {
        this.cartService.addToLocalCart({
          productId: productId,
          productName: '',
          quantity: 1,
          price: 0,
          image: ''
        });
      },
      error: (err) => console.error('Error adding to cart:', err)
    });
  }
}
