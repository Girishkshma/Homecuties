import { CommonModule } from '@angular/common';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { of } from 'rxjs';
import {
  AdminProductDetail,
  AdminProductImage,
  AdminUser,
  CreateProductRequest,
  ProductFormOptions
} from '../models/admin.model';
import { AdminService as AdminApiService } from '../services/admin.service';
import { AuthService } from '../services/auth.service';
import { ProductFormComponent } from './product-form.component';

/**
 * The photos of the admin product form, and the order they are shown in.
 *
 * A product's photos are the rows the uploader wrote, one row per size, and the sizes of one photo share an image
 * index. That index is also the photo's place in the order the storefront draws them: the product page walks the
 * product's pictures in index order, resolved against the shop's image folder by ProductGallery in HC.Business. So the
 * form's job is to show the photos in that order and to let the shop change it, and four things about that are pinned
 * here:
 *
 * - The photos are drawn in the order of their indices, whatever order the rows arrive in, and the sizes of one photo
 *   stay together - a photo is one thing at four or six sizes, not a list of files.
 * - A move puts a photo one place earlier or later and leaves every other photo where it was, and the first photo has
 *   nowhere to go up and the last nowhere to go down.
 * - A move numbers the photos past every index in use rather than reusing the numbers they already have, which is what
 *   keeps the next upload from writing over a photo the shop still shows: a photo's files are named after the index it
 *   was uploaded at ('I10045_L_01.jpg'), so a reorder that reused the numbers could let an upload at index 2 write
 *   over the file of a photo now shown third.
 * - The order the form shows is the order the save sends - one row per size, each at its photo's index - so the page
 *   the shop reorders is the page the storefront draws.
 */

/** The promo file of a photo, which is what the form previews a photo from: the file a test tells photos apart by. */
function promoFile(imageIndex: number): string {
  return `I10045_P_${String(imageIndex).padStart(2, '0')}.jpg`;
}

/** The master file of the same photo, written as L: the second size every photo of the shop has. */
function masterFile(imageIndex: number): string {
  return promoFile(imageIndex).replace('_P_', '_L_');
}

/** One size row of a photo, as the product's detail answer sends it. */
function row(imageIndex: number, imageUrl: string, isPromoImage = false): AdminProductImage {
  return { imageUrl, imageTypeId: isPromoImage ? 6 : 1, imageIndex, isPromoImage, isActive: true };
}

/** One photo of the product as the detail answer sends it: its promo row and its master row, sharing an index. */
function photo(imageIndex: number): AdminProductImage[] {
  return [row(imageIndex, promoFile(imageIndex), true), row(imageIndex, masterFile(imageIndex))];
}

/** The three photos of the product the tests reorder, in the order the form is to open with them. */
function threePhotos(): AdminProductImage[] {
  return [...photo(1), ...photo(2), ...photo(3)];
}

const admin = {
  userId: 1,
  loginId: 'girish',
  firstName: 'Girish',
  lastName: 'S',
  isActive: true,
  roles: []
} as AdminUser;

