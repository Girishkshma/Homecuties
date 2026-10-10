import { CommonModule } from '@angular/common';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { Observable, Subject, of, throwError } from 'rxjs';
import {
  AdminCategoryStock,
  AdminCategoryStockLine,
  AdminStockByCategory,
  AdminUser,
  DashboardStats
} from '../models/admin.model';
import { AdminService as AdminApiService } from '../services/admin.service';
import { AuthService } from '../services/auth.service';
import { PermissionsService } from '../services/permissions.service';
import { DashboardComponent } from './dashboard.component';

/**
 * The 'Stock by category' section of the dashboard: one card per heading of the catalogue, with the categories
 * beneath it and what each of them can still sell. It is the screen the shop restocks from, so the figures have to
 * be read as the server sent them and the section has to be honest about its own state.
 *
 * The rules themselves live on the server (see HC.Business.CategoryStock and its tests); what is pinned here is that
 * the screen paints the answer rather than working figures out again: a card's heading is shown with its own numbers
 * even though they are not the sum of its rows (adding the rows up would report stock the shop does not hold), the
 * two marks - none left, and the low mark the answer itself carries - are the only two figures that are shouted, and
 * 'the read failed', 'the read is still running' and 'there is nothing to count' are three different things on a
 * screen someone orders stock from.
 */

/** One row of a card: a category beneath a heading, and what it and everything below it can still sell. */
function line(
  categoryId: number,
  categoryName: string,
  fields: Partial<AdminCategoryStockLine> = {}
): AdminCategoryStockLine {
  return {
    categoryId,
    categoryName,
    depth: 0,
    productCount: 1,
    unitsInStock: 4,
    outOfStockProducts: 0,
    lowStockProducts: 0,
    ...fields
  };
}

/** One card: a heading and the categories beneath it. */
function card(
  categoryId: number,
  categoryName: string,
  fields: Partial<AdminCategoryStock> = {}
): AdminCategoryStock {
  return {
    categoryId,
    categoryName,
    productCount: 1,
    unitsInStock: 4,
    outOfStockProducts: 0,
    lowStockProducts: 0,
    productsOnTheHeading: 0,
    subCategories: [],
    ...fields
  };
}

/** The answer the stock endpoint sends, as the screen reads it. */
function cards(
  headings: AdminCategoryStock[],
  fields: Partial<AdminStockByCategory> = {}
): AdminStockByCategory {
  return { headings, lowStockUnits: 3, messages: [], ...fields };
}

/** The tiles above the cards, named only where a test looks at them. */
const tiles = {
  totalProducts: 41,
  totalOrders: 7,
  totalCustomers: 3,
  totalPartners: 0,
  totalVendors: 0,
  pendingOrders: 0,
  todayRevenue: 0,
  monthlyRevenue: 0,
  cancelledOrders: 0,
  ordersToDispatch: 0,
  shipmentsInProgress: 0,
  outForDelivery: 0,
  deliveredOrders: 0,
  shipmentsNeedingAttention: 0,
  refundsDue: 0,
  returnsAwaitingDecision: 0,
  returnsComingBack: 0,
  returnsReceived: 0,
  returnedOrders: 0
} as DashboardStats;

const admin = { userId: 1, loginId: 'girish', firstName: 'Girish', lastName: 'S', isActive: true, roles: [] } as AdminUser;

