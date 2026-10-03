import { Component, OnInit } from '@angular/core';
import { AdminService } from '../services/admin.service';
import { AdminFinanceSummary } from '../models/admin.model';

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
}
