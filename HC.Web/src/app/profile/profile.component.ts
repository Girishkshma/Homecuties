import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService, CustomerInfo } from '../services/auth.service';
import { CustomerService } from '../services/customer.service';
import { CustomerAddress } from '../models/product.model';
import { PincodeService, PincodeArea } from '../services/pincode.service';
import {
  CITY_STATE_FROM_PINCODE_HINT,
  PINCODE_NOT_FOUND_MESSAGE,
  PINCODE_UNAVAILABLE_MESSAGE,
  SAVED_CITY_STATE_KEPT_MESSAGE,
  isCompletePincode,
  landmarkWithArea,
  normalizePincode,
  pincodeAreaOptionText,
  pincodeAreas,
  pincodeCityStateOutcome
} from '../services/pincode-lookup';

/** A blank address to type into the form (AddressId 0 = a new one). */
function emptyAddress(): CustomerAddress {
  return {
    AddressId: 0,
    AddressTitle: '',
    ContactName: '',
    AddressLine1: '',
    AddressLine2: '',
    City: '',
    State: '',
    Country: 'India',
    Zipcode: '',
    MobileNumber: '',
    EmailId: ''
  };
}

/**
 * 'My Profile' - the customer's account details and, above all, their address book.
 *
 * The saved addresses are what checkout offers as the delivery address and as the billing address, so
 * a customer can keep several (home, office, a relative's place) instead of typing one per order.
 */
@Component({
  selector: 'app-profile',
  templateUrl: './profile.component.html',
  standalone: false,
  styleUrl: './profile.component.scss'
})
export class ProfileComponent implements OnInit {
  customer: CustomerInfo | null = null;
  addresses: CustomerAddress[] = [];
  loading = true;
  error = '';
  notice = '';

  /** The address being typed: a new one (AddressId 0) or the one being edited. */
  form: CustomerAddress = emptyAddress();
  showForm = false;
  saving = false;
  formError = '';

  // PIN code lookup for the form above - the city, the state and the area are filled in from the PIN
  // code. The city and the state are read-only, so the PIN code is the only thing that fills them in
  // (see PincodeService and 'pincode-lookup').
  lookingUpPincode = false;
  pincodeAreas: PincodeArea[] = [];
  pincodeChoice = '';
  pincodeMessage = '';
  pincodeError = '';

  /** The line under the city and the state, which say where they come from (the wording is shared). */
  readonly cityStateHint = CITY_STATE_FROM_PINCODE_HINT;

  /** The PIN the current lookup is for, so a slow answer for an older PIN code is ignored. */
  private pincodeRequested = '';

  /** The area the last lookup filled in - it is replaced, not stacked up, when the PIN changes. */
  private pincodeAreaInUse = '';

  /**
   * The address being edited, exactly as it was saved. Its PIN code is the one PIN code whose city and
   * state may stand as they are, because they came from that PIN code when it was saved - so a lookup
   * that cannot run must not take them away while the customer corrects the rest of the address (see
   * 'pincodeLookupFailed'). Null while a new address is being typed, which has no city or state yet.
   */
  private editingAddress: CustomerAddress | null = null;

  /** Address whose removal is waiting for a confirmation, and the one an action is running for. */
  confirmingAddressId = 0;
  busyAddressId = 0;

  constructor(
    private authService: AuthService,
    private customerService: CustomerService,
    private pincodeService: PincodeService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.customer = this.authService.getCurrentCustomer();
    this.loadAddresses();
  }

  loadAddresses(): void {
    this.loading = true;
    this.error = '';

    this.customerService.getAddresses().subscribe({
      next: (rows) => {
        this.addresses = rows || [];
        this.loading = false;
      },
      error: (err) => {
        // A 401 means the token was missing or expired - say so plainly instead of showing an empty
        // address book, which reads as 'my addresses are gone' (the interceptor signs them out too).
        this.error = err?.status === 401
          ? 'Your sign-in has expired. Please sign in again to see your addresses.'
          : 'We could not load your addresses. Please try again in a moment.';
        this.loading = false;
        console.error('Failed to load addresses:', err);
      }
    });
  }

  /** Opens the form for a new address, prefilled with what the account already knows. */
  startAddAddress(): void {
    this.form = emptyAddress();
    this.form.ContactName = this.fullName();
    this.form.MobileNumber = this.customer?.MobileNumber || '';
    this.form.EmailId = this.customer?.EmailId || '';
    this.editingAddress = null;
    this.showForm = true;
    this.formError = '';
    this.notice = '';
    this.clearPincodeLookup();
  }

