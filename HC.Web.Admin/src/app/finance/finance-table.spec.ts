import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AdminFinanceLine, AdminFinanceSummary } from '../models/admin.model';
import { AdminService as AdminApiService } from '../services/admin.service';
import { FinanceComponent } from './finance.component';

/**
 * The breakdown table at the foot of the finance screen ('What each item carried'). It is a .data-table like every
 * other table in the admin panel, so its columns sit under their headings and it fills the card it is drawn in - the
 * table styles themselves are the centralised ones (src/scss/_tables.scss) plus this component's own stylesheet.
 *
 * The faults these pin down: the table was written with the modal-table modifier alone, and that class is only
 * defined inside two other screens' stylesheets - so nothing but the browser's defaults applied to it, which is the
 * 'not aligned properly' a reader sees. Its total row also wore the div summaries' .total-row (a flex column), which
 * takes a <tr>'s cells out of the table's columns altogether.
 */

/** One line as the API sends it: the per-unit split of the period's money. */
function line(sku: string, fields: Partial<AdminFinanceLine> = {}): AdminFinanceLine {
  return {
    sku,
    units: 1,
    lineValue: 41.3,
    outputGst: 6.3,
    gatewayFee: 1.01,
    gatewayTax: 0.18,
    freightShare: 15,
    chargesSource: 'Payment',
    ...fields
  };
}

/** The books as the finance endpoint sends them - only the fields the template reads are named. */
function books(lines: AdminFinanceLine[]): AdminFinanceSummary {
  return {
    from: new Date('2026-09-01T00:00:00Z'),
    to: new Date('2026-09-30T00:00:00Z'),
    partnerId: null,
    partnerName: null,
    capturedCount: 1,
    grossSales: 41.3,
    refundCount: 0,
    refundsGiven: 0,
    netSales: 41.3,
    gatewayFee: 1.01,
    gatewayTax: 0.18,
    gatewayCharges: 1.19,
    fromTheGateway: 40.11,
    settledChargeCount: 0,
    estimatedChargeCount: 1,
    unknownChargeCount: 0,
    parcelsShipped: 1,
    parcelsWithoutFreight: 0,
    freightCost: 15,
    afterCourier: 25.11,
    declaredMargin: 10,
    declaredCost: 5,
    taxableValue: 35,
    outputGst: 6.3,
    gstOnRefunds: 0,
    gstHeld: 6.3,
    netGstPayable: 6.12,
    settledLines: 0,
    settledCredit: 0,
    settledDebit: 0,
    settledFee: 0,
    settledTax: 0,
    settledOnHold: 0,
    lineItemsWithoutMoney: 0,
    lineItems: lines,
    messages: []
  } as AdminFinanceSummary;
}

describe('FinanceComponent breakdown table', () => {
  /** The screen drawn over the given lines, as the API's answer draws it. */
  function render(lines: AdminFinanceLine[]): HTMLElement {
    TestBed.configureTestingModule({
      imports: [CommonModule, FormsModule],
      declarations: [FinanceComponent],
      providers: [
        {
          provide: AdminApiService,
          useValue: { getFinanceSummary: () => of(books(lines)) }
        }
      ]
    });

    const fixture = TestBed.createComponent(FinanceComponent);
    fixture.detectChanges();

    return fixture.nativeElement as HTMLElement;
  }

  /** The table of the card headed 'What each item carried'. */
  function breakdownTable(root: HTMLElement): HTMLTableElement {
    const table = Array.from(root.querySelectorAll('table')).find(candidate => candidate.querySelector('tfoot'));

    expect(table).withContext('the breakdown table is on the screen').toBeTruthy();

    return table as HTMLTableElement;
  }

  it('should wear the panel\'s own table styles, so its columns line up', () => {
    const table = breakdownTable(render([line('HC-TOTE-LEAF'), line('HC-SCARF-SAND')]));
    const style = getComputedStyle(table);

    // The class is what the fault was about: without it no .data-table rule reaches the table at all.
    expect(table.classList).toContain('data-table');

    // And, with it, the centralised styles really apply: a collapsed table whose header cells carry the padded,
    // upper-cased band - an unstyled table computes to 'separate', '1px' and 'none' here.
    expect(style.borderCollapse).toBe('collapse');
    expect(style.width).not.toBe('auto');

    const headerCell = table.querySelector('thead th') as HTMLTableCellElement;
    expect(getComputedStyle(headerCell).padding).toBe('12px 16px');
    expect(getComputedStyle(headerCell).textTransform).toBe('uppercase');
  });

  it('should keep every row under its own heading', () => {
    const table = breakdownTable(render([line('HC-TOTE-LEAF'), line('HC-SCARF-SAND')]));
    const headings = table.querySelectorAll('thead th').length;
    const rows = table.querySelectorAll('tbody tr');

    expect(headings).toBe(7);
    expect(rows.length).toBe(2);
    expect(Array.from(rows).every(row => row.querySelectorAll('td').length === headings))
      .withContext('every line has a cell under each heading')
      .toBeTrue();
  });

  it('should total in a real table row, under the columns it totals', () => {
    const table = breakdownTable(render([line('HC-TOTE-LEAF'), line('HC-SCARF-SAND')]));
    const totalRow = table.querySelector('tfoot tr') as HTMLTableRowElement;

    // The div summaries' .total-row is a flex column: on a <tr> it takes the row out of the table's columns and
    // stacks the totals one above the other, which is the misalignment this guards.
    expect(getComputedStyle(totalRow).display).toBe('table-row');
    expect(totalRow.querySelectorAll('td').length).toBe(table.querySelectorAll('thead th').length);

    // The total is read as a total without being moved out of the grid.
    const totalCell = totalRow.querySelector('td') as HTMLTableCellElement;
    expect(getComputedStyle(totalCell).fontWeight).toBe('600');
  });

  it('should say a figure nothing has reported in words, in the app\'s own style for it', () => {
    const table = breakdownTable(render([line('HC-TOTE-LEAF', { gatewayFee: null, gatewayTax: null, chargesSource: null })]));
    const unknown = table.querySelector('td .unknown') as HTMLElement;

    expect(unknown).withContext('the row says not known rather than showing a zero').toBeTruthy();
    expect(unknown.textContent).toContain('not known');
    expect(getComputedStyle(unknown).fontStyle).toBe('italic');
  });
});
