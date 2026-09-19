import { Component, OnInit, OnDestroy, HostListener } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ProductService } from '../services/product.service';
import { FavoritesService } from '../services/favorites.service';
import { AuthService } from '../services/auth.service';
import { CartService } from '../services/cart.service';
import { Product } from '../models/product.model';
import { UtilityService } from '../services/utility.service';

@Component({
  selector: 'app-product-details',
  templateUrl: './product-details.component.html',
  standalone: false,
  styleUrl: './product-details.component.scss'
})
export class ProductDetailsComponent implements OnInit, OnDestroy {
  product: Product | null = null;
  loading = true;
  error = '';
  quantity = 1;
  selectedImage = '';
  addedToCart = false;
  isFavorite = false;
  UtilityService = UtilityService;

  // Image carousel
  currentIndex = 0;
  private autoSlideTimer: any = null;
  private readonly autoSlideInterval = 3000;
  isHovering = false;

  constructor(
    private route: ActivatedRoute,
    private productService: ProductService,
    private favoritesService: FavoritesService,
    private authService: AuthService,
    private cartService: CartService
  ) {}

  ngOnInit(): void {
    this.route.params.subscribe(params => {
      const productId = params['id'];
      if (productId) {
        this.loadProduct(productId);
      }
    });
  }

  ngOnDestroy(): void {
    this.stopAutoSlide();
  }

  get images(): string[] {
    if (!this.product) return [];
    const images = this.product.ProductImages || [];
    // Ensure PromoImage is first if it exists and isn't already in the list
    if (this.product.PromoImage && !images.includes(this.product.PromoImage)) {
      return [this.product.PromoImage, ...images];
    }
    return images.length > 0 ? images : [this.product.PromoImage];
  }

  get hasMultipleImages(): boolean {
    return this.images.length > 1;
  }

  private loadProduct(productId: string): void {
    this.productService.getProduct(productId).subscribe({
      next: (data) => {
        this.product = data;
        this.selectedImage = data.PromoImage;
        this.currentIndex = 0;
        this.quantity = 1;
        this.loading = false;
        this.checkFavoriteStatus();
        this.startAutoSlide();
      },
      error: (err) => {
        this.error = 'Failed to load product details.';
        this.loading = false;
        console.error(err);
      }
    });
  }

  private checkFavoriteStatus(): void {
    if (!this.product) return;

    const customer = this.authService.getCurrentCustomer();
    const customerId = customer ? customer.CustomerID : 0;
    const isGuest = !customer;

    this.favoritesService.isInWishList(customerId, this.product.ProductID, isGuest).subscribe({
      next: (isFav) => this.isFavorite = isFav,
      error: () => {}
    });
  }

  toggleFavorite(): void {
    if (!this.product) return;

    const customer = this.authService.getCurrentCustomer();
    const customerId = customer ? customer.CustomerID : 0;
    const isGuest = !customer;

    if (this.isFavorite) {
      this.favoritesService.removeFromWishList(customerId, this.product.ProductID, isGuest).subscribe({
        next: () => {
          this.isFavorite = false;
          this.favoritesService.removeFromLocalWishList(this.product!.ProductID);
        },
        error: (err) => console.error('Error removing from wishlist:', err)
      });
    } else {
      this.favoritesService.addToWishList(customerId, this.product.ProductID, isGuest).subscribe({
        next: () => {
          this.isFavorite = true;
          this.favoritesService.addToLocalWishList({
            CustomerId: customerId,
            ProductId: this.product!.ProductID,
            AddedOn: new Date().toISOString(),
            ProductName: this.product!.ProductName,
            ProductTitle: this.product!.ProductTitle,
            ProductDescription: this.product!.ProductDescription,
            PromoImage: this.product!.PromoImage || '',
            SalesPrice: this.product!.SalesPrice,
            PostDiscountSalesPrice: this.product!.PostDiscountSalesPrice,
            PostAdditionalDiscountSalesPrice: this.product!.PostAdditionalDiscountSalesPrice,
            DiscountPercent: this.product!.DiscountPercent || 0,
            AdditionalDiscountPercent: this.product!.AdditionalDiscountPercent || 0,
            IsInStock: this.product!.IsInStock
          });
        },
        error: (err) => console.error('Error adding to wishlist:', err)
      });
    }
  }

  selectImage(image: string): void {
    this.selectedImage = image;
    this.currentIndex = this.images.indexOf(image);
    // Reset auto-slide timer on manual selection
    this.restartAutoSlide();
  }

  prevImage(): void {
    if (this.images.length === 0) return;
    this.currentIndex = (this.currentIndex - 1 + this.images.length) % this.images.length;
    this.selectedImage = this.images[this.currentIndex];
    this.restartAutoSlide();
  }

  nextImage(): void {
    if (this.images.length === 0) return;
    this.currentIndex = (this.currentIndex + 1) % this.images.length;
    this.selectedImage = this.images[this.currentIndex];
    this.restartAutoSlide();
  }

  onMouseEnter(): void {
    this.isHovering = true;
    this.stopAutoSlide();
  }

  onMouseLeave(): void {
    this.isHovering = false;
    this.startAutoSlide();
  }

  private startAutoSlide(): void {
    if (this.autoSlideTimer || !this.hasMultipleImages) return;
    this.autoSlideTimer = setInterval(() => {
      if (!this.isHovering) {
        this.currentIndex = (this.currentIndex + 1) % this.images.length;
        this.selectedImage = this.images[this.currentIndex];
      }
    }, this.autoSlideInterval);
  }

  private stopAutoSlide(): void {
    if (this.autoSlideTimer) {
      clearInterval(this.autoSlideTimer);
      this.autoSlideTimer = null;
    }
  }

  private restartAutoSlide(): void {
    this.stopAutoSlide();
    this.startAutoSlide();
  }

  addToCart(): void {
    if (!this.product) return;
    this.cartService.addToCart(0, true, this.product.ProductID, this.quantity).subscribe({
      next: () => {
        this.addedToCart = true;
        this.cartService.addToLocalCart({
          productId: this.product!.ProductID,
          productName: '',
          quantity: this.quantity,
          price: 0,
          image: ''
        });
        setTimeout(() => this.addedToCart = false, 3000);
      },
      error: (err) => console.error('Failed to add to cart:', err)
    });
  }

  incrementQuantity(): void {
    if (this.quantity >= this.maxQuantity) return;
    this.quantity++;
  }

  decrementQuantity(): void {
    if (this.quantity > 1) {
      this.quantity--;
    }
  }

  /** Highest quantity the customer may order = sellable stock (never below 1). */
  get maxQuantity(): number {
    const available = this.product?.AvailableQty ?? 0;
    return available > 0 ? available : 1;
  }

  get isAtMaxQuantity(): boolean {
    return this.quantity >= this.maxQuantity;
  }

  get isLowStock(): boolean {
    return !!this.product?.IsInStock && this.maxQuantity <= 3;
  }
}
