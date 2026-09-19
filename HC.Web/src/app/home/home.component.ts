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

  // Category image mapping based on category name
  private readonly categoryImageMap: { [key: string]: string } = {
    'Toys': '/images/categories/toys.jpg',
    'Decoratives': '/images/categories/decoratives.jpg',
    'Office/Study': '/images/categories/office-study.jpg',
    'Households': '/images/categories/households.jpg',
    'Furnitures': '/images/categories/furnitures.jpg',
    'Vases': '/images/categories/vases.jpg',
    'Pot Houses': '/images/categories/pot-houses.jpg',
    'Musicians': '/images/categories/musicians.jpg',
    'Show Pieces': '/images/categories/show-pieces.jpg',
    'Pen Stands': '/images/categories/pen-stands.jpg',
    'Calendars': '/images/categories/calendars.jpg',
    'Costers': '/images/categories/costers.jpg',
    'Center Tables': '/images/categories/center-tables.jpg',
    'For Kids': '/images/categories/for-kids.jpg',
    'Mobile Stands': '/images/categories/mobile-stands.jpg',
    'Utilities': '/images/categories/utilities.jpg'
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
    return this.categoryImageMap[category.CategoryName] || '/images/categories/decoratives.jpg';
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
