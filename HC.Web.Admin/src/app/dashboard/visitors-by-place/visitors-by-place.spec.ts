import { CommonModule } from '@angular/common';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { AdminPortalAccessPlace, AdminPortalAccessPlaces } from '../../models/admin.model';
import { AdminService } from '../../services/admin.service';
import { PermissionsService } from '../../services/permissions.service';
import { VisitorsByPlaceComponent } from './visitors-by-place.component';

/**
 * The dashboard's 'Visitors by place' tile: where the shop was being read from - country, state or city - for the
 * days the team names, and this week when they name none.
 *
 * The week's own rule lives on the server (Monday to today, see HC.Business.AccessTrackingRules.ResolvePlaceDays and
 * its tests); what is pinned here is that the tile asks for that rule rather than inventing a second one - the first
 * read carries no days at all and the days written over the list are the ones the answer names - and that the size
 * switch, the typed days, the empty period and the role that may not read it are each shown as what they are.
 */

/** One roll-up row: a place, what was seen there, and its part of the period. */
function place(
  name: string,
  hits: number,
  visitors: number,
  share: number,
  countryCode: string | null = null
): AdminPortalAccessPlace {
  return { name, filter: name, countryCode, hits, visitors, share };
}

/** An answer to the tile's read, in the shape the API sends it. */
function answer(overrides: Partial<AdminPortalAccessPlaces> = {}): AdminPortalAccessPlaces {
  return {
    from: '2026-10-26T00:00:00Z',
    to: '2026-10-28T00:00:00Z',
    dimension: 'country',
    title: 'Countries',
    totalHits: 30,
    uniqueVisitors: 12,
    locatedHits: 30,
    unlocatedHits: 0,
    placeCount: 2,
    places: [place('India', 24, 10, 80, 'IN'), place('United Kingdom', 6, 2, 20, 'GB')],
    messages: [],
    ...overrides
  };
}

