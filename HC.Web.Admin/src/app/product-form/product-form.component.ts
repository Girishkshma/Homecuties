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
 *
 * 'imageIndex' is also the photo's place in the order: the product page draws the photos in the order of their image
 * indices (see ProductGallery in HC.Business), so putting the photos in the order the shop wants them seen is a
 * matter of numbering them again - which is what the move controls on each card do (see movePhoto).
 */
export interface ProductPhotoRow {
  imageIndex: number;
  rows: ProductImageRow[];
  previewUrl?: string;
}

/** One card of the product form's categories: one of the shop's own categories, and the shelves listed beneath it. */
export interface CategoryCard {
  /** What the card is headed with: a category's own name, or - for rows no heading reaches - what is wrong with them. */
  title: string;

  /** What the card says where its shelves need a word, and nothing where the shelves speak for themselves. */
  note: string;

  /** The rows listed beneath it, each with a tick to give: the rows a product may be filed under. */
  shelves: AdminCategory[];
}

@Component({
  selector: 'app-product-form',
  templateUrl: './product-form.component.html',
  styleUrls: ['./product-form.component.scss'],
  standalone: false
})
export class ProductFormComponent implements OnInit {
  categories: AdminCategory[] = [];

  /** The same categories drawn as cards, one to a heading: see setCategories and buildCategoryCards. */
  categoryCards: CategoryCard[] = [];
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
      next: (data) => this.setCategories(data),
      error: () => this.setCategories([])
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

        // A product saved before headings were ruled out can still carry one. The form draws a heading as the card over
        // its shelves and not as a tick (see buildCategoryCards), and the save below leaves it out, so the admin is told
        // here rather than finding it gone afterwards.
        const headings = p.categoryIds.filter(categoryId => this.isTopLevelCategory(categoryId));
        if (headings.length > 0) {
          const names = this.categories
            .filter(category => headings.includes(category.categoryId))
            .map(category => category.categoryName);
          this.message =
            `A product is filed under a category beneath a heading, so ${names.join(', ')} will be cleared from this ` +
            'product when it is saved: tick a category beneath a heading.';
          this.isError = false;
        }

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

  /**
   * The categories the form draws: the list itself, and the cards built from it (see buildCategoryCards). The two
   * arrive together from the one call, so nothing here reads one without the other - an empty list means no cards, and
   * no cards is the form saying it has nothing to file a product under.
   */
  setCategories(categories: AdminCategory[]): void {
    this.categories = categories;
    this.categoryCards = this.buildCategoryCards();
  }

  /**
   * The form's category cards: one card per one of the shop's own categories, with everything beneath it listed inside
   * - the shape the dashboard's 'Stock by category' cards are drawn in, and the shape a product's own filing has: the
   * heading is what says which shelves belong together, and the shelf is where the product sits.
   *
   * A heading with nothing beneath it yet keeps its card and says so rather than going unlisted: the shop may name a
   * heading before the shelves under it exist, and a category an admin cannot see is one they can neither file against
   * nor ask about.
   *
   * Anything no heading reaches is a row that names a parent naming it back - a loop in the table, which is what
   * CategoryTree leaves to the end of its own list. It is listed in one last card that says what is wrong with it,
   * rather than dropped: it is a row in the table all the same, and one a product may still name.
   */
  private buildCategoryCards(): CategoryCard[] {
    const cards: CategoryCard[] = [];
    const placed = new Set<number>();

    /**
     * Everything beneath a category, however deep, each row once. A row is marked as listed as it is walked, which is
     * what ends a loop in the table rather than following it round and round.
     */
    const beneath = (categoryId: number): AdminCategory[] => {
      const shelves: AdminCategory[] = [];

      for (const row of this.categories) {
        if (row.parentCategoryId !== categoryId || placed.has(row.categoryId)) {
          continue;
        }

        placed.add(row.categoryId);
        shelves.push(row, ...beneath(row.categoryId));
      }

      return shelves;
    };

    for (const heading of this.categories.filter(category => this.isTopLevelCategory(category.categoryId))) {
      placed.add(heading.categoryId);

      const shelves = beneath(heading.categoryId);
      cards.push({
        title: heading.categoryName,
        note: shelves.length === 0
          ? 'Nothing is filed under this category yet, so it is here for the shape of the shop and cannot hold a product.'
          : '',
        shelves
      });
    }

    const unreached = this.categories.filter(category => !placed.has(category.categoryId));
    if (unreached.length > 0) {
      cards.push({
        title: 'Not under a heading',
        note: 'These categories name each other as parent, so no category above them reaches them: repair the ' +
          'Categories table, then file products under the categories that hold them.',
        shelves: unreached
      });
    }

    return cards;
  }

