import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { environment } from '../../environments/environment';
import { AdminService } from '../services/admin.service';
import { AuthService } from '../services/auth.service';
import {
  AdminCategory,
  AdminProductDetail,
  CreateProductRequest,
  CreateProductResult,
  GeneratedProductImage,
  ImageTypeOption,
  ProductFormOptions
} from '../models/admin.model';

export interface ProductFeatureRow {
  feature: string;
  isActive: boolean;
}

export interface ProductImageRow {
  imageUrl: string;
  imageTypeId: number;
  imageIndex: number;
  isPromoImage: boolean;
  isActive: boolean;
  width?: number;
  height?: number;
  previewUrl?: string;
}

/**
 * One photo of a product as the form shows it: which photo it is ('imageIndex') and every size the API wrote for
 * it.
 *
 * A photo is a set of rows - one index at four or six sizes - because the main frame of the product page and the
 * strip of thumbnails under it are two sizes of the same photograph. Keeping them together is what makes the form
 * show a photo rather than a list of files, and what lets a photo be removed as the one thing it is.
 */
export interface ProductPhotoRow {
  imageIndex: number;
  rows: ProductImageRow[];
  previewUrl?: string;
}

@Component({
  selector: 'app-product-form',
  templateUrl: './product-form.component.html',
  styleUrls: ['./product-form.component.scss'],
  standalone: false
})
export class ProductFormComponent implements OnInit {
  categories: AdminCategory[] = [];
  options: ProductFormOptions = { statuses: [], imageTypes: [] };
  productId: number | null = null;
  isEditMode = false;
  isLoading = true;
  isSaving = false;
  message = '';
  isError = false;
  imageBaseUrl = environment.apiUrl.replace(/\/api\/admin\/?$/, '');

  form = {
    productName: '',
    productTitle: '',
    productDescription: '',
    displayOnHomePage: false,
    productStatusId: 0,
    unitPrice: 0,
    hsncode: '',
    packagingCharge: 0,
    storageCharge: 0,
    discountPercent: 0,
    additionalDiscountPercent: 0,
    deliveryCharge: 0,
    profitMarginPercent: 0,
    cgstpercent: 0,
    sgstpercent: 0,
    igstpercent: 0,
    categoryIds: [] as number[],
    features: [] as ProductFeatureRow[],
    images: [] as ProductImageRow[]
  };

