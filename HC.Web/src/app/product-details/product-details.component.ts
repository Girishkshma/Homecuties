import { Component, OnInit, OnDestroy, HostListener, Inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { ProductService } from '../services/product.service';
import { FavoritesService } from '../services/favorites.service';
import { AuthService } from '../services/auth.service';
import { CartService } from '../services/cart.service';
import { Product, Category, ProductPicture } from '../models/product.model';
import { pathOf } from '../models/category-tree';
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
  addedToCart = false;
  isFavorite = false;
  UtilityService = UtilityService;

  /**
   * The category the shopper opened this product from, when they came out of a category listing ('shop/:categoryId').
   * The shop's product links carry it as a query parameter and the breadcrumb uses it to put the category the shopper
   * was browsing back into the trail. Null for a product reached from anywhere else (home, favorites, a direct link),
   * so no category is invented for a walk that never had one.
   */
  fromCategoryId: number | null = null;

  /**
   * The rows the storefront lists, flat (see ProductService.GetCategoriesAsync): what the category the shopper came
   * from is named by, and what the product's own categories are read through. It is the same list every page's menu
   * is built from, so a category is named the same way everywhere.
   */
  categories: Category[] = [];

  // Image carousel
  currentIndex = 0;
  private autoSlideTimer: any = null;
  private readonly autoSlideInterval = 3000;
  isHovering = false;
  private isBrowser: boolean;

  constructor(
    private route: ActivatedRoute,
    private productService: ProductService,
    private favoritesService: FavoritesService,
    private authService: AuthService,
    private cartService: CartService,
    @Inject(PLATFORM_ID) private platformId: Object
  ) {
    this.isBrowser = isPlatformBrowser(this.platformId);
  }

  ngOnInit(): void {
    this.loadCategories();
    this.route.params.subscribe(params => {
      const productId = params['id'];
      if (productId) {
        this.loadProduct(productId);
      }
    });
    // Read after every navigation: the category the shopper came from rides on the URL as a query parameter, so it
    // survives a refresh and a shared link, and a walk in from anywhere else simply carries none.
    this.route.queryParams.subscribe(params => {
      const categoryId = params['categoryId'];
      this.fromCategoryId = categoryId ? Number(categoryId) : null;
    });
  }

  ngOnDestroy(): void {
    this.stopAutoSlide();
  }

  /**
   * The product's photos as this page draws them: for each one the file the main frame takes and the file the strip
   * takes, resolved by the API against the shop's image folder (see ProductGallery in HC.Business). Drawing the
   * product's own image rows instead is what put broken thumbnails in the strip and blank frames under the arrows.
   *
   * A product the API could resolve nothing for - one with no image rows at all, or one whose every file is missing -
   * is still shown as the single picture the shop has always had for it, its promo image, rather than as a page with
   * no picture.
   */
  get pictures(): ProductPicture[] {
    const pictures = this.product?.Pictures ?? [];
    if (pictures.length > 0) return pictures;

    const promo = this.product?.PromoImage;
    return promo ? [{ Large: promo, Thumbnail: promo }] : [];
  }

  /** The file the main frame draws: the current photo's large file. */
  get currentImage(): string {
    return this.pictures[this.currentIndex]?.Large ?? '';
  }

  get hasMultipleImages(): boolean {
    return this.pictures.length > 1;
  }

  /**
   * The category step of the breadcrumb: the category the shopper came from, named through the rows the page holds.
   *
   * A shopper who opened the product out of a heading's listing ('Decoratives', a category the product itself is not
   * filed under - the shop files its pieces on the shelves beneath a heading) keeps that step, and the step goes back
   * to the heading's own listing. Null when nothing was carried, so no category is invented for a walk that never had
   * one.
   */
  get breadcrumbCategory(): Category | null {
    if (!this.fromCategoryId) return null;

    const browsed = this.categories.find(category => category.CategoryID === this.fromCategoryId);
    if (browsed) return browsed;

    // The list has not arrived yet, or the row is one the storefront does not list: the product's own categories are
    // then the only name the page can put on the step.
    return (this.product?.Categories || []).find(category => category.CategoryID === this.fromCategoryId) ?? null;
  }

  /**
   * Where a category the product is filed under sits in the catalogue, heading first: 'Decoratives / Vases'. The chip
   * says which collection a piece belongs to and not only the shelf it is on.
   */
  categoryPath(category: Category): string {
    return pathOf(category, this.categories);
  }

  /**
   * The rows the storefront lists, for naming the categories this page shows. The page never lists them itself, so
   * the read failing leaves the page as it was rather than stopping it.
   */
  private loadCategories(): void {
    this.productService.getCategories().subscribe({
      next: (data) => this.categories = data,
      error: () => console.error('Failed to load categories')
    });
  }

  private loadProduct(productId: string): void {
    this.productService.getProduct(productId).subscribe({
      next: (data) => {
        this.product = data;
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
            ListingPrice: this.product!.ListingPrice,
            PreDiscountListingPrice: this.product!.PreDiscountListingPrice,
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

  /**
   * Tells one photo of the gallery from another across redraws: the page builds its photo list out of the product it
   * holds every time it is checked, and a thumbnail that is rebuilt is a photograph the browser asks for again.
   */
  trackPicture(_index: number, picture: ProductPicture): string {
    return picture.Large;
  }

  /**
   * Shows one photo of the product. The photos are held by their place in the gallery rather than by a file: the main
   * frame draws the photo's large file and the strip draws its thumbnail, so which photo is shown is the only thing a
   * click has to say.
   */
  selectImage(index: number): void {
    this.currentIndex = index;
    // Reset auto-slide timer on manual selection
    this.restartAutoSlide();
  }

  prevImage(): void {
    if (this.pictures.length === 0) return;
    this.currentIndex = (this.currentIndex - 1 + this.pictures.length) % this.pictures.length;
    this.restartAutoSlide();
  }

  nextImage(): void {
    if (this.pictures.length === 0) return;
    this.currentIndex = (this.currentIndex + 1) % this.pictures.length;
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

  /**
   * The photos walk by themselves while the shopper is not on the gallery, one photo every few seconds. Nothing is
   * remembered between steps: the main frame draws whichever photo the index points at.
   *
   * The walk is started in a browser only. On the server the page is written out once the application has settled, and
   * a clock that ticks forever never settles: every product with more than one photo held its render open until the
   * server gave up on the request, so no product page was served.
   */
  private startAutoSlide(): void {
    if (!this.isBrowser || this.autoSlideTimer || !this.hasMultipleImages) return;
    this.autoSlideTimer = setInterval(() => {
      if (!this.isHovering) {
        this.currentIndex = (this.currentIndex + 1) % this.pictures.length;
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
