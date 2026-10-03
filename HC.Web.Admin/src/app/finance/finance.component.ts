import { Component, OnInit } from '@angular/core';
import { AdminService } from '../services/admin.service';
import { AdminFinanceSummary, AdminFinanceLine } from '../models/admin.model';

/**
 * The shop's own books for a period (see HC.Business.AdminDashboardService.Finance and HC.Business.OrderMoney):
 * what the customers paid, what was given back, what the gateway kept for taking it, what the couriers were paid,
 * and what the shop's own declared margin on the goods was - the numbers the dashboard's revenue tiles only
 * summarise.
 *
 * The screen writes nothing and asks no gateway: it reads the period and shows the answer as it came, including
 * the days it actually covered (the server resolves those) and the findings the figures cannot say by themselves
 * ('messages'). Pulling the gateway's settlement books - the only thing that fills the bank side - stays on the
 * Dashboard, where that action is offered.
 *
 * WHOSE books it shows follows the signed-in admin and not this screen: an admin or a super admin sees the shop's,
 * and a partner sees their own goods' sales alone (see HC.Business.AdminDashboardService.Finance). The answer says
 * whose it is ('partnerName'), which is what the banner at the top of the page reads from - the screen itself asks
 * for no scope, because one it could ask for is one it could get wrong.
 */
@Component({
  selector: 'app-finance',
  templateUrl: './finance.component.html',
  styleUrls: ['./finance.component.scss'],
  standalone: false
})
export class FinanceComponent implements OnInit {
  summary: AdminFinanceSummary | null = null;
  isLoading = true;
  loadError = '';

  /**
   * The period boxes ('YYYY-MM-DD'). Both empty asks for the month to date - the stretch the dashboard's monthly
   * tile reads - which is also what 'This month' puts back.
   */
  from = '';
  to = '';

  constructor(private adminService: AdminService) {}

  ngOnInit(): void {
    this.load();
  }

  /** Reads the books over the days in the boxes (empty boxes ask for whatever period the server resolves). */
  load(): void {
    this.isLoading = true;
    this.loadError = '';

    this.adminService.getFinanceSummary(this.from, this.to).subscribe({
      next: (summary) => {
        this.summary = summary;
        this.isLoading = false;
      },
      error: (err) => {
        this.isLoading = false;
        this.loadError = err?.status === 403
          ? 'Your admin role covers neither the orders nor the finance section, so it cannot read the shop\'s books.'
          : 'The books could not be read. Nothing was changed - please try again.';
      }
    });
  }

  /** Back to the month to date - the period the screen opens on. */
  showThisMonth(): void {
    this.from = '';
    this.to = '';
    this.load();
  }

  /** True while the boxes are empty, i.e. the report on screen is the month to date. */
  get isDefaultPeriod(): boolean {
    return !this.from && !this.to;
  }

  /**
   * Whether these are one partner's books rather than the whole shop's. The server decides it from the signed-in
   * admin, so all this does is report it: a partner's figures are their own goods' share of each order, and without
   * the banner saying so they would read as if they were every order's.
   */
  get isPartnerView(): boolean {
    return !!this.summary?.partnerName;
  }

  /**
   * The share of the period's sales that the shop declared as its margin ('Profit margin %' on each product
   * line). Shown beside the margin amount so the figure can be read as a rate as well as a rupee total.
   */
  get declaredMarginPercent(): number {
    const books = this.summary;
    if (!books || books.grossSales <= 0) {
      return 0;
    }

    return (books.declaredMargin / books.grossSales) * 100;
  }

  /**
   * What the shop was left with after the goods: what the gateway handed over, less the couriers' bills, less what
   * the shop declared the goods cost. This is the screen's last line and the only one that rests on a figure the
   * shop typed itself rather than on a row the system recorded - which is why it is labelled 'as declared'.
   */
  get afterGoods(): number {
    const books = this.summary;
    if (!books) {
      return 0;
    }

    return books.afterCourier - books.declaredCost;
  }

  /**
   * The GST as a share of the gross sales, so the tax inside what the customers paid can be read as a rate as well
   * as a rupee total. Taken against the gross sales because that is what the tax was charged on - the refunds shown
   * beside it are a separate line, and the tax they give back is its own.
   */
  get outputGstPercent(): number {
    const books = this.summary;
    if (!books || books.grossSales <= 0) {
      return 0;
    }

    return (books.outputGst / books.grossSales) * 100;
  }

  /**
   * What the period was really worth to the shop: the waterfall's last line ('left after the goods, as declared')
   * with the tax the period owes the government set aside. The GST was only ever held, never earned, so this is the
   * figure the shop keeps - not the one above it.
   */
  get leftAfterTax(): number {
    const books = this.summary;
    if (!books) {
      return 0;
    }

    return this.afterGoods - books.netGstPayable;
  }

  /**
   * What each SKU of the period's sales carried, money-wise - the recorded per-unit split (see the API's
   * AdminFinanceLine), most valuable first. It is the same money the waterfall above is made of, cut to the lines
   * it was really made on, which is what lets a shop keeper see a thing that is mostly carriage - or one that came
   * back - next to the things beside it.
   */
  get lineItems(): AdminFinanceLine[] {
    return this.summary?.lineItems || [];
  }

  /** The units the breakdown covers - the period's own units, one row per physical unit on the order lines. */
  get lineItemsUnits(): number {
    return this.lineItems.reduce((sum, line) => sum + line.units, 0);
  }

  /** What those units were worth to the customers: the weight every per-line figure was split by. */
  get lineItemsValue(): number {
    return this.lineItems.reduce((sum, line) => sum + line.lineValue, 0);
  }

  /** The output GST of those units - the tax held for the government on them. */
  get lineItemsGst(): number {
    return this.lineItems.reduce((sum, line) => sum + line.outputGst, 0);
  }

  /**
   * The lines' parts of the gateway's charge and the GST on it. A line whose charge nothing has reported counts as
   * nothing, so the total is what IS known - the table marks those rows one by one rather than hiding them in a
   * total.
   */
  get lineItemsFee(): number {
    return this.lineItems.reduce((sum, line) => sum + (line.gatewayFee ?? 0), 0);
  }

  get lineItemsTax(): number {
    return this.lineItems.reduce((sum, line) => sum + (line.gatewayTax ?? 0), 0);
  }

  /** The lines' parts of the couriers' bills - what the parcels that carried them cost. */
  get lineItemsFreight(): number {
    return this.lineItems.reduce((sum, line) => sum + (line.freightShare ?? 0), 0);
  }

  /** How many of those lines have no gateway charge recorded at all, so their part of the fee is 'not known'. */
  get lineItemsWithoutCharge(): number {
    return this.lineItems.filter(line => line.gatewayFee == null && line.gatewayTax == null).length;
  }

  /**
   * Whose word the gateway figures in the breakdown rest on. A single line still carrying the capture's own estimate
   * makes the total an estimate, so the weakest word among the rows is the one said - which is the same rule the
   * server applies per row.
   */
  get lineItemsChargeSourceText(): string {
    const words = this.lineItems.map(line => (line.chargesSource || '').toLowerCase());

    if (words.length === 0 || words.every(word => !word)) {
      return 'nothing recorded yet';
    }

    if (words.some(word => word === 'payment')) {
      return 'the captures themselves (estimates the settlement pull can still correct)';
    }

    return 'the settlement pull - what the bank was actually settled on';
  }
}
