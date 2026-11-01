import { Component, OnInit } from '@angular/core';
import { AdminPortalAccessPlaces } from '../../models/admin.model';
import { AdminService } from '../../services/admin.service';
import { PermissionsService } from '../../services/permissions.service';

/**
 * The size a place is counted at, as the buttons above the list name it. 'state' is the shop's own word for a
 * region, and the API accepts either ('country', 'state'/'region', 'city').
 */
type PlaceSize = 'country' | 'state' | 'city';

/**
 * The dashboard's 'Visitors by place' tile: where the shop was read from - country, state or city - and how often,
 * for the days the team names.
 *
 * It opens on this week, and it opens on this week without working the week out here: the tile asks for a period
 * with no days in it, and the API answers with the days it read (Monday to today, see
 * HC.Business.AccessTrackingRules.ResolvePlaceDays), which are the days the tile writes over the list. A week that
 * starts on Monday, in UTC, is therefore a rule in one place on the server rather than a second one in this
 * screen - the same reason the frequency chart's bars are labelled by the server.
 *
 * The figures are the Portal Access screen's own roll-up, cut to one list (see
 * HC.Business.AccessTrackingService.Places), so a country's count here is the count it has there. What the tile
 * cannot say - an empty period, the hits nobody could place, a list cut short - comes back as the answer's own
 * 'messages' and is shown as written.
 *
 * The tile is only drawn for an admin whose role covers the Portal Access section: it is a window onto that
 * section, and the API refuses the read to everyone else, so it is hidden rather than drawn and then refused.
 */
@Component({
  selector: 'app-visitors-by-place',
  templateUrl: './visitors-by-place.component.html',
  styleUrls: ['./visitors-by-place.component.scss'],
  standalone: false
})
export class VisitorsByPlaceComponent implements OnInit {
  /** What the list counts: the country, the state inside it, or the city inside that. */
  by: PlaceSize = 'country';

  /**
   * The days typed in the two boxes ('YYYY-MM-DD'). Both empty is the tile's own default - this week - which is
   * also what the 'This week' button returns to.
   */
  from = '';
  to = '';

  /** The answer on screen: the days actually read, the totals, the list and the findings. */
  places: AdminPortalAccessPlaces | null = null;

  isLoading = true;
  loadError = '';

  /** True once the signed-in admin's menus say the Portal Access section is theirs. */
  isAllowed = false;

  /** The last read asked for, so an older answer cannot land on a newer question. */
  private request = 0;

  private static readonly Weekdays = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
  private static readonly Months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

  constructor(
    private adminService: AdminService,
    private permissionsService: PermissionsService
  ) {}

  ngOnInit(): void {
    this.openToRole();
  }

  /**
   * Decides whether this tile is this admin's at all, from the same menu list the sidebar and the route guard
   * read ('PermissionsService' asks the API once and caches the answer, so this is not a second call when the
   * sidebar has already loaded them). A menu list that cannot be read hides the tile rather than showing a card
   * that can only be refused.
   */
  private openToRole(): void {
    this.permissionsService.loadMenus().subscribe({
      next: () => {
        this.isAllowed = this.permissionsService.canOpenSection('/portal-access');
        this.isLoading = this.isAllowed;

        if (this.isAllowed) {
          this.load();
        }
      },
      error: () => {
        this.isAllowed = false;
        this.isLoading = false;
      }
    });
  }

  /** Reads the list for the size and the days on screen. */
  load(): void {
    this.isLoading = true;
    this.loadError = '';

    const request = ++this.request;

    this.adminService.getPortalAccessPlaces(this.by, this.from, this.to).subscribe({
      next: places => {
        // A slower answer to an older question is dropped: the buttons and the day boxes can be used faster than
        // the API answers, and the list on screen must always belong to the last thing asked.
        if (request !== this.request) {
          return;
        }

        this.places = places;
        this.isLoading = false;
      },
      error: err => {
        if (request !== this.request) {
          return;
        }

        this.isLoading = false;
        this.loadError = err?.status === 403
          ? 'Your admin role does not cover the Portal Access section, so it cannot read where the shop is being read from.'
          : 'Where the shop was read from could not be read. Nothing was changed - reload the dashboard to try again.';
      }
    });
  }

  /** Counts the same days at another size (the country, the state or the city). */
  showBy(size: PlaceSize): void {
    if (size === this.by) {
      return;
    }

    this.by = size;
    this.load();
  }

  /** Back to this week: the boxes are emptied and the API reads its own Monday to today. */
  showThisWeek(): void {
    this.from = '';
    this.to = '';
    this.load();
  }

  /** Re-reads when a day box is changed by hand - one day on its own is still a period. */
  applyDays(): void {
    this.load();
  }

  /** True while nothing has been typed, i.e. the list on screen is the server's own this-week window. */
  get isThisWeek(): boolean {
    return !this.from && !this.to;
  }

  /** The first day the answer covers ('YYYY-MM-DD'), taken from the answer itself. */
  get readFrom(): string {
    return VisitorsByPlaceComponent.dayOf(this.places?.from);
  }

  /** The last day the answer covers ('YYYY-MM-DD'), taken from the answer itself. */
  get readTo(): string {
    return VisitorsByPlaceComponent.dayOf(this.places?.to);
  }

  /**
   * True when the days read are not the two that were typed - a period wider than a year is trimmed at its far
   * end, and two days the wrong way round are read as the one day they name. The tile says so rather than
   * showing a quieter answer under the dates that were asked for.
   */
  get daysAdjusted(): boolean {
    if (!this.places || !this.from || !this.to) {
      return false;
    }

    return this.from !== this.readFrom || this.to !== this.readTo;
  }

  /**
   * What an empty list says. A period with no hits at all is a different finding from one whose hits had no
   * place (a private address, a lookup with no answer), and the tile words them differently.
   */
  get emptyText(): string {
    return `No ${this.by} could be placed in this period - the hits are in the totals above and in none of the rows.`;
  }

  /**
   * The findings to show under the list. A period with no hits says so in the list's place (the answer's first
   * message), so that message is not repeated underneath it.
   */
  get notes(): string[] {
    if (!this.places || !this.places.totalHits) {
      return [];
    }

    return this.places.messages ?? [];
  }

  /** True when there were hits but none of them could be grouped into a place. */
  get isUnplacedPeriod(): boolean {
    return !!this.places && this.places.totalHits > 0 && !this.places.places.length;
  }

  /**
   * A day as the tile writes it ('Mon, 26 Oct 2026'), read from the day itself rather than through a timezone:
   * the API's days are UTC days, and a formatter that shifted them into local time would name the day before for
   * half the world.
   */
  dayLabel(day: string): string {
    const [year, month, date] = (day ?? '').split('-').map(Number);

    if (!year || !month || !date) {
      return day ?? '';
    }

    const at = new Date(Date.UTC(year, month - 1, date));
    const padded = String(date).padStart(2, '0');

    return `${VisitorsByPlaceComponent.Weekdays[at.getUTCDay()]}, ${padded} ${VisitorsByPlaceComponent.Months[at.getUTCMonth()]} ${year}`;
  }

  /** The day part of an answer's date ('2026-10-26T00:00:00Z' -> '2026-10-26'), or nothing when there is none. */
  private static dayOf(value?: string | null): string {
    return (value ?? '').substring(0, 10);
  }
}
