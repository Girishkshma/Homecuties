import { CommonModule } from '@angular/common';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { Observable, of, throwError } from 'rxjs';
import { AdminManagedCategory, AdminUser } from '../models/admin.model';
import { AdminService as AdminApiService } from '../services/admin.service';
import { AuthService } from '../services/auth.service';
import { CategoriesComponent } from './categories.component';
import {
  catalogueNote,
  categoryPath,
  categoryType,
  deleteBlockerSentence,
  deleteBlockers,
  parentChoices
} from './catalogue';

/**
 * The Categories screen: the catalogue drawn in the order the server sent it, stepped in by the depth it sent, with the
 * rules about what may be written left to the server (see HC.Business.CategoryCatalog) and only what the screen draws
 * from those rows pinned here - the path a shelf is named by, which rows are headings, what a catalogue that has been
 * left broken looks like, what the form may offer as a parent, and what is said before a delete is asked for.
 */

/** One row of the answer, with the fields a test does not name left at their plainest values. */
function row(
  categoryId: number,
  categoryName: string,
  fields: Partial<AdminManagedCategory> = {}
): AdminManagedCategory {
  return {
    categoryId,
    categoryName,
    parentCategoryId: null,
    parentCategoryName: null,
    depth: 0,
    productCount: 0,
    childCount: 0,
    ...fields
  };
}

/** The shop's own catalogue, in the display order the API answers with (see CategoryTree.RowsInDisplayOrder). */
function catalogue(): AdminManagedCategory[] {
  return [
    row(1, 'Toys', { childCount: 1 }),
    row(14, 'For Kids', { parentCategoryId: 1, parentCategoryName: 'Toys', depth: 1, productCount: 3 }),
    row(2, 'Decoratives', { childCount: 2 }),
    row(6, 'Vases', { parentCategoryId: 2, parentCategoryName: 'Decoratives', depth: 1, productCount: 4 }),
    row(7, 'Pot Houses', { parentCategoryId: 2, parentCategoryName: 'Decoratives', depth: 1, productCount: 2 }),
    row(5, 'Furnitures', { childCount: 1 }),
    row(13, 'Center Tables', { parentCategoryId: 5, parentCategoryName: 'Furnitures', depth: 1, productCount: 1 })
  ];
}

const admin = { userId: 1, loginId: 'girish', firstName: 'Girish', lastName: 'S', isActive: true, roles: [] } as AdminUser;

