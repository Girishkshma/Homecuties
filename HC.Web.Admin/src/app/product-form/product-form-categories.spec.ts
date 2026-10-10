import { CommonModule } from '@angular/common';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Observable, Subject, of } from 'rxjs';
import {
  AdminCategory,
  AdminUser,
  CreateProductRequest,
  CreateProductResult,
  ProductFormOptions,
  ProductStatusOption
} from '../models/admin.model';
import { AdminService as AdminApiService } from '../services/admin.service';
import { AuthService } from '../services/auth.service';
import { ProductFormComponent } from './product-form.component';

/**
 * The categories of the admin product form, which are the shop's own catalogue read one row at a time.
 *
 * A row of 'Categories' that names no parent is the shop's own category - a heading, named for the shape of the shop -
 * and a row that names one is a shelf beneath a heading, which is where a product is filed. That is the API's reading
 * too: it refuses a product that names a heading (see CategoryTree.TopsOfTree and the product save in
 * AdminDashboardService), so the form's job is to draw a heading as the card over its shelves, to give a tick to the
 * shelves alone, and to drop a heading from the save.
 *
 * What is pinned here is that reading and the three places the form acts on it: the cards it draws, the ticks it draws
 * under them, and the request it sends. 'Nothing is filed under it' is not the same question - it is true of a shelf as
 * well as of a heading, and of a heading the shop has just added before any shelf beneath it exists - and a form that
 * asked it would grey out every shelf, offer ticks on the headings, and so be unable to file a product at all.
 */

/** One of the shop's own categories: a heading, which the API sends with no parent at all. */
function heading(categoryId: number, categoryName: string): AdminCategory {
  return { categoryId, categoryName, parentCategoryId: null };
}

/** One of the categories beneath a heading: a shelf, and the only kind of row a product may be filed under. */
function shelf(categoryId: number, categoryName: string, parent: AdminCategory): AdminCategory {
  return {
    categoryId,
    categoryName,
    parentCategoryId: parent.categoryId,
    parentCategoryName: parent.categoryName
  };
}

const toys = heading(1, 'Toys');
const decoratives = heading(2, 'Decoratives');
const officeStudy = heading(3, 'Office/Study');
const households = heading(4, 'Households');
const furnitures = heading(5, 'Furnitures');

/**
 * The catalogue as the 'Categories' table holds it, in the order the API lists it (see CategoryTree.InDisplayOrder):
 * every heading followed at once by the shelves beneath it, each in the table's own order - which is why 'For Kids' is
 * a child of 'Toys' and still the second row here.
 */
const shopCategories: AdminCategory[] = [
  toys,
  shelf(14, 'For Kids', toys),
  decoratives,
  shelf(6, 'Vases', decoratives),
  shelf(7, 'Pot Houses', decoratives),
  shelf(8, 'Musicians', decoratives),
  shelf(9, 'Show Pieces', decoratives),
  officeStudy,
  shelf(10, 'Pen Stands', officeStudy),
  shelf(11, 'Calendars', officeStudy),
  shelf(15, 'Mobile Stands', officeStudy),
  households,
  shelf(12, 'Coasters', households),
  shelf(16, 'Utilities', households),
  furnitures,
  shelf(13, 'Center Tables', furnitures)
];

const admin = {
  userId: 1,
  loginId: 'girish',
  firstName: 'Girish',
  lastName: 'S',
  isActive: true,
  roles: []
} as AdminUser;