  /**
   * Whether a category is a heading: a top of the tree, which is a row that names no parent - or one whose parent is
   * not in this list at all. That is the question the API's own save asks of a product that names a category (see
   * CategoryTree.TopsOfTree), so this is the question that has to be asked here: a row read the other way round would
   * grey out the shelves under a heading - the only rows a product may be filed under - and offer ticks on the headings
   * the API refuses, which is a screen that cannot file a product at all.
   *
   * The parent decides it, and not what sits beneath the row. The rows that name no parent are the shop's own
   * categories, and every row that names one is a shelf: 'nothing is filed under it' is true of a heading and of a
   * shelf alike, so it cannot tell the two apart.
   *
   * A category this list does not hold is not a heading. The categories and the product's own details are two calls, and
   * while the first has not answered yet, nothing here may read a product's categories as headings and leave them out of
   * the save.
   */
  isTopLevelCategory(categoryId: number): boolean {
    const category = this.categories.find(row => row.categoryId === categoryId);

    if (!category) {
      return false;
    }

    // 'parentCategoryId' is absent on a heading the API sent, which JSON spells as null - hence the == and not a
    // comparison with undefined alone.
    return category.parentCategoryId == null
      || !this.categories.some(row => row.categoryId === category.parentCategoryId);
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
   * The product's photos, in the order the shop shows them, each with the sizes the API wrote for it. This is what the
   * form shows - a photo, not a list of files - and what it removes, so a photograph cannot be left behind at one size
   * and gone at another.
   *
   * The order is the photos' image indices (see ProductGallery in HC.Business): the product page draws the photos in
   * the order of their indices, so the form draws them in that order too, and the two screens cannot disagree about
   * which photo comes first.
   */
  get photos(): ProductPhotoRow[] {
    return this.photoOrder().map(imageIndex => {
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

  /** The image indices of the product's photos, one per photo, in the order the form shows them. */
  private photoOrder(): number[] {
    return Array.from(new Set(this.form.images.map(row => row.imageIndex))).sort((a, b) => a - b);
  }

  /**
   * The index the next uploaded photo is written as: past every index in use, so a new photo never lands on a number
   * one of the photos already on the page is written with.
   */
  nextImageIndex(): number {
    return this.form.images.reduce((highest, row) => Math.max(highest, row.imageIndex), 0) + 1;
  }

  /** Shows a photo one place earlier. Nothing happens to the first photo, which has no earlier place to go. */
  movePhotoUp(imageIndex: number): void {
    this.movePhoto(imageIndex, -1);
  }

  /** The same, one place later. Nothing happens to the last photo. */
  movePhotoDown(imageIndex: number): void {
    this.movePhoto(imageIndex, 1);
  }

  /**
   * Moves one photo by one place in the order the form shows them, and leaves the other photos where they are.
   *
   * The order is the photos' image indices, so a move numbers the photos again - and it numbers them PAST every index
   * in use rather than reusing the numbers the photos already carry. That is what keeps a later upload from writing
   * over a photo the shop still shows: a photo's files are named after the index it was uploaded at ('I10045_L_01.jpg',
   * see ProductImageVariants.FileName in HC.Business), and an upload writes the index the form gives it - so a photo
   * holding row index 1 while its file is named '_02' would be overwritten by the next upload at index 2. Numbering
   * past every index in use means the next upload writes a name nothing points at. The numbers are only ever the
   * order: no screen shows them, and the storefront reads the order alone (see ProductGallery).
   *
   * Every row of a photo moves with it, so a photograph is never left behind at one size and gone at another, and the
   * new order is written when the product is saved, exactly like every other change on this page.
   */
  private movePhoto(imageIndex: number, step: number): void {
    const order = this.photoOrder();
    const from = order.indexOf(imageIndex);
    const to = from + step;

    if (from < 0 || to < 0 || to >= order.length) return;

    [order[from], order[to]] = [order[to], order[from]];

    const highest = order.reduce((max, index) => Math.max(max, index), 0);
    const renumbered = new Map<number, number>();
    order.forEach((index, position) => renumbered.set(index, highest + position + 1));

    this.form.images = this.form.images.map(row => ({
      ...row,
      imageIndex: renumbered.get(row.imageIndex) ?? row.imageIndex
    }));
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
      // Only the categories a product can be filed under: a heading is drawn as the card over its shelves and not as a
      // tick (see buildCategoryCards), and the API refuses a product that names one, so a heading a product still
      // carries - one saved before this rule - is left out of the save rather than refusing the whole product.
      categoryIds: this.form.categoryIds.filter(id => !this.isTopLevelCategory(id)),
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