describe('the catalogue as the screen reads the rows', () => {
  it('should name a shelf by the heading it is read under', () => {
    const rows = catalogue();

    expect(categoryPath(rows[3], rows)).toBe('Decoratives / Vases');

    // A shelf of a shelf carries the whole path, because that is the label the product form puts on it.
    const deeper = [...rows, row(20, 'Glass Vases', { parentCategoryId: 6, depth: 2 })];
    expect(categoryPath(deeper[7], deeper)).toBe('Decoratives / Vases / Glass Vases');
  });

  it('should name a row whose parent is gone on its own, and end a loop rather than run it', () => {
    const orphan = row(99, 'Orphan', { parentCategoryId: 98 });
    const loopOne = row(30, 'Loop One', { parentCategoryId: 31 });
    const loopTwo = row(31, 'Loop Two', { parentCategoryId: 30 });
    const rows = [orphan, loopOne, loopTwo];

    // Nothing above either of them is in the list, so neither is named under a heading it cannot be drawn under.
    expect(categoryPath(orphan, rows)).toBe('Orphan');
    expect(categoryPath(loopOne, rows)).toBe('Loop Two / Loop One');

    // The pair leads back into itself, so the walk stops rather than running: what it read before it stopped is what
    // the label says, and the note on the screen is what tells the shop to repair it.
    expect(categoryPath(loopTwo, rows)).toBe('Loop One / Loop Two');
  });

  it('should call a row with nothing above it in the list a heading', () => {
    const rows = catalogue();

    expect(categoryType(rows[0], rows)).toBe('Heading');
    expect(categoryType(rows[3], rows)).toBe('Shelf');

    // The reading the server takes of a heading (see CategoryTree.TopsOfTree) is the one here: a row whose parent row
    // is not in the list is a heading too, because there is nothing left to file a product under.
    const withOrphan = [...rows, row(99, 'Orphan', { parentCategoryId: 98 })];
    expect(categoryType(withOrphan[7], withOrphan)).toBe('Heading');
  });

  it('should say what is broken about a row that has been left that way, and nothing about the rest', () => {
    const rows = catalogue();

    // The shop's own catalogue has nothing broken in it: no note is drawn under any of its rows.
    rows.forEach(category => expect(catalogueNote(category, rows)).toBe(''));

    const withOrphan = [...rows, row(99, 'Orphan', { parentCategoryId: 98 })];
    expect(catalogueNote(withOrphan[7], withOrphan)).toContain('no longer in the catalogue');

    const loop = [row(30, 'Loop One', { parentCategoryId: 31 }), row(31, 'Loop Two', { parentCategoryId: 30 })];
    expect(catalogueNote(loop[0], loop)).toContain('loop');
    expect(catalogueNote(loop[1], loop)).toContain('loop');
  });

  it('should offer every category as a parent except the row being edited and everything beneath it', () => {
    const rows = catalogue();

    // Adding a category: the whole catalogue is on offer, headings included, because a shelf may sit under any
    // category at any depth.
    expect(parentChoices(rows, null).map(choice => choice.categoryId)).toEqual([1, 14, 2, 6, 7, 5, 13]);

    // Editing 'Decoratives': the row itself and its two shelves are left out - the server refuses a category sitting
    // under itself or under one of its own shelves (see its CategoryCatalog.ParentProblem), and the form cannot offer
    // what would be refused.
    const editable = parentChoices(rows, 2);
    expect(editable.map(choice => choice.categoryId)).toEqual([1, 14, 5, 13]);
    expect(editable.map(choice => choice.label)).toEqual(['Toys', 'Toys / For Kids', 'Furnitures', 'Furnitures / Center Tables']);

    // A row that is one of the catalogue's own shelves leaves its own subtree out and nothing else.
    expect(parentChoices(rows, 14).map(choice => choice.categoryId)).toEqual([1, 2, 6, 7, 5, 13]);
  });

  it('should say what is in the way of a delete rather than let the refusal be a surprise', () => {
    // A shelf with nothing under it and nothing filed on it is a row nothing refers to: nothing is in the way.
    expect(deleteBlockers(row(6, 'Vases'))).toEqual([]);
    expect(deleteBlockerSentence(row(6, 'Vases'))).toBe('');

    expect(deleteBlockerSentence(row(14, 'For Kids', { productCount: 3 })))
      .toBe('The catalogue still holds this: 3 products are filed on it. Move them first.');

    expect(deleteBlockerSentence(row(2, 'Decoratives', { childCount: 2 })))
      .toBe('The catalogue still holds this: 2 categories sit under it. Move them first.');
    expect(deleteBlockerSentence(row(1, 'Toys', { childCount: 1 })))
      .toContain('one category sits under it');

    expect(deleteBlockerSentence(row(6, 'Vases', { productCount: 1 })))
      .toContain('one product is filed on it');

    // A row holding both says both, in the order the shop deals with them: the shelves first, then the products.
    expect(deleteBlockerSentence(row(2, 'Decoratives', { childCount: 2, productCount: 4 })))
      .toBe('The catalogue still holds this: 2 categories sit under it and 4 products are filed on it. Move them first.');
  });
});