describe('ProductFormComponent categories', () => {
  /** The request the form last sent, as the API would receive it. */
  let sent: CreateProductRequest | null;

  /**
   * The form drawn over the categories the API answered with - or, for a read that has not answered yet, over a read
   * still running. The other lookups are the least the form needs to draw: a status to default to, and no sizes.
   */
  function open(categories: Observable<AdminCategory[]> = of(shopCategories)): {
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
            getProductFormOptions: () => of({
              statuses: [{ productStatusId: 1 } as ProductStatusOption],
              imageTypes: []
            } as ProductFormOptions),
            getCategories: () => categories,
            createProduct: (request: CreateProductRequest) => {
              sent = request;
              return of({ result: 1, messages: ['Product created.'], productId: 9 } as CreateProductResult);
            }
          }
        },
        { provide: AuthService, useValue: { getUser: () => admin } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null } } } },
        { provide: Router, useValue: { navigate: () => Promise.resolve(true) } }
      ]
    });

    const fixture: ComponentFixture<ProductFormComponent> = TestBed.createComponent(ProductFormComponent);
    fixture.detectChanges();

    return { fixture, component: fixture.componentInstance, root: fixture.nativeElement as HTMLElement };
  }

  /** The checkbox row of one category, found the way the admin finds it: by the name written on it. */
  function checkboxRow(root: HTMLElement, label: string): HTMLElement {
    const found = Array.from(root.querySelectorAll('label.checkbox-item')).find(
      candidate => candidate.querySelector('span')?.textContent?.trim() === label
    );

    expect(found).withContext(`the checkbox for ${label} is on the screen`).toBeTruthy();

    return found as HTMLElement;
  }

  /** The tick itself: whether it can be given at all is what tells a heading from a shelf. */
  function tick(row: HTMLElement): HTMLInputElement {
    return row.querySelector('input') as HTMLInputElement;
  }

  /** A card of the form, found the way the admin finds it: by the name written over it. */
  function card(root: HTMLElement, title: string): HTMLElement {
    const found = Array.from(root.querySelectorAll('.category-card')).find(
      candidate => candidate.querySelector('.category-card-head')?.textContent?.trim() === title
    );

    expect(found).withContext(`the card headed ${title} is on the screen`).toBeTruthy();

    return found as HTMLElement;
  }

  /** The headings the form drew a card for, in the order it drew them. */
  function cardTitles(root: HTMLElement): string[] {
    return Array.from(root.querySelectorAll('.category-card-head')).map(head => head.textContent?.trim() as string);
  }

  /** The categories written on the ticks beneath a node: the form's answer to 'what may a product be filed under'. */
  function namesOf(names: NodeListOf<Element>): string[] {
    return Array.from(names).map(name => name.textContent?.trim() as string);
  }

  /** The categories listed inside one card: what that heading holds. */
  function shelves(cardElement: HTMLElement): string[] {
    return namesOf(cardElement.querySelectorAll('label.checkbox-item span'));
  }

  /** Every category the whole screen offers a tick on: the shelves, and nothing else on the form. */
  function tickedCategories(root: HTMLElement): string[] {
    return namesOf(root.querySelectorAll('.category-card label.checkbox-item span'));
  }

  /** The form filled in far enough to be saved: the categories are what the save is being asked about. */
  function fillIn(component: ProductFormComponent, categoryIds: number[]): void {
    component.form.productName = 'A vase';
    component.form.productTitle = 'A vase';
    component.form.productStatusId = 1;
    component.form.categoryIds = categoryIds;
  }

  it('should read the shop\'s own categories as the headings and every category beneath one as a shelf', () => {
    const { component } = open();

    const headings = shopCategories
      .filter(category => component.isTopLevelCategory(category.categoryId))
      .map(category => category.categoryName);
    expect(headings).toEqual(['Toys', 'Decoratives', 'Office/Study', 'Households', 'Furnitures']);

    // The other 11 rows are the shelves the storefront lists, and every one of them has a tick to give.
    const shelves = shopCategories.filter(category => category.parentCategoryId != null);
    expect(shelves.length).toBe(11);
    expect(shelves.filter(category => component.isTopLevelCategory(category.categoryId))).toEqual([]);
    expect(component.isTopLevelCategory(14)).withContext('For Kids, under Toys').toBeFalse();
    expect(component.isTopLevelCategory(12)).withContext('Coasters, under Households').toBeFalse();
  });

  it('should read a heading the shop has just added, with no shelf beneath it yet, as a heading', () => {
    const { component } = open(of([...shopCategories, heading(17, 'Spices')]));

    // 'Nothing is filed under it' would make this row a shelf and offer it a tick the API then refuses.
    expect(component.isTopLevelCategory(17)).toBeTrue();
  });

  it('should read a category whose parent row is not in the list as a heading, the way the API does', () => {
    const { component } = open(of([
      ...shopCategories,
      // A row naming a parent the list no longer holds: there is nothing to file it under, so it is a top of the
      // tree here as it is to TopsOfTree.
      { categoryId: 18, categoryName: 'Trays', parentCategoryId: 99 }
    ]));

    expect(component.isTopLevelCategory(18)).toBeTrue();
  });

  it('should draw one card per one of the shop\'s own categories, holding the shelves beneath it', () => {
    const { root } = open();

    expect(cardTitles(root)).toEqual(['Toys', 'Decoratives', 'Office/Study', 'Households', 'Furnitures']);

    // Each shelf sits in the card of the heading it names, and in no other card.
    expect(shelves(card(root, 'Toys'))).toEqual(['For Kids']);
    expect(shelves(card(root, 'Decoratives'))).toEqual(['Vases', 'Pot Houses', 'Musicians', 'Show Pieces']);
    expect(shelves(card(root, 'Households'))).toEqual(['Coasters', 'Utilities']);
    expect(shelves(card(root, 'Furnitures'))).toEqual(['Center Tables']);
  });

  it('should give a tick to every shelf and to no heading', () => {
    const { root } = open();

    // The headings are the cards, so the ticks on the screen are the shop's 11 shelves, each of them tickable - which
    // is the whole of 'a product may be filed here'.
    expect(tickedCategories(root)).toEqual([
      'For Kids', 'Vases', 'Pot Houses', 'Musicians', 'Show Pieces',
      'Pen Stands', 'Calendars', 'Mobile Stands', 'Coasters', 'Utilities', 'Center Tables'
    ]);

    tickedCategories(root).forEach(name =>
      expect(tick(checkboxRow(root, name)).disabled).withContext(`${name} can hold a product`).toBeFalse()
    );
  });

  it('should draw a heading the shop has just named, with no shelf beneath it yet, as a card with nothing in it', () => {
    const { root } = open(of([...shopCategories, heading(17, 'Spices')]));

    const spices = card(root, 'Spices');
    expect(shelves(spices)).toEqual([]);
    expect(spices.textContent).toContain('Nothing is filed under this category yet');

    // The heading is the card and not a tick: the API would refuse a product naming it.
    expect(tickedCategories(root)).not.toContain('Spices');
  });

  it('should list rows that name each other as parent in one last card, once each, rather than dropping them', () => {
    const { root } = open(of([
      ...shopCategories,
      // Two rows each naming the other as parent: no card above reaches them, and CategoryTree leaves such rows to the
      // end of its own list for the same reason.
      { categoryId: 20, categoryName: 'Wall Art', parentCategoryId: 21 },
      { categoryId: 21, categoryName: 'Wall Clocks', parentCategoryId: 20 }
    ]));

    expect(cardTitles(root)).toEqual([
      'Toys', 'Decoratives', 'Office/Study', 'Households', 'Furnitures', 'Not under a heading'
    ]);

    const last = Array.from(root.querySelectorAll('.category-card')).pop() as HTMLElement;
    expect(shelves(last)).toEqual(['Wall Art', 'Wall Clocks']);
    expect(last.textContent).toContain('name each other as parent');

    // Listed once each: the 11 shelves and the two rows in the loop, and nothing drawn twice.
    expect(root.querySelectorAll('.category-card label.checkbox-item').length).toBe(13);
  });

  it('should leave a heading a product still carries out of the save, and the shelf it sits on in', () => {
    const { component } = open();

    // A product filed before headings were ruled out can still carry one; the shelf beside it is where it belongs.
    fillIn(component, [4, 12]);
    component.saveProduct();

    expect(sent?.categoryIds).withContext('the shelf is saved, the heading is left out').toEqual([12]);
  });

  it('should read no category as a heading while the categories are still being read', () => {
    const { component } = open(new Subject<AdminCategory[]>());

    // The categories and the product's own details are two calls: while the first has not answered, nothing here may
    // read a product's categories as headings and leave them out of the save.
    expect(component.isTopLevelCategory(4)).toBeFalse();

    fillIn(component, [4, 12]);
    component.saveProduct();

    expect(sent?.categoryIds).toEqual([4, 12]);
  });
});
