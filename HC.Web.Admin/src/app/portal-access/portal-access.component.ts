import { Component, OnInit } from '@angular/core';
import { AdminService } from '../services/admin.service';
import {
  AdminPortalAccessPlace,
  AdminPortalAccessReport,
  AdminPortalAccessLogPage
} from '../models/admin.model';

/**
 * Where the shop is being read from, and how often - a location and a count, never a person.
 *
 * The screen reads one thing (see HC.Business.AccessTrackingService): the anonymous page views the storefront
 * records, counted by country, region and city, split by the shop front they came from, and laid out over the
 * period as a frequency. The log underneath is the same rows, so any figure can be checked against what it is
 * made of - and clicking a country, a region or a portal narrows the log to it rather than starting a second
 * report.
 *
 * Nothing here is a customer: no customer id is read, shown or exported, and the only thing that can tell two
 * visits apart is the address they came from - which is also the only way to count visitors.
 *
 * The period boxes work exactly like the Finance screen's: both empty is the month to date, which is what the
 * screen opens on and what 'This month' puts back, and the server answers with the days it actually read. Times
 * are UTC (the rows are), and the screen says so rather than quietly shifting them.
 */
@Component({
  selector: 'app-portal-access',
  templateUrl: './portal-access.component.html',
  styleUrls: ['./portal-access.component.scss'],
  standalone: false
})
export class PortalAccessComponent implements OnInit {
  report: AdminPortalAccessReport | null = null;
  isLoading = true;
  loadError = '';

  log: AdminPortalAccessLogPage | null = null;
  isLogLoading = false;
  logError = '';

  /** The period boxes ('YYYY-MM-DD'). Both empty asks for the month to date. */
  from = '';
  to = '';

  /** '' counts every portal; 'Web' or 'Admin' narrows it. */
  portal = '';

  /** The place the log is narrowed to - set by clicking a row of a roll-up, never typed. */
  country = '';
  region = '';
  city = '';

  page = 1;
  pageSize = 50;

  /** The housekeeping action: how much history to keep when the team clears out the rest. */
  retentionDays = 365;
  isPurging = false;
  purgeMessage = '';
  purgeError = '';

  constructor(private adminService: AdminService) {}

  ngOnInit(): void {
    this.load();
  }

  /** Reads the period's figures and then the first page of the log behind them. */
  load(): void {
    this.isLoading = true;
    this.loadError = '';

    this.adminService.getPortalAccessReport(this.from, this.to, this.portal).subscribe({
      next: (report) => {
        this.report = report;
        this.isLoading = false;
        this.loadLog(1);
      },
      error: (err) => {
        this.isLoading = false;
        this.loadError = err?.status === 403
          ? 'Your admin role does not cover the Portal Access section, so it cannot read where the shop is being read from.'
          : 'The portal access figures could not be read. Nothing was changed - please try again.';
      }
    });
  }

  /** One page of the log for the same period and the same filters. */
  loadLog(page: number = this.page): void {
    if (!this.report) {
      return;
    }

    this.isLogLoading = true;
    this.logError = '';

    this.adminService
      .getPortalAccessLog(
        { from: this.from, to: this.to, portal: this.portal, country: this.country, region: this.region, city: this.city },
        page,
        this.pageSize
      )
      .subscribe({
        next: (log) => {
          this.log = log;
          this.page = log.page;
          this.isLogLoading = false;
        },
        error: () => {
          this.isLogLoading = false;
          this.logError = 'The hits behind these figures could not be read. The figures above are unaffected.';
        }
      });
  }

  /** Back to the month to date, with no portal or place narrowing the log. */
  showThisMonth(): void {
    this.from = '';
    this.to = '';
    this.portal = '';
    this.country = '';
    this.region = '';
    this.city = '';
    this.load();
  }

  /**
   * Narrows the log to one roll-up row - a country, a region, a city or a portal. The row carries the value the
   * hits are stored with, so the click asks for exactly what the figure counted.
   */
  filterLogBy(place: AdminPortalAccessPlace, kind: 'country' | 'region' | 'city' | 'portal'): void {
    const value = place.filter || place.name;

    if (kind === 'country') {
      this.country = value;
      this.region = '';
      this.city = '';
    } else if (kind === 'region') {
      this.region = value;
      this.city = '';
    } else if (kind === 'city') {
      this.city = value;
    } else {
      this.portal = value;
    }

    this.loadLog(1);
  }

  /** Drops the narrowing and re-reads the log of the whole period. */
  clearLogFilters(): void {
    this.country = '';
    this.region = '';
    this.city = '';
    this.portal = '';
    this.loadLog(1);
  }

  changePage(page: number): void {
    if (page < 1 || page > this.pageCount || page === this.page) {
      return;
    }

    this.loadLog(page);
  }

  /**
   * Removes the history older than the days in the box, after asking once. This is the only thing on the screen
   * that writes anything, and it only ever removes rows - the day-to-day tracking needs no permission and no
   * person, which is why nothing else here has a button.
   */
  purge(): void {
    const days = Number(this.retentionDays) || 365;

    if (!confirm(`Remove every portal access older than ${days} day(s)? This cannot be undone.`)) {
      return;
    }

    this.isPurging = true;
    this.purgeMessage = '';
    this.purgeError = '';

    this.adminService.purgePortalAccess(days).subscribe({
      next: (result) => {
        this.isPurging = false;
        this.purgeMessage = result.messages?.[0] || 'The old history was removed.';
        this.load();
      },
      error: () => {
        this.isPurging = false;
        this.purgeError = 'The old history could not be removed. Nothing was changed - please try again.';
      }
    });
  }

  /** True while the boxes are empty, i.e. the report on screen is the month to date. */
  get isDefaultPeriod(): boolean {
    return !this.from && !this.to;
  }

  /** True while the log is narrowed to a place or a portal. */
  get hasLogFilters(): boolean {
    return !!(this.country || this.region || this.city || this.portal);
  }

  get pageCount(): number {
    return this.log?.pageCount ?? 0;
  }

  /** The hits of the busiest slice - what the chart's bars are scaled against. */
  get peakHits(): number {
    const slices = this.report?.frequency ?? [];
    return Math.max(1, ...slices.map(slice => slice.hits));
  }

  /** One bar's height as a percentage of the busiest slice, so the chart fits its box whatever the numbers are. */
  barHeight(hits: number): number {
    return Math.round((hits / this.peakHits) * 100);
  }

  /**
   * The pages the pager offers: at most nine, centred on the page in hand, so a year of hits is still a row of
   * buttons rather than a list of hundreds.
   */
  get pageNumbers(): number[] {
    const count = this.pageCount;

    if (count <= 9) {
      return Array.from({ length: count }, (_, index) => index + 1);
    }

    const first = Math.min(Math.max(1, this.page - 4), count - 8);

    return Array.from({ length: 9 }, (_, index) => first + index);
  }

  /** What the log is narrowed to, in the words of the badge above it. */
  get logScopeText(): string {
    const parts: string[] = [];

    if (this.portal) {
      parts.push(`the ${this.portal} portal`);
    }

    if (this.country) {
      parts.push(this.country);
    }

    if (this.region) {
      parts.push(this.region);
    }

    if (this.city) {
      parts.push(this.city);
    }

    return parts.join(' · ');
  }
}