describe('CategoriesComponent', () => {
  /** The screen drawn over the given read of the catalogue: the answer, or a failure. */
  function draw(read: Observable<AdminManagedCategory[]>): { fixture: ComponentFixture<CategoriesComponent>; root: HTMLElement } {
    const api = {
      getManagedCategories: () => read,
      createCategory: () => of({ result: 1, messages: ['Category \'Rugs\' created successfully.'] }),
      updateCategory: () => of({ result: 0, messages: ['A category named \'Vases\' already sits there.'] }),
      deleteCategory: () => of({ result: 0, messages: ['2 categories sit under this one. Move them out or delete them first.'] })
    };

    TestBed.configureTestingModule({
      imports: [CommonModule, FormsModule],
      declarations: [CategoriesComponent],
      providers: [
        { provide: AdminApiService, useValue: api },
        { provide: AuthService, useValue: { getUser: () => admin } }
      ]
    });

    const fixture: ComponentFixture<CategoriesComponent> = TestBed.createComponent(CategoriesComponent);
    fixture.detectChanges();

    return { fixture, root: fixture.nativeElement as HTMLElement };
  }

  /** The rows of the catalogue, top to bottom. */
  function rowsOf(root: HTMLElement): HTMLTableRowElement[] {
    return Array.from(root.querySelectorAll('.data-table tbody tr')) as HTMLTableRowElement[];
  }

  /** The row of one category, found the way the shop finds it: by the name written on it. */
  function rowFor(root: HTMLElement, categoryName: string): HTMLTableRowElement {
    const found = rowsOf(root).find(candidate => nameOf(candidate) === categoryName);
    expect(found).withContext(`the row for ${categoryName} is on the screen`).toBeTruthy();

    return found as HTMLTableRowElement;
  }

  function nameOf(categoryRow: HTMLTableRowElement): string {
    return categoryRow.querySelector('.category-name')?.textContent?.trim() ?? '';
  }

  function click(fixture: ComponentFixture<CategoriesComponent>, element: Element | null | undefined): void {
    expect(element).withContext('the button is on the screen').toBeTruthy();
    (element as HTMLElement).click();
    fixture.detectChanges();
  }

  it('should draw the catalogue in the order the server sent it, stepped in by the depth it sent', () => {
    const { root } = draw(of(catalogue()));
    const rows = rowsOf(root);

    expect(rows.map(nameOf)).toEqual(['Toys', 'For Kids', 'Decoratives', 'Vases', 'Pot Houses', 'Furnitures', 'Center Tables']);

    // The key teaches the two words above the list, so a heading and a shelf are read where they are drawn.
    expect(root.querySelector('.catalogue-key')?.textContent).toContain('Heading');
    expect(root.querySelector('.catalogue-key')?.textContent).toContain('Shelf');

    // The indentation is the server's depth and nothing else: a heading flush left, a shelf of one 20px in under it,
    // and the step is spent on the wrapper so the elbow drawn before the name travels in with it (see indentOf).
    const nameCell = (categoryRow: HTMLTableRowElement) => categoryRow.querySelector('.name-wrap') as HTMLElement;
    expect(nameCell(rows[0]).style.paddingLeft).toBe('0px');
    expect(nameCell(rows[1]).style.paddingLeft).toBe('20px');
    expect(nameCell(rows[3]).style.paddingLeft).toBe('20px');

    // The two kinds of row are drawn apart from each other, and a shelf is joined to the heading it is read under by
    // its own elbow: the step in on its own read as a stray indent.
    expect(rowFor(root, 'Decoratives').classList.contains('heading-row')).toBeTrue();
    expect(rowFor(root, 'Vases').classList.contains('shelf-row')).toBeTrue();
    expect(rowFor(root, 'Vases').querySelector('.branch')?.textContent?.trim()).toBe('└');
    expect(rowFor(root, 'Decoratives').querySelector('.branch')).toBeNull();

    // What each row is, and what it sits under, read the way the shop reads the catalogue.
    expect(rowFor(root, 'Decoratives').children[1].textContent?.trim()).toBe('Heading');
    expect(rowFor(root, 'Vases').children[1].textContent?.trim()).toBe('Shelf');
    expect(rowFor(root, 'Vases').children[2].textContent?.trim()).toBe('Decoratives');
    expect(rowFor(root, 'Decoratives').children[2].textContent?.trim()).toBe('—');

    // The two figures the delete is decided by are shown as the server sent them - and a heading says nothing is filed
    // on it rather than showing a 0 that reads as an empty collection (a product is filed on a shelf, never a heading).
    expect(rowFor(root, 'Vases').children[3].textContent?.trim()).toBe('4');
    expect(rowFor(root, 'Decoratives').children[3].textContent?.trim()).toBe('—');
    expect(rowFor(root, 'Decoratives').children[4].textContent?.trim()).toBe('2');
    expect(rowFor(root, 'Vases').children[4].textContent?.trim()).toBe('—');

    // Nothing is drawn about a broken row when nothing is broken.
    expect(root.querySelector('.row-note')).toBeNull();
  });

  it('should say the catalogue could not be read rather than showing it as empty', () => {
    const { root } = draw(throwError(() => ({ status: 500 })));

    expect(root.querySelector('.page-error')?.textContent).toContain('The catalogue could not be read');
    expect(root.querySelector('.data-table')).toBeNull();
    expect(root.querySelector('.empty')).toBeNull();
  });

  it('should say a broken row is broken instead of hiding it', () => {
    const { root } = draw(of([
      row(2, 'Decoratives', { childCount: 1 }),
      row(6, 'Vases', { parentCategoryId: 2, parentCategoryName: 'Decoratives', depth: 1 }),
      row(99, 'Orphan', { parentCategoryId: 98 })
    ]));

    // The row whose parent is gone is drawn as one of the catalogue's own headings, with the reason under its name -
    // an admin cannot repair what the screen has hidden.
    expect(rowFor(root, 'Orphan').children[1].textContent?.trim()).toBe('Heading');
    expect(rowFor(root, 'Orphan').querySelector('.row-note')?.textContent).toContain('no longer in the catalogue');
    expect(rowFor(root, 'Vases').querySelector('.row-note')).toBeNull();
  });

  it('should leave the row being edited and its own shelves out of the parents the form offers', () => {
    const { fixture, root } = draw(of(catalogue()));

    click(fixture, rowFor(root, 'Decoratives').querySelector('.btn-edit'));

    const options = Array.from(root.querySelectorAll('.modal-content select option')).map(option => option.textContent?.trim());
    expect(options).toEqual(['— Nothing: this category is a heading —', 'Toys', 'Toys / For Kids', 'Furnitures', 'Furnitures / Center Tables']);
    expect(root.querySelector('.modal-header h2')?.textContent).toContain('Edit Category');
  });

  it('should add a shelf under the row it was asked from, with that category already chosen', () => {
    const { fixture, root } = draw(of(catalogue()));

    click(fixture, rowFor(root, 'Vases').querySelector('.btn-view'));

    // The heading the shelf will be filed under is already chosen: 'Vases' is row 6, so the form is adding a shelf
    // rather than starting a heading, and the select is drawn with that row selected (the DOM value is written a
    // microtask later by ngModel, so the form's own state is what says it).
    expect(root.querySelector('.modal-header h2')?.textContent).toContain('Add Shelf');
    expect(fixture.componentInstance.categoryForm.parentCategoryId).toBe(6);
    expect(fixture.componentInstance.isAddingShelf).toBeTrue();
  });

  it('should say what is in the way of a delete before the admin asks for one', () => {
    const { fixture, root } = draw(of(catalogue()));

    // 'Decoratives' holds two shelves: the confirmation says so and its delete button cannot be pressed, so the
    // refusal is never a surprise the API has to deliver.
    click(fixture, rowFor(root, 'Decoratives').querySelector('.btn-delete'));

    expect(root.querySelector('.delete-blocker')?.textContent)
      .toBe('The catalogue still holds this: 2 categories sit under it. Move them first.');
    expect((root.querySelector('.btn-danger') as HTMLButtonElement).disabled).toBeTrue();
  });

  it('should ask the API for a delete that has nothing in its way, and show what it answers', () => {
    // One shelf, with nothing under it and nothing filed on it: the delete is asked for, and the API has the last word.
    const { fixture, root } = draw(of([
      row(2, 'Decoratives', { childCount: 1 }),
      row(6, 'Vases', { parentCategoryId: 2, parentCategoryName: 'Decoratives', depth: 1 })
    ]));

    click(fixture, rowFor(root, 'Vases').querySelector('.btn-delete'));

    expect(root.querySelector('.delete-blocker')).toBeNull();
    expect((root.querySelector('.btn-danger') as HTMLButtonElement).disabled).toBeFalse();

    click(fixture, root.querySelector('.modal-content .btn-danger'));

    // The stub refuses the way the API does when the catalogue moved on under the screen, and its reason is shown
    // exactly as it came back.
    expect(root.querySelector('.modal-content .form-message.error')?.textContent)
      .toContain('2 categories sit under this one');
  });

  it('should show a name clash the server refused as its own sentence, with the form still open', () => {
    const { fixture, root } = draw(of(catalogue()));

    click(fixture, rowFor(root, 'Vases').querySelector('.btn-edit'));
    click(fixture, root.querySelector('.modal-content .btn-primary'));

    expect(root.querySelector('.modal-content .form-message.error')?.textContent)
      .toBe('A category named \'Vases\' already sits there.');
    expect(root.querySelector('.modal-content form')).toBeTruthy();
  });
});