  startEditAddress(address: CustomerAddress): void {
    this.form = { ...address, EmailId: address.EmailId || '' };
    this.editingAddress = { ...address };
    this.showForm = true;
    this.formError = '';
    this.notice = '';
    this.clearPincodeLookup();
  }

  cancelForm(): void {
    this.form = emptyAddress();
    this.editingAddress = null;
    this.showForm = false;
    this.formError = '';
    this.clearPincodeLookup();
  }

  // ---------------------------------------------------------------------------------------------
  // PIN code -> city, state and area
  //
  // The customer types the PIN code, and the city, the state and the area that PIN code covers are
  // filled in from it. The city and the state are read-only - the PIN code is the only thing that fills
  // them - so the PIN code is what a customer corrects when the place is wrong, which is why every
  // message about a lookup that could not fill them in ends there.
  // ---------------------------------------------------------------------------------------------

  /**
   * Called as the customer types the PIN code. Anything that cannot be part of a PIN code is dropped
   * from the field, a whole PIN code is looked up, and a half-typed one clears the lookup state so an
   * area from an earlier PIN code is never left behind.
   */
  onPincodeTyped(value: string): void {
    const pin = normalizePincode(value);
    if (pin !== this.form.Zipcode) {
      this.form.Zipcode = pin;
    }

    if (!isCompletePincode(pin)) {
      this.clearPincodeLookup();

      // Nothing can fill the city and the state in until a whole PIN code is back, so a place from the
      // PIN code that was just erased must not stay on the form - the fields are read-only, and a value
      // with no PIN code behind it is exactly what a customer cannot correct.
      this.form.City = '';
      this.form.State = '';
      return;
    }

    if (pin === this.pincodeRequested) {
      return;
    }

    this.lookUpPincode(pin);
  }

  /** The customer picked one of the places this PIN code covers (see the area list in the form). */
  onPincodeAreaChosen(areaName: string): void {
    const area = this.pincodeAreas.find((candidate) => candidate.Area === areaName);
    if (area) {
      this.applyPincodeArea(area);
    }
  }

  /**
   * The text of one choice in the area list. The wording is shared with the checkout forms (see
   * 'pincode-lookup'), and a template can only reach the component, so it is handed over from here.
   */
  pincodeAreaOption(area: PincodeArea): string {
    return pincodeAreaOptionText(area);
  }

  private lookUpPincode(pin: string): void {
    this.pincodeRequested = pin;
    this.lookingUpPincode = true;
    this.pincodeAreas = [];
    this.pincodeChoice = '';
    this.pincodeMessage = '';
    this.pincodeError = '';

    // The city and the state belong to whichever PIN code is being looked up: a value left over from an
    // earlier PIN code must not be saved with this one, and it must not be saved while the answer that
    // would fill them in is still on its way.
    this.form.City = '';
    this.form.State = '';

    this.pincodeService.lookupPincode(pin).subscribe({
      next: (result) => {
        // The customer has typed a different PIN code since: this answer is about a place they are no
        // longer asking about.
        if (this.pincodeRequested !== pin) {
          return;
        }

        this.lookingUpPincode = false;

        const areas = pincodeAreas(result);
        if (areas.length === 0) {
          this.pincodeLookupFailed(pin, result?.Messages?.[0] || PINCODE_NOT_FOUND_MESSAGE);
          return;
        }

        if (areas.length === 1) {
          this.applyPincodeArea(areas[0]);
          return;
        }

        // Every post office the PIN code covers is offered, as the public directory lists them, with the
        // one that delivers first; which of them is theirs is something only the customer knows.
        this.pincodeAreas = areas;
        this.pincodeMessage = `PIN code ${pin} covers ${areas.length} post offices. Pick the one closest to you.`;
      },
      error: () => {
        if (this.pincodeRequested !== pin) {
          return;
        }

        this.lookingUpPincode = false;
        this.pincodeLookupFailed(pin, PINCODE_UNAVAILABLE_MESSAGE);
      }
    });
  }

  /**
   * Fills the form in for the customer. The city and the state are the PIN code's answer and are not
   * typed into, so they are replaced rather than left over from an earlier PIN code - and when the
   * answer does not name both, the customer is told instead of being left with a form that will not
   * save.
   */
  private applyPincodeArea(area: PincodeArea): void {
    this.form.AddressLine2 = landmarkWithArea(this.form.AddressLine2, this.pincodeAreaInUse, area.Area);
    this.pincodeAreaInUse = area.Area;

    this.form.City = (area.City || '').trim();
    this.form.State = (area.State || '').trim();

    const outcome = pincodeCityStateOutcome(area, this.form.Zipcode);
    if (outcome.filled) {
      this.pincodeError = '';
      this.pincodeMessage = outcome.message;
      return;
    }

    this.pincodeMessage = '';
    this.pincodeError = outcome.message;
  }