describe('VisitorsByPlaceComponent', () => {
  /** What the tile is drawn over: the stub that will answer its reads. */
  interface Api {
    getPortalAccessPlaces: jasmine.Spy;
  }

  /** The tile drawn over the given reads - and, by default, for an admin whose role covers Portal Access. */
  function draw(
    api: Api,
    mayRead: boolean = true
  ): { fixture: ComponentFixture<VisitorsByPlaceComponent>; tile: VisitorsByPlaceComponent; root: HTMLElement } {
    TestBed.configureTestingModule({
      imports: [CommonModule, FormsModule, RouterModule.forRoot([])],
      declarations: [VisitorsByPlaceComponent],
      providers: [
        { provide: AdminService, useValue: api },
        {
          provide: PermissionsService,
          useValue: { loadMenus: () => of([]), canOpenSection: () => mayRead }
        }
      ]
    });

    const fixture: ComponentFixture<VisitorsByPlaceComponent> = TestBed.createComponent(VisitorsByPlaceComponent);
    fixture.detectChanges();

    return { fixture, tile: fixture.componentInstance, root: fixture.nativeElement as HTMLElement };
  }

  /** A stub that answers every read with the same answer. */
  function answering(places: AdminPortalAccessPlaces = answer()): Api {
    return { getPortalAccessPlaces: jasmine.createSpy('getPortalAccessPlaces').and.returnValue(of(places)) };
  }

  /** The rows of the list, top to bottom. */
  function rowsOf(root: HTMLElement): HTMLTableRowElement[] {
    return Array.from(root.querySelectorAll('.place-table tbody tr')) as HTMLTableRowElement[];
  }

  /** The line under the heading: which places, which days, and whether those days are this week. */
  function captionOf(root: HTMLElement): string {
    return (root.querySelector('.section-note')?.textContent ?? '').replace(/\s+/g, ' ').trim();
  }

  it('should open on this week by asking for the countries with no days at all', () => {
    // The week is the server's rule: the tile asks a question with no period in it and writes the days it is
    // answered with. Sending a Monday worked out here would be a second rule free to drift from the first.
    const api = answering();
    const { root } = draw(api);

    expect(api.getPortalAccessPlaces).toHaveBeenCalledWith('country', '', '');
    expect(captionOf(root)).toContain('Countries');
    expect(captionOf(root)).toContain('Mon, 26 Oct 2026 to Wed, 28 Oct 2026');
    expect(captionOf(root)).toContain('(this week)');
    expect(root.querySelector('.tile-reset')).toBeNull();
  });

  it('should draw each place with its own figures and its share of the period', () => {
    const { root } = draw(answering());
    const rows = rowsOf(root);

    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('India');
    expect(rows[0].textContent).toContain('IN');
    expect(rows[0].textContent).toContain('24');
    expect(rows[0].textContent).toContain('80%');
    expect(rows[1].textContent).toContain('United Kingdom');
    expect(rows[1].querySelector('.share-bar')).toBeTruthy();
  });

  it('should count the same days at the size the button names', () => {
    const api = answering();
    const { fixture, root } = draw(api);

    const city = Array.from(root.querySelectorAll<HTMLButtonElement>('.size-btn')).find(
      button => button.textContent?.trim() === 'City'
    );

    expect(city).withContext('the City button is on the tile').toBeTruthy();
    city?.click();
    fixture.detectChanges();

    expect(api.getPortalAccessPlaces).toHaveBeenCalledWith('city', '', '');
    expect(city?.getAttribute('aria-pressed')).toBe('true');
  });

  it('should read the days typed in the boxes, and stop calling them this week', () => {
    const api = answering();
    const { fixture, tile, root } = draw(api);

    tile.from = '2026-09-01';
    tile.to = '2026-09-30';
    tile.applyDays();
    fixture.detectChanges();

    expect(api.getPortalAccessPlaces).toHaveBeenCalledWith('country', '2026-09-01', '2026-09-30');
    expect(captionOf(root)).not.toContain('(this week)');
  });

  it('should return to this week when the boxes are cleared', () => {
    const api = answering();
    const { fixture, tile, root } = draw(api);

    tile.from = '2026-09-01';
    tile.to = '2026-09-30';
    tile.applyDays();
    fixture.detectChanges();

    const reset = root.querySelector<HTMLButtonElement>('.tile-reset');
    expect(reset).withContext('a typed period can be cleared').toBeTruthy();
    reset?.click();
    fixture.detectChanges();

    expect(api.getPortalAccessPlaces).toHaveBeenCalledWith('country', '', '');
    expect(captionOf(root)).toContain('(this week)');
  });

  it('should say so when the days it read are not the days that were asked for', () => {
    // A reversed pair is read as the one day it names; the tile shows the day it read rather than a quieter answer
    // under the dates that were typed.
    const api = answering(answer({ from: '2026-10-10T00:00:00Z', to: '2026-10-10T00:00:00Z' }));
    const { fixture, tile, root } = draw(api);

    tile.from = '2026-10-20';
    tile.to = '2026-10-10';
    tile.applyDays();
    fixture.detectChanges();

    const notes = Array.from(root.querySelectorAll('.tile-note')).map(note => note.textContent ?? '').join(' ');
    expect(notes).toContain('The days read are Sat, 10 Oct 2026 to Sat, 10 Oct 2026');
  });

  it('should not be drawn at all for an admin whose role does not cover Portal Access', () => {
    // The tile is a window onto that section and the API refuses the read to everyone else, so it is hidden before
    // it is drawn rather than drawn and then refused - and nothing is read for a screen that is not there.
    const api = answering();
    const { root } = draw(api, false);

    expect(root.querySelector('.place-tile')).toBeNull();
    expect(api.getPortalAccessPlaces).not.toHaveBeenCalled();
  });

  it('should say a period had nothing in it rather than draw an empty list of places', () => {
    const api = answering(
      answer({
        totalHits: 0,
        uniqueVisitors: 0,
        locatedHits: 0,
        placeCount: 0,
        places: [],
        messages: ['Nothing was recorded in this period yet.']
      })
    );
    const { root } = draw(api);

    expect(rowsOf(root).length).toBe(0);
    expect(root.querySelector('.place-table')).toBeNull();
    expect(root.querySelector('.tile-state')?.textContent).toContain('Nothing was recorded in this period yet.');
  });

  it('should draw a period whose hits had no place, and say the rest in the answer own words', () => {
    // A private address is in the totals and in none of the rows: the tile shows the totals, an empty list that
    // says why it is empty, and the API's own finding underneath.
    const api = answering(
      answer({
        totalHits: 4,
        uniqueVisitors: 2,
        locatedHits: 0,
        unlocatedHits: 4,
        placeCount: 0,
        places: [],
        messages: ['4 of the 4 hit(s) have no place: the address was private (a local or office network) or the location lookup had no answer for it, so they are in the totals above and in none of the rows.']
      })
    );
    const { root } = draw(api);

    expect(rowsOf(root).length).toBe(1);
    expect(root.querySelector('.tile-empty-cell')?.textContent).toContain('No country could be placed in this period');
    expect(root.querySelector('.tile-totals')?.textContent).toContain('4 hits');
    expect(root.querySelector('.tile-note')?.textContent).toContain('4 of the 4 hit(s) have no place');
  });

  it('should say the read failed without pretending the period was empty', () => {
    const api: Api = {
      getPortalAccessPlaces: jasmine
        .createSpy('getPortalAccessPlaces')
        .and.returnValue(throwError(() => ({ status: 500 })))
    };
    const { root } = draw(api);

    expect(root.querySelector('.form-message.error')?.textContent).toContain('could not be read');
    expect(root.querySelector('.tile-state')?.textContent ?? '').not.toContain('Nothing was recorded');
  });

  it('should name the role when the API refuses the read', () => {
    const api: Api = {
      getPortalAccessPlaces: jasmine
        .createSpy('getPortalAccessPlaces')
        .and.returnValue(throwError(() => ({ status: 403 })))
    };
    const { root } = draw(api);

    expect(root.querySelector('.form-message.error')?.textContent).toContain('does not cover the Portal Access section');
  });

  it('should keep the newest answer when two reads overlap', () => {
    // The buttons can be used faster than the API answers: the slower answer to the older question must not land on
    // the list that belongs to the newer one.
    const countries = new Subject<AdminPortalAccessPlaces>();
    const cities = new Subject<AdminPortalAccessPlaces>();
    const api: Api = {
      getPortalAccessPlaces: jasmine
        .createSpy('getPortalAccessPlaces')
        .and.returnValues(countries.asObservable(), cities.asObservable())
    };

    const { fixture, tile, root } = draw(api);
    tile.showBy('city');
    fixture.detectChanges();

    cities.next(answer({ dimension: 'city', title: 'Cities', places: [place('Chennai, Tamil Nadu', 30, 12, 100)] }));
    countries.next(answer());
    fixture.detectChanges();

    const rows = rowsOf(root);
    expect(rows.length).toBe(1);
    expect(rows[0].textContent).toContain('Chennai, Tamil Nadu');
  });

  it('should write a day from the day itself, never through a timezone', () => {
    // The API's days are UTC days. A formatter that shifted them into local time would name the day before for half
    // the world, which is how a Monday's week comes to be labelled with a Sunday.
    const { tile } = draw(answering());

    expect(tile.dayLabel('2026-10-26')).toBe('Mon, 26 Oct 2026');
    expect(tile.dayLabel('2026-11-01')).toBe('Sun, 01 Nov 2026');
    expect(tile.dayLabel('2027-01-01')).toBe('Fri, 01 Jan 2027');
    expect(tile.dayLabel('')).toBe('');
  });
});