describe('ProductFormComponent photo order', () => {
  /** The request the form last sent, as the API would receive it. */
  let sent: CreateProductRequest | null;

  /** The product the form is editing, over the image rows the test opened it with. */
  function detail(images: AdminProductImage[]): AdminProductDetail {
    return {
      productId: 10045,
      productName: 'A vase',
      productTitle: 'A vase',
      productDescription: '',
      displayOnHomePage: false,
      productStatusId: 1,
      unitPrice: 100,
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
      categoryIds: [],
      features: [],
      images
    } as AdminProductDetail;
  }

  /**
   * The product's edit page, over the photos the test gives it. The photos are the whole of what the test is about, so
   * every other lookup answers with the least the form draws with.
   */
  function open(images: AdminProductImage[]): {
    fixture: ComponentFixture<ProductFormComponent>;
    component: ProductFormComponent;
    root: HTMLElement;
  } {
    sent = null;

    TestBed.configureTestingModule({
      imports: [CommonModule, FormsModule],
      declarations: [ProductFormComponent],
      providers: [
        {
          provide: AdminApiService,
          useValue: {
            getProductFormOptions: () =>
              of({
                statuses: [{ productStatusId: 1, productStatusName: 'Active' }],
                imageTypes: []
              } as ProductFormOptions),
            getCategories: () => of([]),
            getProductDetail: () => of(detail(images)),
            updateProduct: (_productId: number, request: CreateProductRequest) => {
              sent = request;
              return of({ result: 1, messages: ['Product updated successfully.'] });
            }
          }
        },
        { provide: AuthService, useValue: { getUser: () => admin } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => '10045' } } } },
        { provide: Router, useValue: { navigate: () => Promise.resolve(true) } }
      ]
    });

    const fixture: ComponentFixture<ProductFormComponent> = TestBed.createComponent(ProductFormComponent);
    fixture.detectChanges();

    return { fixture, component: fixture.componentInstance, root: fixture.nativeElement as HTMLElement };
  }

  /** The photos the form shows, by the file each preview is drawn from, in the order it shows them. */
  function shownPhotos(root: HTMLElement): string[] {
    return Array.from(root.querySelectorAll('.image-card img.image-preview')).map(preview =>
      ((preview as HTMLImageElement).src.split('/').pop() as string)
    );
  }

  /** The names written over the cards: 'Photo 1 of 3', which is the position the shop reads rather than an index. */
  function cardTitles(root: HTMLElement): string[] {
    return Array.from(root.querySelectorAll('.photo-title')).map(title => title.textContent?.trim() as string);
  }

  /** The photos the form holds, by the promo file each one is, in the order it holds them. */
  function heldOrder(component: ProductFormComponent): string[] {
    return component.photos.map(photo => (photo.rows.find(row => row.isPromoImage) ?? photo.rows[0]).imageUrl);
  }

  /** The move control of the card showing the given photo, found the way the admin finds it. */
  function moveControl(root: HTMLElement, file: string, label: string): HTMLButtonElement {
    const card = Array.from(root.querySelectorAll('.image-card')).find(candidate =>
      (candidate.querySelector('img.image-preview') as HTMLImageElement | null)?.src.endsWith(file)
    );
    expect(card).withContext(`the card showing ${file} is on the screen`).toBeTruthy();

    const control = Array.from((card as HTMLElement).querySelectorAll('.photo-order button')).find(button =>
      button.textContent?.includes(label)
    );
    expect(control).withContext(`the ${label} control is on the card showing ${file}`).toBeTruthy();

    return control as HTMLButtonElement;
  }

  /**
   * What the storefront will draw once this save is answered, by the promo file of each photo: the product's pictures
   * in the order the product page walks them, which is the order of the image indices ProductGallery reads.
   */
  function drawnOrder(request: CreateProductRequest): string[] {
    return request.images
      .slice()
      .sort((a, b) => a.imageIndex - b.imageIndex)
      .filter(image => image.isPromoImage)
      .map(image => image.imageUrl);
  }

  it('should show the photos in the order of their image indices, whatever order the rows arrive in', () => {
    const { root, component } = open([...photo(2), ...photo(1), ...photo(3)]);

    expect(shownPhotos(root)).toEqual([promoFile(1), promoFile(2), promoFile(3)]);
    expect(cardTitles(root)).toEqual(['Photo 1 of 3', 'Photo 2 of 3', 'Photo 3 of 3']);

    // A photo is one thing at its several sizes: its rows travel together, so no card holds half a photograph.
    expect(component.photos.length).toBe(3);
    expect(component.photos.every(photo => photo.rows.length === 2)).toBeTrue();
  });

  it('should put a photo one place earlier or later when its own control is used, and leave the others alone', () => {
    const { fixture, root, component } = open(threePhotos());

    moveControl(root, promoFile(3), 'Move up').click();
    fixture.detectChanges();

    expect(heldOrder(component)).withContext('the third photo is one place earlier, so it is second').toEqual([
      promoFile(1),
      promoFile(3),
      promoFile(2)
    ]);
    expect(shownPhotos(root)).toEqual([promoFile(1), promoFile(3), promoFile(2)]);
    expect(cardTitles(root)).toEqual(['Photo 1 of 3', 'Photo 2 of 3', 'Photo 3 of 3']);

    // A move is one place at a time, so the shop walks a photo to where it wants it seen: one more click and the photo
    // is the one the product page opens on.
    moveControl(root, promoFile(3), 'Move up').click();
    fixture.detectChanges();

    expect(shownPhotos(root)).toEqual([promoFile(3), promoFile(1), promoFile(2)]);
    expect(cardTitles(root)[0]).toBe('Photo 1 of 3');

    // The photo after the moved one is the one that gives way, and only it: a move trades a pair, not the whole list.
    component.movePhotoDown(component.photos[1].imageIndex);
    fixture.detectChanges();

    expect(heldOrder(component)).toEqual([promoFile(3), promoFile(2), promoFile(1)]);
    expect(cardTitles(root)).toEqual(['Photo 1 of 3', 'Photo 2 of 3', 'Photo 3 of 3']);
  });

  it('should leave the first photo nowhere to go up and the last nowhere to go down', () => {
    const { root, component } = open(threePhotos());

    expect(moveControl(root, promoFile(1), 'Move up').disabled)
      .withContext('nothing is shown before the first photo')
      .toBeTrue();
    expect(moveControl(root, promoFile(3), 'Move down').disabled)
      .withContext('nothing is shown after the last photo')
      .toBeTrue();
    expect(moveControl(root, promoFile(2), 'Move up').disabled).toBeFalse();

    // A move asked for at an end is not a move: the order is the one the form opened with, photo for photo.
    component.movePhotoUp(1);
    component.movePhotoDown(3);

    expect(heldOrder(component)).toEqual([promoFile(1), promoFile(2), promoFile(3)]);
  });

  it('should number the photos past every index in use when they are moved, so the next upload cannot write over one', () => {
    const { component } = open(threePhotos());

    component.movePhotoUp(3);

    // The photos are 1, 2 and 3, so the move numbers them 4, 5 and 6 in the order on the screen - and the photo that
    // moved is the one whose files are named '_03'. The file the next upload writes is '_07': a name no row points at,
    // whichever order the photos were put in, so an upload can never land on a photo still on the page.
    expect(component.photos.map(photo => photo.imageIndex)).toEqual([4, 5, 6]);
    expect(heldOrder(component)).toEqual([promoFile(1), promoFile(3), promoFile(2)]);
    expect(component.nextImageIndex()).toBe(7);

    // The files are untouched: a move is a change of order and nothing else, so no photo is renamed or rewritten.
    expect(component.form.images.map(row => row.imageUrl).sort()).toEqual(
      [promoFile(1), masterFile(1), promoFile(2), masterFile(2), promoFile(3), masterFile(3)].sort()
    );
  });

  it('should save the photos in the order the form shows them, every size of a photo at its own index', () => {
    const { component } = open(threePhotos());

    // The third photo is walked to the front, one place at a time - the way the shop puts it where it wants it seen.
    component.movePhotoUp(component.photos[2].imageIndex);
    component.movePhotoUp(component.photos[1].imageIndex);

    expect(heldOrder(component)).withContext('the third photo is now shown first').toEqual([
      promoFile(3),
      promoFile(1),
      promoFile(2)
    ]);

    component.form.productName = 'A vase';
    component.form.productTitle = 'A vase';
    component.form.productStatusId = 1;
    component.saveProduct();

    expect(sent).withContext('the product was saved').toBeTruthy();

    const saved = (sent as CreateProductRequest).images;

    // What the storefront will draw: the product page walks the saved rows by image index (see ProductGallery).
    expect(drawnOrder(sent as CreateProductRequest)).toEqual([promoFile(3), promoFile(1), promoFile(2)]);

    // No photo was lost or doubled: three photos, two sizes each.
    expect(saved.length).toBe(6);

    // Every size of a photo shares its index, so the photo shown first is written whole at the lowest index - and the
    // index is past every one the photos came with, so the next upload cannot write over it.
    const firstShown = component.photos[0].imageIndex;
    expect(firstShown).toBe(7);
    expect(saved.filter(image => image.imageIndex === firstShown).map(image => image.imageUrl).sort()).toEqual(
      [promoFile(3), masterFile(3)].sort()
    );
    expect(component.nextImageIndex()).toBe(10);
  });
});