describe('DashboardComponent stock by category', () => {
  /** The screen drawn over the given read of the stock: the answer, a failure, or a read still running. */
  function draw(read: Observable<AdminStockByCategory>): { fixture: ComponentFixture<DashboardComponent>; root: HTMLElement } {
    TestBed.configureTestingModule({
      imports: [CommonModule, FormsModule, RouterModule.forRoot([])],
      declarations: [DashboardComponent],
      providers: [
        {
          provide: AdminApiService,
          useValue: { getDashboardStats: () => of(tiles), getStockByCategory: () => read }
        },
        { provide: AuthService, useValue: { getUser: () => admin } },
        {
          provide: PermissionsService,
          useValue: { loadMenus: () => of([]), canOpenSection: () => false, canOpenAnySection: () => false }
        }
      ]
    });

    const fixture: ComponentFixture<DashboardComponent> = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();

    return { fixture, root: fixture.nativeElement as HTMLElement };
  }

  /** The drawn screen of a read that has already landed. */
  function render(read: Observable<AdminStockByCategory>): HTMLElement {
    return draw(read).root;
  }

  /** The 'Stock by category' section itself: its cards, its reading line, its failure and its footnotes live in it. */
  function section(root: HTMLElement): HTMLElement {
    const found = Array.from(root.querySelectorAll('.stats-section')).find(
      candidate => candidate.querySelector('.section-title')?.textContent?.trim() === 'Stock by category'
    );

    expect(found).withContext('the stock section is on the screen').toBeTruthy();

    return found as HTMLElement;
  }

  /** The card of one heading, found the way the shop finds it: by the heading written on it. */
  function cardFor(root: HTMLElement, heading: string): HTMLElement {
    const found = Array.from(root.querySelectorAll('.stock-card')).find(
      candidate => candidate.querySelector('h3')?.textContent?.trim() === heading
    );

    expect(found).withContext(`the card for ${heading} is on the screen`).toBeTruthy();

    return found as HTMLElement;
  }

  /** The rows of a card's table, top to bottom - a category and the ones stepped in under it. */
  function rowsOf(card: HTMLElement): HTMLTableRowElement[] {
    return Array.from(card.querySelectorAll('.stock-table tbody tr')) as HTMLTableRowElement[];
  }

  /** The figures of one row, read left to right from the columns headed 'Products' on. */
  function figuresOf(row: HTMLTableRowElement): number[] {
    return Array.from(row.querySelectorAll('td.num')).map(cell => Number(cell.textContent?.trim()));
  }

  it('should draw one card per heading, with the heading and its own figures at the top', () => {
    const root = render(of(cards([
      card(1, 'Toys', {
        productCount: 5,
        unitsInStock: 12,
        outOfStockProducts: 1,
        lowStockProducts: 2,
        subCategories: [line(14, 'For Kids')]
      }),
      card(2, 'Decoratives', { productCount: 2, unitsInStock: 6 })
    ])));

    expect(root.querySelectorAll('.stock-card').length).toBe(2);

    const toys = cardFor(root, 'Toys');
    expect(toys.querySelector('.stock-card-total')?.textContent).toContain('5 products');
    expect(toys.querySelector('.stock-card-total')?.textContent).toContain('12 units in stock');

    const decoratives = cardFor(root, 'Decoratives');
    expect(decoratives.querySelector('.stock-card-total')?.textContent).toContain('2 products');
  });

  it('should show a heading with its own figures rather than add its rows up', () => {
    // The shop's own shape: the same toy is filed under 'For Kids' and under 'Plush' below it, so the rows come to
    // five products between them where the shop holds three. Adding the rows up would order stock the shop has not
    // sold, which is why the heading is painted as the answer sent it.
    const root = render(of(cards([
      card(1, 'Toys', {
        productCount: 3,
        unitsInStock: 7,
        outOfStockProducts: 1,
        lowStockProducts: 1,
        subCategories: [
          line(14, 'For Kids', { productCount: 2, unitsInStock: 5, outOfStockProducts: 1 }),
          line(17, 'Plush', { depth: 1, productCount: 2, unitsInStock: 5, outOfStockProducts: 1 }),
          line(15, 'Learning', { productCount: 1, unitsInStock: 2, lowStockProducts: 1 })
        ]
      })
    ])));

    const toys = cardFor(root, 'Toys');
    expect(toys.querySelector('.stock-card-total')?.textContent).toContain('3 products');
    expect(toys.querySelector('.stock-card-total')?.textContent).toContain('7 units in stock');

    const rows = rowsOf(toys);
    expect(rows.length).toBe(3);
    expect(rows.map(row => figuresOf(row))).toEqual([[2, 5, 1, 0], [2, 5, 1, 0], [1, 2, 0, 1]]);

    // The rows really do not come to the heading, and the card says so rather than leaving the two figures to be
    // reconciled by the reader.
    const products = rows.reduce((sum, row) => sum + figuresOf(row)[0], 0);
    const units = rows.reduce((sum, row) => sum + figuresOf(row)[1], 0);
    expect(products).toBe(5);
    expect(units).toBe(12);

    expect(section(root).querySelector('.hint')?.textContent).toContain('rows do not add up');
  });

  it('should step each category in under the one it sits under', () => {
    const root = render(of(cards([
      card(1, 'Toys', {
        subCategories: [line(14, 'For Kids'), line(17, 'Plush', { depth: 1 }), line(15, 'Learning')]
      })
    ])));

    const rows = rowsOf(cardFor(root, 'Toys'));

    expect(rows.map(row => row.querySelector('.stock-category')?.textContent?.trim()))
      .toEqual(['For Kids', 'Plush', 'Learning']);

    // The depth the answer carries is the whole of the indentation: a category of a category is stepped in under it,
    // and two categories at the same depth line up with each other.
    const lefts = rows.map(row => getComputedStyle(row.querySelectorAll('td')[0]).paddingLeft);
    expect(lefts).toEqual(['12px', '30px', '12px']);
  });

  it('should shout only the two figures the ordering is decided from', () => {
    const root = render(of(cards([
      card(1, 'Toys', {
        productCount: 3,
        unitsInStock: 7,
        outOfStockProducts: 2,
        lowStockProducts: 1,
        subCategories: [
          line(14, 'For Kids', { outOfStockProducts: 2 }),
          line(17, 'Plush', { depth: 1, lowStockProducts: 3 }),
          line(15, 'Learning')
        ]
      }),
      card(2, 'Decoratives', { unitsInStock: 0 })
    ])));

    const rows = rowsOf(cardFor(root, 'Toys'));

    const out = rows[0].querySelectorAll('td.num')[2] as HTMLElement;
    expect(out.classList).toContain('danger');
    expect(getComputedStyle(out).color).toBe('rgb(192, 57, 43)');

    // A zero is a fact and not an alarm: 'nothing has run out here' is drawn quietly, not in red.
    const low = rows[0].querySelectorAll('td.num')[3] as HTMLElement;
    expect(low.classList).not.toContain('warn');

    const lowBelow = rows[1].querySelectorAll('td.num')[3] as HTMLElement;
    expect(lowBelow.classList).toContain('warn');
    expect(getComputedStyle(lowBelow).color).toBe('rgb(185, 119, 14)');
    expect(rows[1].querySelectorAll('td.num')[2].classList).not.toContain('danger');

    // Both flags are on every card, so all of them read the same way; a flag at zero is the quiet one.
    const loud = cardFor(root, 'Toys');
    expect(Array.from(loud.querySelectorAll('.stock-flag')).every(flag => !flag.classList.contains('quiet'))).toBeTrue();
    expect(loud.querySelector('.stock-flag.out')?.textContent).toContain('2 out of stock');

    const quiet = cardFor(root, 'Decoratives');
    expect(quiet.querySelectorAll('.stock-flag').length).toBe(2);
    expect(Array.from(quiet.querySelectorAll('.stock-flag')).every(flag => flag.classList.contains('quiet'))).toBeTrue();
    expect(getComputedStyle(quiet.querySelector('.stock-flag') as HTMLElement).color).toBe('rgb(154, 160, 166)');
  });

  it('should say what the figures cannot: stock no card reaches, and what is filed right on a heading', () => {
    const root = render(of(cards(
      [
        card(1, 'Toys', { productsOnTheHeading: 2, subCategories: [line(14, 'For Kids')] }),
        card(2, 'Decoratives')
      ],
      {
        messages: [
          'Nine units of stock are filed under no category the catalogue can reach, so no card above counts them.',
          'Four units belong to products the shop has suspended.'
        ]
      }
    )));

    const notes = Array.from(section(root).querySelectorAll('.stock-notes p'))
      .map(note => note.textContent?.trim());
    expect(notes.length).toBe(2);
    expect(notes[0]).toContain('no category the catalogue can reach');
    expect(notes[1]).toContain('suspended');

    // The products of older days are in the figures above and in no row below, so the card says how many they are
    // rather than leaving the figures unexplained.
    const toys = cardFor(root, 'Toys');
    expect(toys.querySelector('.stock-on-heading')?.textContent).toContain('2 products filed right on the heading');
    expect(rowsOf(toys).length).toBe(1);
  });

  it('should say nothing about what is not there', () => {
    const root = render(of(cards([card(1, 'Toys'), card(2, 'Decoratives')])));

    // No stock is off the cards, and none is filed on a heading: neither note is drawn, rather than drawn empty.
    expect(section(root).querySelector('.stock-notes')).toBeNull();
    expect(root.querySelector('.stock-on-heading')).toBeNull();
  });

  it('should say the catalogue has no headings rather than draw a card of zeroes', () => {
    const root = render(of(cards([])));
    const stock = section(root);

    expect(root.querySelectorAll('.stock-card').length).toBe(0);
    expect(stock.querySelector('.stock-empty')?.textContent).toContain('No headings in the catalogue yet');
    expect(stock.querySelector('.hint')).toBeNull();
  });

  it('should fail inside its own section, with the tiles still on the screen', () => {
    spyOn(console, 'error');

    const root = render(throwError(() => ({ status: 500 })));
    const stock = section(root);

    expect(stock.querySelector('.form-message.error')?.textContent).toContain('The stock cards could not be read');

    // Nothing is invented to fill the gap: the section says it could not read, and no card is drawn - an empty card
    // here would read as 'nothing to restock', which is the one thing a failed read must never look like.
    expect(root.querySelectorAll('.stock-card').length).toBe(0);
    expect(stock.querySelector('.stock-empty')).toBeNull();

    // The tiles are a separate read of their own, and the failure of this one leaves them alone.
    expect(root.textContent).toContain('Total Products');
  });

  it('should say it is reading the catalogue while the read runs, and draw the cards when it lands', () => {
    const reading = new Subject<AdminStockByCategory>();
    const { fixture, root } = draw(reading);

    expect(section(root).querySelector('.loading')?.textContent).toContain("Reading the shop's stock");
    expect(root.querySelectorAll('.stock-card').length).toBe(0);

    // The tiles are read separately and are already on screen while the stock is still being counted.
    expect(root.textContent).toContain('Total Products');

    reading.next(cards([card(1, 'Toys', { productCount: 4, unitsInStock: 9 })]));
    reading.complete();
    fixture.detectChanges();

    expect(section(root).querySelector('.loading')).toBeNull();
    expect(cardFor(root, 'Toys').querySelector('.stock-card-total')?.textContent).toContain('9 units in stock');
  });
});