  /**
   * A PIN code that could not be looked up leaves no city and no state behind: those two fields are the
   * lookup's to fill and cannot be typed into, so a place from an earlier PIN code must not ride along
   * with this one.
   *
   * The one exception is the address being edited while its PIN code is unchanged. The city and the
   * state that address was saved with came from that PIN code, so they are given back and the message
   * says where they came from - a directory that is down, or that no longer knows a PIN code it knew
   * when the address was saved, must never stop a customer from correcting the rest of their address.
   */
  private pincodeLookupFailed(pin: string, message: string): void {
    this.pincodeAreaInUse = '';

    const saved = this.editingAddress;
    const savedCity = (saved?.City || '').trim();
    const savedState = (saved?.State || '').trim();

    if (pin === (saved?.Zipcode || '').trim() && savedCity.length > 0 && savedState.length > 0) {
      this.form.City = savedCity;
      this.form.State = savedState;
      this.pincodeError = SAVED_CITY_STATE_KEPT_MESSAGE;
      return;
    }

    this.form.City = '';
    this.form.State = '';
    this.pincodeError = message;
  }

  private clearPincodeLookup(): void {
    this.pincodeRequested = '';
    this.pincodeAreaInUse = '';
    this.lookingUpPincode = false;
    this.pincodeAreas = [];
    this.pincodeChoice = '';
    this.pincodeMessage = '';
    this.pincodeError = '';
  }

  /**
   * Why the address cannot be saved yet, or '' when it can.
   *
   * The city and the state are not typed into - they are filled in from the PIN code - so a missing one
   * of those is said as a PIN code problem, rather than as a field to fill in, which would send the
   * customer looking for something they cannot type into.
   */
  private addressProblem(): string {
    if (!this.form.AddressLine1?.trim()) {
      return 'Please fill in the address.';
    }

    if (!isCompletePincode(this.form.Zipcode)) {
      return 'Please fill in your PIN code - the city and the state are filled in from it.';
    }

    if (!this.form.City?.trim() || !this.form.State?.trim()) {
      // The address can only wait for one of two things: the customer picking which post office this is,
      // or a PIN code that the directory can answer for.
      return this.pincodeAreas.length > 1 && !this.pincodeChoice
        ? 'Please choose the post office this address is closest to.'
        : this.pincodeError ||
          'We could not fill in the city and the state from your PIN code. Please check the PIN code.';
    }

    if ((this.form.MobileNumber || '').trim().length > 12) {
      return 'Please check the phone number - it looks too long.';
    }

    return '';
  }

  saveAddress(): void {
    this.formError = this.addressProblem();
    if (this.formError) {
      return;
    }

    this.saving = true;

    this.customerService.saveAddress(this.form).subscribe({
      next: (result) => {
        this.saving = false;

        if (result.Result === 1) {
          this.notice = result.Messages?.[0] || 'Address saved.';
          this.cancelForm();
          this.loadAddresses();
          return;
        }

        this.formError = result.Messages?.join(' ') || 'We could not save that address.';
      },
      error: (err) => {
        this.saving = false;
        this.formError = err?.status === 401
          ? 'Your sign-in has expired. Please sign in again.'
          : 'We could not save that address. Please try again.';
        console.error('Failed to save address:', err);
      }
    });
  }

  askToRemove(address: CustomerAddress): void {
    this.notice = '';
    this.formError = '';
    this.confirmingAddressId = address.AddressId;
  }

  dismissRemove(): void {
    this.confirmingAddressId = 0;
  }

  removeAddress(address: CustomerAddress): void {
    this.confirmingAddressId = 0;
    this.busyAddressId = address.AddressId;

    this.customerService.deleteAddress(address.AddressId).subscribe({
      next: (result) => {
        this.busyAddressId = 0;

        if (result.Result === 1) {
          this.notice = result.Messages?.[0] || 'Address removed.';
          this.loadAddresses();
          return;
        }

        // The server refuses to remove an address an order was placed with - that is what this says.
        this.error = result.Messages?.join(' ') || 'We could not remove that address.';
      },
      error: (err) => {
        this.busyAddressId = 0;
        this.error = err?.status === 401
          ? 'Your sign-in has expired. Please sign in again.'
          : 'We could not remove that address. Please try again.';
        console.error('Failed to remove address:', err);
      }
    });
  }

  fullName(): string {
    const customer = this.customer;
    if (!customer) {
      return '';
    }

    return `${customer.FirstName || ''} ${customer.LastName || ''}`.trim();
  }

  goToOrders(): void {
    this.router.navigate(['/my-orders']);
  }
}
