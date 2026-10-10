import { Component, OnInit } from '@angular/core';
import { AdminService } from '../services/admin.service';
import { AuthService } from '../services/auth.service';
import { PermissionsService } from '../services/permissions.service';
import { DashboardStats, AdminUser, AdminStockByCategory } from '../models/admin.model';

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss'],
  standalone: false
})
export class DashboardComponent implements OnInit {
  stats: DashboardStats = {
    totalProducts: 0,
    totalOrders: 0,
    totalCustomers: 0,
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
  };
  user: AdminUser | null = null;
  isLoading = true;

  /**
   * The 'Stock by category' cards (see 'AdminService.getStockByCategory'): one card per heading of the catalogue,
   * with the categories beneath it and what each of them can still sell - the shelf-by-shelf answer to 'what do we
   * restock', which is a different question from 'how many units does the shop hold' (a product filed under two
   * sub-categories is stock under both, so a card's rows do not add up to its heading).
   *
   * It is read on its own so a slow catalogue read cannot hold up the tiles, and its failure is shown as its own
   * message: 'we could not read the stock' and 'there is no stock' must not look the same on a screen the shop
   * restocks from.
   */
  stockByCategory: AdminStockByCategory | null = null;
  isLoadingStock = true;
  stockError = '';

  /**
   * The settlement pull - the one action on this screen that brings the money tiles up to date, so it is offered
   * only to an admin whose role covers the orders section ('canPullSettlements'): that is what the endpoint checks,
   * because payments and refunds are read and acted on from the order screens.
   */
  canPullSettlements = false;
  isPullingSettlements = false;
  settlementMessage = '';
  settlementError = false;
  /** The day pickers ('YYYY-MM-DD'); both empty is the rolling window the server's own pass reads. */
  settlementFrom = '';
  settlementTo = '';

  /**
   * True when this admin may open the shop's own books ('/finance'). The two money actions on this screen - pulling
   * the gateway's books and reading the period's books - are offered to the same pair the API checks: the orders
   * section, where payments and refunds are acted on, or the finance menu on its own.
   */
  canOpenFinance = false;

  constructor(
    private adminService: AdminService,
    private authService: AuthService,
    private permissionsService: PermissionsService
  ) {}

  ngOnInit(): void {
    this.user = this.authService.getUser();
    this.loadStats();
    this.loadStockByCategory();
    this.offerMoneyActions();
  }

  /**
   * Reads the tiles. 'quiet' leaves the ones on screen while the new figures are fetched, which is what the
   * settlement pull asks for: the message it has just shown belongs to this screen and must not be flashed away
   * by a spinner.
   */
  loadStats(quiet = false): void {
    if (!quiet) {
      this.isLoading = true;
    }

    this.adminService.getDashboardStats().subscribe({
      next: (stats) => {
        this.stats = stats;
        this.isLoading = false;
      },
      error: (err) => {
        console.error('Failed to load dashboard stats:', err);
        this.isLoading = false;
      }
    });
  }

  /**
   * Reads the stock cards. Nothing else on the screen depends on them, so a failure says so in the section itself
   * rather than blanking the dashboard: the figures are what the shop orders against, and an empty screen would
   * read as 'nothing to restock'.
   */
  loadStockByCategory(): void {
    this.isLoadingStock = true;
    this.stockError = '';

    this.adminService.getStockByCategory().subscribe({
      next: (stock) => {
        this.stockByCategory = stock;
        this.isLoadingStock = false;
      },
      error: (err) => {
        console.error('Failed to load stock by category:', err);
        this.isLoadingStock = false;
        this.stockError = 'The stock cards could not be read. Nothing was changed - reload the dashboard to try again.';
      }
    });
  }

  /**
   * '1 product' / '3 products' and '1 unit' / '9 units', so a figure on a card reads as a sentence rather than as
   * a bare number.
   */
  count(value: number, noun: string): string {
    return `${value} ${noun}${value === 1 ? '' : 's'}`;
  }

  /**
   * Decides which money actions this screen may offer, from the same list the sidebar and the route guard read
   * ('PermissionsService' asks the API once and caches the answer, so this is not a second call when the sidebar has
   * already loaded them). A menu list that cannot be read hides the actions rather than showing buttons that can
   * only be refused.
   */
  private offerMoneyActions(): void {
    this.permissionsService.loadMenus().subscribe({
      next: () => {
        this.canPullSettlements = this.permissionsService.canOpenSection('/orders');
        this.canOpenFinance = this.permissionsService.canOpenAnySection('/finance', '/orders');
      },
      error: () => {
        this.canPullSettlements = false;
        this.canOpenFinance = false;
      }
    });
  }

  /**
   * Pulls the gateway's own books over the days named above - or the rolling window when neither is set (see
   * 'AdminService.pullSettlements'). What the pull did is decided on the server and its own wording is shown as it
   * came, including the days it could not read and the findings it made; the tiles are then re-read, because a
   * settled charge written onto a payment row changes the revenue they show.
   */
  pullSettlements(): void {
    if (this.isPullingSettlements) {
      return;
    }

    this.isPullingSettlements = true;
    this.settlementMessage = '';
    this.settlementError = false;

    this.adminService.pullSettlements({
      from: this.settlementFrom || null,
      to: this.settlementTo || null
    }).subscribe({
      next: (result) => {
        this.isPullingSettlements = false;
        this.settlementError = result.result !== 1;
        this.settlementMessage = (result.messages || []).join(' ');
        this.loadStats(true);
      },
      error: (err) => {
        this.isPullingSettlements = false;
        this.settlementError = true;
        this.settlementMessage = err?.status === 403
          ? 'Your admin role does not cover the orders section, so it cannot pull the shop\'s settlement books.'
          : 'The pull could not be made. Nothing was changed - please try again.';
      }
    });
  }

  getInitials(firstName: string, lastName?: string): string {
    const first = firstName ? firstName.charAt(0).toUpperCase() : '';
    const last = lastName ? lastName.charAt(0).toUpperCase() : '';
    return first + last || 'U';
  }
}
