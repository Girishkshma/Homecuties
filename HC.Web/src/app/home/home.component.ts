import { Component, OnInit, OnDestroy } from '@angular/core';
import { Subscription } from 'rxjs';
import { ProductService } from '../services/product.service';
import { FavoritesService } from '../services/favorites.service';
import { AuthService } from '../services/auth.service';
import { CartService, CartItem } from '../services/cart.service';
import { Product, Category, HomeStats } from '../models/product.model';
import { UtilityService } from '../services/utility.service';

@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  standalone: false,
  styleUrl: './home.component.scss'
})
export class HomeComponent implements OnInit, OnDestroy {
  products: Product[] = [];
  categories: Category[] = [];
  stats: HomeStats | null = null;
  loading = true;
  error = '';
  UtilityService = UtilityService;
  favoriteProductIds: Set<number> = new Set();
  cartQuantities: Map<number, number> = new Map();
  private cartSubscription?: Subscription;

  /* Category tile artwork: hand-drawn Channapatna SVGs under src/assets/images/channapatna.
     Each tile is drawn at 400 x 300 with the piece centred on the canvas, and
     .category-image-wrapper is locked to the same 4:3, so object-fit: cover never crops the
     illustration. */
  // Category image mapping based on category name
  private readonly categoryImageMap: { [key: string]: string } = {
    'Toys': '/images/channapatna/toys.svg',
    'Decoratives': '/images/channapatna/decoratives.svg',
    'Office/Study': '/images/channapatna/office-study.svg',
    'Households': '/images/channapatna/households.svg',
    'Furnitures': '/images/channapatna/furnitures.svg',
    'Vases': '/images/channapatna/vases.svg',
    'Pot Houses': '/images/channapatna/pot-houses.svg',
    'Musicians': '/images/channapatna/musicians.svg',
    'Show Pieces': '/images/channapatna/show-pieces.svg',
    'Pen Stands': '/images/channapatna/pen-stands.svg',
    'Calendars': '/images/channapatna/calendars.svg',
    'Costers': '/images/channapatna/costers.svg',
    'Center Tables': '/images/channapatna/center-tables.svg',
    'For Kids': '/images/channapatna/for-kids.svg',
    'Mobile Stands': '/images/channapatna/mobile-stands.svg',
    'Utilities': '/images/channapatna/utilities.svg'
  };

  constructor(
    private productService: ProductService,
    private favoritesService: FavoritesService,
    private authService: AuthService,
    private cartService: CartService
  ) {}

  ngOnInit(): void {
    this.loadProducts();
    this.loadCategories();
    this.loadStats();
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

  getCategoryImage(category: Category): string {
    return this.categoryImageMap[category.CategoryName] || '/images/channapatna/decoratives.svg';
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
    this.productService.getProductsForHomepage().subscribe({
      next: (data) => {
        this.products = data;
        this.loading = false;
        this.loadFavoriteStatus();
      },
      error: (err) => {
        this.error = 'Failed to load products. Please try again later.';
        this.loading = false;
        console.error('Error loading products:', err);
      }
    });
  }

  private loadCategories(): void {
    this.productService.getCategories().subscribe({
      next: (data) => this.categories = data,
      error: () => console.error('Failed to load categories')
    });
  }

  /** Hero counters come from the database (products / registered customers / categories). */
  private loadStats(): void {
    this.productService.getHomeStats().subscribe({
      next: (data) => this.stats = data,
      error: () => console.error('Failed to load home stats')
    });
  }

  private loadFavoriteStatus(): void {
    if (this.products.length === 0) return;

    const customer = this.authService.getCurrentCustomer();
    const customerId = customer ? customer.CustomerID : 0;
    const isGuest = !customer;

    // Check each product's favorite status
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
        // Update local cart state
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
