import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { environment } from '../../environments/environment';
import { AdminService } from '../services/admin.service';
import { AuthService } from '../services/auth.service';
import {
  AdminCategory,
  AdminProductDetail,
  CreateProductRequest,
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
  previewUrl?: string;
  uploading?: boolean;
  uploadError?: string;
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
            previewUrl: /^prod_/.test(i.imageUrl)
              ? `${this.imageBaseUrl}/images/products/${i.imageUrl}`
              : undefined
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
  addImage(): void {
    this.form.images.push({
      imageUrl: '',
      imageTypeId: this.options.imageTypes[0]?.imageTypeId ?? 1,
      imageIndex: this.form.images.length + 1,
      isPromoImage: false,
      isActive: true,
      previewUrl: undefined,
      uploading: false,
      uploadError: undefined
    });
  }

  removeImage(index: number): void {
    this.form.images.splice(index, 1);
  }

  onImageSelected(index: number, event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files && input.files[0];
    if (!file) return;

    const image = this.form.images[index];
    if (!image) return;

    image.uploading = true;
    image.uploadError = undefined;
    this.adminService.uploadProductImage(file).subscribe({
      next: (result) => {
        image.uploading = false;
        if (result.result === 1) {
          image.imageUrl = result.fileName;
          image.previewUrl = `${this.imageBaseUrl}${result.url}`;
        } else {
          image.uploadError = result.messages[0] || 'Upload failed.';
        }
      },
      error: () => {
        image.uploading = false;
        image.uploadError = 'Upload failed. Please check your connection and try again.';
      }
    });

    input.value = '';
  }

  imageTypeName(imageTypeId: number): string {
    const t = this.options.imageTypes.find(x => x.imageTypeId === imageTypeId);
    return t ? `${t.imageTypeName}${t.shortCode ? ` (${t.shortCode})` : ''}` : 'Unknown';
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
        next: (result) => this.handleSaveResult(result),
        error: () => this.handleSaveError()
      });
    }
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