  constructor(
    private adminService: AdminService,
    private authService: AuthService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.productId = Number(idParam);
      this.isEditMode = true;
    }
    this.isLoading = true;
    this.loadLookups();
  }

  loadLookups(): void {
    this.adminService.getProductFormOptions().subscribe({
      next: (options) => {
        this.options = options;
        // The sizes the storefront draws come ticked, so the usual upload needs nothing chosen; the admin unticks what
        // a photograph does not need and ticks anything else it does. A type the uploader has no box for (width 0) is
        // left unticked and is shown without a tick to give, see the picker's markup.
        this.selectedImageTypeIds = options.imageTypes
          .filter(type => type.generatedByDefault && type.width > 0)
          .map(type => type.imageTypeId);
        if (this.isEditMode && this.productId) {
          this.loadProduct(this.productId);
        } else {
          this.initDefaults();
        }
      },
      error: () => {
        this.message = 'Failed to load product options.';
        this.isError = true;
        this.isLoading = false;
      }
    });
    this.adminService.getCategories().subscribe({
      next: (data) => this.categories = data,
      error: () => this.categories = []
    });
  }

  initDefaults(): void {
    this.form.productStatusId = this.options.statuses[0]?.productStatusId ?? 1;
    this.form.displayOnHomePage = false;
    this.form.features = [];
    this.form.images = [];
    this.isLoading = false;
  }

  loadProduct(id: number): void {
    this.adminService.getProductDetail(id).subscribe({
      next: (p) => {
        this.form = {
          productName: p.productName,
          productTitle: p.productTitle,
          productDescription: p.productDescription,
          displayOnHomePage: p.displayOnHomePage,
          productStatusId: p.productStatusId,
          unitPrice: p.unitPrice,
          hsncode: p.hsncode || '',
          packagingCharge: p.packagingCharge,
          storageCharge: p.storageCharge,
          discountPercent: p.discountPercent,
          additionalDiscountPercent: p.additionalDiscountPercent,
          deliveryCharge: p.deliveryCharge,
          profitMarginPercent: p.profitMarginPercent,
          cgstpercent: p.cgstpercent,
          sgstpercent: p.sgstpercent,
          igstpercent: p.igstpercent,
          categoryIds: p.categoryIds,
          features: p.features.map(f => ({ feature: f.feature, isActive: f.isActive })),
          images: p.images.map(i => ({
            imageUrl: i.imageUrl,
            imageTypeId: i.imageTypeId,
            imageIndex: i.imageIndex,
            isPromoImage: i.isPromoImage,
            isActive: i.isActive,
            // Every row's file is in the shop's own image folder, so every row has a preview: the API serves the
            // folder the uploader writes to.
            previewUrl: `${this.imageBaseUrl}/images/products/${i.imageUrl}`
          }))
        };
        this.isLoading = false;
      },
      error: () => {
        this.message = 'Failed to load product details.';
        this.isError = true;
        this.isLoading = false;
      }
    });
  }

  get isProductIdValid(): boolean {
    return this.form.productStatusId > 0;
  }

  // Categories
  categoryLabel(category: AdminCategory): string {
    return category.parentCategoryName
      ? `${category.parentCategoryName} / ${category.categoryName}`
      : category.categoryName;
  }

  isCategorySelected(categoryId: number): boolean {
    return this.form.categoryIds.includes(categoryId);
  }

  toggleCategory(categoryId: number, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    if (checked) {
      if (!this.form.categoryIds.includes(categoryId)) {
        this.form.categoryIds.push(categoryId);
      }
    } else {
      this.form.categoryIds = this.form.categoryIds.filter(id => id !== categoryId);
    }
  }

  // Features
  addFeature(): void {
    this.form.features.push({ feature: '', isActive: true });
  }

  removeFeature(index: number): void {
    this.form.features.splice(index, 1);
  }

  // Images
  /**
   * True while the photograph the admin just picked is being written. One upload covers every size ticked for it, so
   * there is one such flag rather than one per row: no size is ever uploaded by hand.
   */
  photoUploading = false;
  photoUploadError = '';

  /**
   * The sizes the next upload is to write, as the ids of the shop's own 'ImageTypes' rows: the ticks on the upload
   * screen. It starts as the sizes the storefront draws (the ones the API marks 'generatedByDefault'), and the admin
   * changes it per photograph - the API writes exactly what is ticked here and nothing else.
   */
  selectedImageTypeIds: number[] = [];

  /**
   * The product's photos, in the order the admin uploaded them, each with the sizes the API wrote for it. This is
   * what the form shows - a photo, not a list of files - and what it removes, so a photograph cannot be left behind
   * at one size and gone at another.
   */
  get photos(): ProductPhotoRow[] {
    const indices = Array.from(new Set(this.form.images.map(i => i.imageIndex))).sort((a, b) => a - b);

    return indices.map(imageIndex => {
      const rows = this.form.images.filter(i => i.imageIndex === imageIndex);

      return {
        imageIndex,
        rows,
        // The promo image is what the shop's cards, cart rows and order rows are pictured from, so it is the one
        // worth showing as the photo's own picture.
        previewUrl: (rows.find(row => row.isPromoImage) ?? rows[0])?.previewUrl
      };
    });
  }

  /** The index the next uploaded photo is written as: photos are numbered from 1 and never reused. */
  nextImageIndex(): number {
    return this.form.images.reduce((highest, row) => Math.max(highest, row.imageIndex), 0) + 1;
  }

  /**
   * Tells one photo from another across redraws. The form builds its photo cards out of the saved rows every time it
   * is checked, so without this every check would rebuild them - and a rebuilt preview is a photograph the browser
   * asks for again.
   */
  trackPhoto(_index: number, photo: ProductPhotoRow): number {
    return photo.imageIndex;
  }

  /** The same, for the sizes inside a photo: the file is what each row is. */
  trackImageRow(_index: number, row: ProductImageRow): string {
    return row.imageUrl;
  }

  /**
   * The one upload control of the form: the admin picks a photograph and the API writes it in the sizes ticked above,
   * at the next photo's index. The rows that come back are that photo's; the file never passes through the form, and no
   * size is written that the admin did not ask for.
   *
   * The control belongs to a saved product's page and is not shown while one is being created (see the Images section of
   * the markup): the API names a photo's files after the product's id, so a product has to be saved before it can have
   * one - which is why creating a product opens its edit page, where this picker is.
   */
  onPhotoSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files && input.files[0];
    input.value = '';
    if (!file) return;

    // Nothing to write a photo for until the product has an id to name its files after. The picker is not offered
    // before then either, so this is the answer to a form that was left open across the create.
    if (!this.isEditMode || !this.productId) {
      this.photoUploadError = 'Save the product first - its photos are added from its own page.';
      return;
    }

    // A photograph with no size ticked would be a photograph with no file, so nothing is sent: the API refuses such an
    // upload too, but saying so here saves the round trip and leaves the answer where the admin is looking.
    if (this.selectedImageTypeIds.length === 0) {
      this.photoUploadError = 'Tick at least one size to write the photo in.';
      return;
    }

    const productId = this.productId;
    const imageIndex = this.nextImageIndex();
    this.photoUploading = true;
    this.photoUploadError = '';

    this.adminService
      .uploadProductImage(file, productId, imageIndex, this.selectedImageTypeIds)
      .subscribe({
        next: (result) => {
          this.photoUploading = false;
          if (result.result !== 1) {
            this.photoUploadError = result.messages[0] || 'Upload failed.';
            return;
          }

          this.addPhotoRows(result.images);
        },
        error: () => {
          this.photoUploading = false;
          this.photoUploadError = 'Upload failed. Please check your connection and try again.';
        }
      });
  }

  /**
   * The sizes the API wrote for one photo, as the rows that are saved with the product. They replace that photo's
   * rows rather than joining them: uploading a photo again writes the same file names, so the old rows would point
   * at the same files and only make the form show the photo twice.
   */
  private addPhotoRows(images: GeneratedProductImage[]): void {
    const imageIndex = images[0]?.imageIndex ?? 0;

    this.form.images = this.form.images.filter(row => row.imageIndex !== imageIndex);
    this.form.images.push(...images.map(image => ({
      imageUrl: image.fileName,
      imageTypeId: image.imageTypeId,
      imageIndex: image.imageIndex,
      isPromoImage: image.isPromoImage,
      isActive: true,
      width: image.width,
      height: image.height,
      previewUrl: `${this.imageBaseUrl}${image.url}`
    })));
  }

  /** Removes a photo: every size of it, because half a photo is a broken frame on the storefront. */
  removePhoto(imageIndex: number): void {
    this.form.images = this.form.images.filter(row => row.imageIndex !== imageIndex);
  }

  /** The name of a size the way the shop's own 'ImageTypes' row spells it, with its short code - 'Large (L)'. */
  imageTypeName(imageTypeId: number): string {
    const t = this.options.imageTypes.find(x => x.imageTypeId === imageTypeId);
    return t ? `${t.imageTypeName}${t.shortCode ? ` (${t.shortCode})` : ''}` : 'Unknown';
  }

  /** Whether the next upload is to write the given size: what that size's tick says. */
  isSizeSelected(imageTypeId: number): boolean {
    return this.selectedImageTypeIds.includes(imageTypeId);
  }

  /** Ticks or unticks one size for the next upload. */
  toggleSize(imageTypeId: number, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    if (checked) {
      if (!this.selectedImageTypeIds.includes(imageTypeId)) this.selectedImageTypeIds.push(imageTypeId);
    } else {
      this.selectedImageTypeIds = this.selectedImageTypeIds.filter(id => id !== imageTypeId);
    }
  }

  /**
   * Whether the uploader has a box to write this size into. A type the shop's 'ImageTypes' table names and the uploader
   * does not know is shown without a tick to give, rather than as a tick that would quietly write nothing.
   */
  hasSizeBox(type: ImageTypeOption): boolean {
    return type.width > 0 && type.height > 0;
  }
  private num(v: any): number {
    const n = Number(v);
    return isNaN(n) ? 0 : n;
  }

  validateForm(): string | null {
    if (!this.form.productName.trim()) return 'Product name is required.';
    if (!this.form.productTitle.trim()) return 'Product title is required.';
    if (this.form.productStatusId <= 0) return 'Select a product status.';
    if (this.num(this.form.unitPrice) < 0) return 'Unit price cannot be negative.';
    return null;
  }

  saveProduct(): void {
    const error = this.validateForm();
    if (error) {
      this.message = error;
      this.isError = true;
      return;
    }

    const actor = this.authService.getUser();
    const actorUserId = actor?.userId ?? 0;

    const request: CreateProductRequest = {
      productName: this.form.productName.trim(),
      productTitle: this.form.productTitle.trim(),
      productDescription: this.form.productDescription?.trim() || '',
      displayOnHomePage: this.form.displayOnHomePage,
      productStatusId: this.form.productStatusId,
      unitPrice: this.num(this.form.unitPrice),
      hsncode: this.form.hsncode?.trim() || undefined,
      packagingCharge: this.num(this.form.packagingCharge),
      storageCharge: this.num(this.form.storageCharge),
      discountPercent: this.num(this.form.discountPercent),
      additionalDiscountPercent: this.num(this.form.additionalDiscountPercent),
      deliveryCharge: this.num(this.form.deliveryCharge),
      profitMarginPercent: this.num(this.form.profitMarginPercent),
      cgstpercent: this.num(this.form.cgstpercent),
      sgstpercent: this.num(this.form.sgstpercent),
      igstpercent: this.num(this.form.igstpercent),
      categoryIds: this.form.categoryIds,
      features: this.form.features
        .filter(f => f.feature && f.feature.trim())
        .map(f => ({ feature: f.feature.trim(), isActive: f.isActive })),
      images: this.form.images
        .filter(img => img.imageUrl && img.imageUrl.trim())
        .map((img, i) => ({
          imageUrl: img.imageUrl.trim(),
          imageTypeId: img.imageTypeId,
          imageIndex: img.imageIndex > 0 ? img.imageIndex : i + 1,
          isPromoImage: img.isPromoImage,
          isActive: img.isActive
        }))
    };

    this.isSaving = true;
    this.message = '';
    this.isError = false;

    if (this.isEditMode && this.productId) {
      this.adminService.updateProduct(this.productId, request, actorUserId).subscribe({
        next: (result) => this.handleSaveResult(result),
        error: () => this.handleSaveError()
      });
    } else {
      this.adminService.createProduct(request, actorUserId).subscribe({
        next: (result) => this.handleCreateResult(result),
        error: () => this.handleSaveError()
      });
    }
  }

  /**
   * A created product opens its own page, rather than going back to the list: its photos are added from that page and
   * not while it was being created, because the API names a photo's files after the product's id (see
   * ProductImageVariants.FileName in HC.Business). The create form has done its job the moment the product exists, so
   * what opens is the page that can finish it - the product's edit page, with its picture picker.
   */
  handleCreateResult(result: CreateProductResult): void {
    this.isSaving = false;

    if (result.result !== 1) {
      this.message = result.messages[0];
      this.isError = true;
      return;
    }

    if (result.productId) {
      this.router.navigate(['/products', result.productId]);
      return;
    }

    // A create that named no product cannot open the page its photos belong on, and the form must not be left able to
    // create the product a second time: the list is where the admin is told what happened.
    this.message = result.messages[0];
    this.isError = false;
    setTimeout(() => this.router.navigate(['/products']), 1400);
  }

  handleSaveResult(result: { result: number; messages: string[] }): void {
    this.isSaving = false;
    if (result.result === 1) {
      this.message = result.messages[0];
      this.isError = false;
      setTimeout(() => this.router.navigate(['/products']), 1400);
    } else {
      this.message = result.messages[0];
      this.isError = true;
    }
  }

  handleSaveError(): void {
    this.isSaving = false;
    this.message = 'Unable to save product. Please try again.';
    this.isError = true;
  }

  goBack(): void {
    this.router.navigate(['/products']);
  }
}
