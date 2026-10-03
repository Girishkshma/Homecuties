import { Component, OnInit, OnDestroy, Inject, PLATFORM_ID, NgZone } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { CartService } from '../services/cart.service';
import { AuthService } from '../services/auth.service';
import { PaymentService, CreateOrderResponse, OrderActionResult } from '../services/payment.service';
import { CustomerService } from '../services/customer.service';
import { UtilityService } from '../services/utility.service';
import { CustomerAddress } from '../models/product.model';
import { PaymentWindowWatcher } from '../services/payment-window';
import { PincodeService, PincodeArea } from '../services/pincode.service';
import {
  CITY_STATE_FROM_PINCODE_HINT,
  PINCODE_NOT_FOUND_MESSAGE,
  PINCODE_UNAVAILABLE_MESSAGE,
  isCompletePincode,
  landmarkWithArea,
  normalizePincode,
  pincodeAreaOptionText,
  pincodeAreas,
  pincodeCityStateOutcome
} from '../services/pincode-lookup';
import { Router } from '@angular/router';

declare var Razorpay: any;

@Component({
  selector: 'app-checkout',
  templateUrl: './checkout.component.html',
  standalone: false,
  styleUrl: './checkout.component.scss'
})
export class CheckoutComponent implements OnInit, OnDestroy {
  /**
   * Key under which the order whose payment is in flight is remembered. Without it a customer who
   * paid and then lost the Razorpay callback (closed the window, reloaded, lost the connection) saw
   * the button spin on 'Processing...' forever, because only the callback ever cleared it.
   */
  private static readonly PendingOrderKey = 'hcPendingOrderId';

  cartItems: any[] = [];
  loading = true;
  placingOrder = false;
  error = '';
  orderSuccess = false;
  orderNumber = '';

  /** True while Razorpay Checkout has handed the payment over and a result is still on its way. */
  paymentInFlight = false;

  /**
   * True once Razorpay reported a payment result. Razorpay also raises 'dismiss' after a successful
   * payment, so a dismissal is only a cancellation while no result has been seen at all.
   */
  private paymentAttempted = false;

  /** The order the current payment belongs to - lets the customer jump to it when a check fails. */
  lastOrderId = 0;

  /** Order that was created but not paid for - the subject of the 'payment not completed' panel. */
  pendingOrderId = 0;

  /** True while the page is telling the customer that an order of theirs is still waiting for payment. */
  paymentNotCompleted = false;

  /** What the panel says about that order: why it was not paid for, and what can be done about it. */
  pendingOrderMessage = '';

  /** True while 'Pay now' on the panel is asking the backend for a fresh payment attempt. */
  payingPendingOrder = false;

  /** Notices the payment window being closed (see PaymentWindowWatcher) - see also 'onPaymentWindowDismissed'. */
  private windowWatcher: PaymentWindowWatcher | null = null;

  // Stock validation
  removedItems: string[] = [];
  adjustedItems: string[] = [];
  showStockWarning = false;

  // Shipping form - the address typed here is used when it is not picked from the address book
  shippingAddress = '';
  city = '';
  state = '';
  zipCode = '';
  phoneNumber = '';
  email = '';

  /** Optional label for the new address ("Home", "Office", ...) - see the address form. */
  addressTitle = '';

  /** Optional second line / landmark of the new address. */
  addressLine2 = '';

  /** Who receives the parcel; the account holder is used when left empty. */
  contactName = '';

  // PIN code lookup - the city, the state and the area of the typed address come from the PIN code.
  // The city and the state are read-only, so the PIN code is the only thing that fills them in (see
  // PincodeService and 'pincode-lookup'). One set per address form, like the fields above.
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

  lookingUpBillingPincode = false;
  billingPincodeAreas: PincodeArea[] = [];
  billingPincodeChoice = '';
  billingPincodeMessage = '';
  billingPincodeError = '';
  private billingPincodeRequested = '';
  private billingPincodeAreaInUse = '';

  // Address book - the addresses the customer saved in 'My Profile'
  savedAddresses: CustomerAddress[] = [];

  /** Saved address picked as the delivery address, or 0 while the form below is the address. */
  selectedShippingAddressId = 0;

  /** Saved address picked as the billing address, or 0 while the billing form is the address. */
  selectedBillingAddressId = 0;

  /** True (the usual case) while the order is billed to the address it is delivered to. */
  billingSameAsShipping = true;

  // Billing address - only used while 'billingSameAsShipping' is off
  billingContactName = '';
  billingAddressLine1 = '';
  billingAddressLine2 = '';
  billingCity = '';
  billingState = '';
  billingZipCode = '';
  billingPhoneNumber = '';
  billingEmail = '';

  // Payment
  paymentMethod = 'razorpay';

  UtilityService = UtilityService;

  private isBrowser: boolean;

  constructor(
    private cartService: CartService,
    private authService: AuthService,
    private paymentService: PaymentService,
    private customerService: CustomerService,
    private pincodeService: PincodeService,
    private router: Router,
    private zone: NgZone,
    @Inject(PLATFORM_ID) private platformId: Object
  ) {
    this.isBrowser = isPlatformBrowser(this.platformId);
  }

  ngOnInit(): void {
    this.loadCart();
    this.prefillCustomerInfo();
    this.loadSavedAddresses();
  }

  /** Nothing may keep watching the page once the customer has left it. */
  ngOnDestroy(): void {
    this.windowWatcher?.stop();
  }

  /**
   * Finishes a payment the browser never got the result of. The order id is remembered before
   * Razorpay Checkout opens, so coming back to this page (with an empty cart - see 'loadCart') asks
   * the backend to look the payment up and settle the order: the customer never gets stuck on
   * 'Processing...', and the payment is confirmed even when the callback never reached us.
   */
  private resumePendingPayment(): void {
    const pendingOrderId = this.getPendingOrderId();
    if (!pendingOrderId) {
      return;
    }

    this.lastOrderId = pendingOrderId;
    this.syncPayment(pendingOrderId);
  }

  private getPendingOrderId(): number {
    const stored = this.isBrowser ? localStorage.getItem(CheckoutComponent.PendingOrderKey) : null;
    return stored ? Number(stored) : 0;
  }

  private rememberPendingOrder(orderId: number): void {
    this.lastOrderId = orderId;

    if (this.isBrowser) {
      localStorage.setItem(CheckoutComponent.PendingOrderKey, String(orderId));
    }
  }

  private clearPendingOrder(): void {
    if (this.isBrowser) {
      localStorage.removeItem(CheckoutComponent.PendingOrderKey);
    }
  }

  private prefillCustomerInfo(): void {
    const customer = this.authService.getCurrentCustomer();
    if (customer) {
      this.email = customer.EmailId || '';
      this.phoneNumber = customer.MobileNumber || '';
      if (!this.contactName) {
        this.contactName = `${customer.FirstName || ''} ${customer.LastName || ''}`.trim();
      }
    }
  }

  /** True while the delivery address is typed below rather than picked from the address book. */
  get isNewShippingAddress(): boolean {
    return this.selectedShippingAddressId === 0;
  }

  /**
   * True for a signed-in customer with a usable token - the only one who has an address book, and the
   * only one whose typed addresses are stored in it (see AuthService.hasValidSession).
   */
  get isSignedIn(): boolean {
    return this.authService.hasValidSession();
  }

  /** True while the billing address is typed below rather than picked from the address book. */
  get isNewBillingAddress(): boolean {
    return this.selectedBillingAddressId === 0;
  }

  /**
   * The addresses the customer saved in 'My Profile'. Picking one turns a second order into a
   * two-click affair instead of typing the same address out again. A guest has no address book (the
   * call answers 401), and a failure here must never block checkout - the form below still works.
   */
  private loadSavedAddresses(): void {
    if (!this.authService.hasValidSession()) {
      return;
    }

    this.customerService.getAddresses().subscribe({
      next: (addresses) => {
        this.savedAddresses = addresses || [];

        // Preselect the newest saved address (the server sends them newest first), so a returning
        // customer only has to press 'Place Order'.
        if (this.savedAddresses.length > 0) {
          this.selectedShippingAddressId = this.savedAddresses[0].AddressId;
        }
      },
      error: (err) => console.error('Failed to load the saved addresses:', err)
    });
  }

  chooseSavedShipping(addressId: number): void {
    this.selectedShippingAddressId = addressId;
    this.error = '';
  }

  chooseNewShippingAddress(): void {
    this.selectedShippingAddressId = 0;

    // Start from what the account already knows rather than from an empty form.
    if (!this.shippingAddress && !this.city) {
      this.prefillCustomerInfo();
    }
  }

  chooseSavedBilling(addressId: number): void {
    this.selectedBillingAddressId = addressId;
    this.error = '';
  }

  chooseNewBillingAddress(): void {
    this.selectedBillingAddressId = 0;
  }

  /**
   * Turning 'same as the delivery address' off starts the billing form from the account details, so
   * the customer never ends up billing an address that quietly still holds the delivery address.
   */
  onBillingSameAsShippingChange(): void {
    this.error = '';

    if (this.billingSameAsShipping) {
      return;
    }

    const customer = this.authService.getCurrentCustomer();
    if (!this.billingContactName) {
      this.billingContactName = this.contactName ||
        (customer ? `${customer.FirstName || ''} ${customer.LastName || ''}`.trim() : '');
    }
    this.billingPhoneNumber = this.billingPhoneNumber || customer?.MobileNumber || '';
    this.billingEmail = this.billingEmail || customer?.EmailId || '';
  }

  /** The saved address picked as the delivery address, or null while one is being typed. */
  private selectedShippingAddress(): CustomerAddress | null {
    return this.savedAddresses.find(a => a.AddressId === this.selectedShippingAddressId) || null;
  }

  /** What Razorpay Checkout is prefilled with - the picked saved address wins over the account. */
  private contactEmail(): string {
    const customer = this.authService.getCurrentCustomer();
    return (this.selectedShippingAddress()?.EmailId || this.email || customer?.EmailId || '').trim();
  }

  private contactPhone(): string {
    const customer = this.authService.getCurrentCustomer();
    return (this.selectedShippingAddress()?.MobileNumber || this.phoneNumber || customer?.MobileNumber || '').trim();
  }

  private getCustomerInfo(): { customerId: number; isGuest: boolean } {
    const customer = this.authService.getCurrentCustomer();
    if (customer) {
      return { customerId: customer.CustomerID, isGuest: false };
    }

    // Guests: reuse the guest customer id created when the item was added to the cart.
    // Sending 0 here would look up a different (empty) cart and fail with "Cart is empty".
    return { customerId: this.cartService.getStoredGuestCustomerId(), isGuest: true };
  }

  private loadCart(): void {
    const { customerId, isGuest } = this.getCustomerInfo();
    this.cartService.getCart(customerId, isGuest).subscribe({
      next: (data) => {
        this.cartItems = data.Items || [];
        this.loading = false;

        // Check for out-of-stock items and show warning
        this.validateCartStock();

        // Nothing left to check out: the customer has most likely just come back from a payment that
        // never reported back, so finish that order before showing them an empty checkout page.
        if (this.getFilteredCartItems().length === 0) {
          this.resumePendingPayment();
        }
      },
      error: (err) => {
        this.error = 'Failed to load cart. Please try again.';
        this.loading = false;
        console.error(err);
      }
    });
  }

  private validateCartStock(): void {
    const outOfStock: string[] = [];
    const adjusted: string[] = [];

    for (const item of this.cartItems) {
      if (!item.IsInStock) {
        outOfStock.push(item.ProductTitle);
      } else if (item.AvailableQty > 0 && item.Quantity > item.AvailableQty) {
        adjusted.push(`${item.ProductTitle} (only ${item.AvailableQty} available)`);
      }
    }

    this.removedItems = outOfStock;
    this.adjustedItems = adjusted;
    this.showStockWarning = outOfStock.length > 0 || adjusted.length > 0;
  }

  getFilteredCartItems(): any[] {
    return this.cartItems.filter((item: any) => item.IsInStock);
  }

  /**
   * True while the page is settling an earlier payment (see 'resumePendingPayment'): with an empty
   * cart this is the only thing the page is busy with, so it shows 'Checking your payment...'.
   */
  get checkingPayment(): boolean {
    return this.placingOrder && !this.orderSuccess && this.getFilteredCartItems().length === 0;
  }

  getSubtotal(): number {
    return this.getFilteredCartItems().reduce((sum: number, item: any) => sum + (item.Price * item.Quantity), 0);
  }

  /**
   * True once every address the order needs is there. A picked saved address counts as complete (it was
   * saved through a form that asked for all of this); a typed one has to pass 'orderProblem'.
   */
  isFormValid(): boolean {
    return this.orderProblem() === '';
  }

  /**
   * Why the order cannot be placed yet, or '' while it can. The place-order button is disabled until
   * this is empty, so the page says it out loud as well: a button that is quietly dead would leave the
   * customer with no way of knowing that the PIN code is what has to be corrected.
   */
  orderProblem(): string {
    return this.shippingAddressProblem() || this.billingAddressProblem();
  }

  /**
   * What is still missing from the typed delivery address, or '' while nothing is. The city and the
   * state are not typed into - they are filled in from the PIN code (see 'pincode-lookup') - so a
   * missing one of those is said as a PIN code problem, rather than as a field to fill in.
   */
  private shippingAddressProblem(): string {
    if (!this.isNewShippingAddress) {
      return '';
    }

    if (!this.shippingAddress.trim()) {
      return 'Please fill in the address we deliver to.';
    }

    if (!isCompletePincode(this.zipCode)) {
      return 'Please fill in your PIN code - the delivery city and state are filled in from it.';
    }

    if (!this.city.trim() || !this.state.trim()) {
      // The address can only be waiting for one of two things: the customer picking which post office
      // this is, or a PIN code the directory can answer for.
      return this.pincodeAreas.length > 1 && !this.pincodeChoice
        ? 'Please choose the post office this address is closest to.'
        : this.pincodeError ||
          'We could not fill in the city and the state from your PIN code. Please check the PIN code.';
    }

    if (this.phoneNumber.trim().length < 10) {
      return 'Please fill in a phone number we can reach you on.';
    }

    if (!this.email.trim()) {
      return 'Please fill in your email for the order confirmation.';
    }

    return '';
  }

  /** The same for the invoice address, while the customer is billing somewhere other than delivery. */
  private billingAddressProblem(): string {
    if (this.billingSameAsShipping || !this.isNewBillingAddress) {
      return '';
    }

    if (!this.billingAddressLine1.trim()) {
      return 'Please fill in the address we bill to.';
    }

    if (!isCompletePincode(this.billingZipCode)) {
      return 'Please fill in the PIN code for the invoice address - its city and state are filled in from it.';
    }

    if (!this.billingCity.trim() || !this.billingState.trim()) {
      return this.billingPincodeAreas.length > 1 && !this.billingPincodeChoice
        ? 'Please choose the post office this invoice address is closest to.'
        : this.billingPincodeError ||
          'We could not fill in the city and the state from the invoice PIN code. Please check the PIN code.';
    }

    return '';
  }

  // ---------------------------------------------------------------------------------------------
  // PIN code -> city, state and area (delivery address)
  //
  // Typing a PIN code is the only thing the customer has to do about the place they live: the city,
  // the state and the area that PIN code covers are filled in from it. The city and the state are
  // read-only - the PIN code is the only thing that fills them - so the PIN code is what a customer
  // corrects when the place is wrong, which is why every message about a lookup that could not fill
  // them in ends there.
  // ---------------------------------------------------------------------------------------------

  /**
   * Called as the customer types the delivery PIN code. Anything that cannot be part of a PIN code is
   * dropped from the field, a whole PIN code is looked up, and a half-typed one clears the lookup
   * state so an area from an earlier PIN code is never left behind.
   */
  onShippingPincode(value: string): void {
    const pin = normalizePincode(value);
    if (pin !== this.zipCode) {
      this.zipCode = pin;
    }

    if (!isCompletePincode(pin)) {
      this.clearShippingPincodeLookup();
      return;
    }

    if (pin === this.pincodeRequested) {
      return;
    }

    this.lookUpShippingPincode(pin);
  }

  /** The customer picked one of the places this PIN code covers (see the area list in the form). */
  onShippingAreaChosen(areaName: string): void {
    const area = this.pincodeAreas.find((candidate) => candidate.Area === areaName);
    if (area) {
      this.applyShippingArea(area);
    }
  }

  /**
   * The text of one choice in the area list. The wording is shared with the other address forms (see
   * 'pincode-lookup'), and a template can only reach the component, so it is handed over from here.
   */
  pincodeAreaOption(area: PincodeArea): string {
    return pincodeAreaOptionText(area);
  }

  private lookUpShippingPincode(pin: string): void {
    this.pincodeRequested = pin;
    this.lookingUpPincode = true;
    this.pincodeAreas = [];
    this.pincodeChoice = '';
    this.pincodeMessage = '';
    this.pincodeError = '';

    // The city and the state belong to whichever PIN code is being looked up: a value left over from an
    // earlier PIN code must not be ordered with this one, and it must not be ordered while the answer
    // that would fill them in is still on its way.
    this.city = '';
    this.state = '';

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
          this.pincodeAreaInUse = '';
          this.pincodeError = result?.Messages?.[0] || PINCODE_NOT_FOUND_MESSAGE;
          return;
        }

        if (areas.length === 1) {
          this.applyShippingArea(areas[0]);
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
        this.pincodeAreaInUse = '';
        this.pincodeError = PINCODE_UNAVAILABLE_MESSAGE;
      }
    });
  }

  /**
   * Fills the form in for the customer. The city and the state are the PIN code's answer and are not
   * typed into, so they are replaced rather than left over from an earlier PIN code - and when the
   * answer does not name both, the customer is told instead of being left with a form that cannot be
   * ordered from.
   */
  private applyShippingArea(area: PincodeArea): void {
    this.addressLine2 = landmarkWithArea(this.addressLine2, this.pincodeAreaInUse, area.Area);
    this.pincodeAreaInUse = area.Area;

    this.city = (area.City || '').trim();
    this.state = (area.State || '').trim();

    const outcome = pincodeCityStateOutcome(area, this.zipCode);
    if (outcome.filled) {
      this.pincodeError = '';
      this.pincodeMessage = outcome.message;
      return;
    }

    this.pincodeMessage = '';
    this.pincodeError = outcome.message;
  }

  private clearShippingPincodeLookup(): void {
    this.pincodeRequested = '';
    this.pincodeAreaInUse = '';
    this.lookingUpPincode = false;
    this.pincodeAreas = [];
    this.pincodeChoice = '';
    this.pincodeMessage = '';
    this.pincodeError = '';

    // No whole PIN code has been typed, so nothing can fill the city and the state in: they go back to
    // empty rather than showing a place that no PIN code in this form answers for.
    this.city = '';
    this.state = '';
  }

  // ---------------------------------------------------------------------------------------------
  // PIN code -> city, state and area (billing address) - the same thing for the billing form, which
  // is only shown when the invoice goes somewhere other than the delivery address.
  // ---------------------------------------------------------------------------------------------

  /** Called as the customer types the billing PIN code - see 'onShippingPincode'. */
  onBillingPincode(value: string): void {
    const pin = normalizePincode(value);
    if (pin !== this.billingZipCode) {
      this.billingZipCode = pin;
    }

    if (!isCompletePincode(pin)) {
      this.clearBillingPincodeLookup();
      return;
    }

    if (pin === this.billingPincodeRequested) {
      return;
    }

    this.lookUpBillingPincode(pin);
  }

  /** The customer picked one of the places the billing PIN code covers. */
  onBillingAreaChosen(areaName: string): void {
    const area = this.billingPincodeAreas.find((candidate) => candidate.Area === areaName);
    if (area) {
      this.applyBillingArea(area);
    }
  }

  private lookUpBillingPincode(pin: string): void {
    this.billingPincodeRequested = pin;
    this.lookingUpBillingPincode = true;
    this.billingPincodeAreas = [];
    this.billingPincodeChoice = '';
    this.billingPincodeMessage = '';
    this.billingPincodeError = '';

    // The city and the state belong to whichever PIN code is being looked up - see 'lookUpShippingPincode'.
    this.billingCity = '';
    this.billingState = '';

    this.pincodeService.lookupPincode(pin).subscribe({
      next: (result) => {
        if (this.billingPincodeRequested !== pin) {
          return;
        }

        this.lookingUpBillingPincode = false;

        const areas = pincodeAreas(result);
        if (areas.length === 0) {
          this.billingPincodeAreaInUse = '';
          this.billingPincodeError = result?.Messages?.[0] || PINCODE_NOT_FOUND_MESSAGE;
          return;
        }

        if (areas.length === 1) {
          this.applyBillingArea(areas[0]);
          return;
        }

        // Every post office the PIN code covers is offered for the invoice address too, delivering one
        // first, exactly as it is for the delivery address.
        this.billingPincodeAreas = areas;
        this.billingPincodeMessage =
          `PIN code ${pin} covers ${areas.length} post offices. Pick the one closest to you.`;
      },
      error: () => {
        if (this.billingPincodeRequested !== pin) {
          return;
        }

        this.lookingUpBillingPincode = false;
        this.billingPincodeAreaInUse = '';
        this.billingPincodeError = PINCODE_UNAVAILABLE_MESSAGE;
      }
    });
  }

  /** Fills the invoice form in - the city and the state are the PIN code's answer here as well. */
  private applyBillingArea(area: PincodeArea): void {
    this.billingAddressLine2 = landmarkWithArea(this.billingAddressLine2, this.billingPincodeAreaInUse, area.Area);
    this.billingPincodeAreaInUse = area.Area;

    this.billingCity = (area.City || '').trim();
    this.billingState = (area.State || '').trim();

    const outcome = pincodeCityStateOutcome(area, this.billingZipCode);
    if (outcome.filled) {
      this.billingPincodeError = '';
      this.billingPincodeMessage = outcome.message;
      return;
    }

    this.billingPincodeMessage = '';
    this.billingPincodeError = outcome.message;
  }

  private clearBillingPincodeLookup(): void {
    this.billingPincodeRequested = '';
    this.billingPincodeAreaInUse = '';
    this.lookingUpBillingPincode = false;
    this.billingPincodeAreas = [];
    this.billingPincodeChoice = '';
    this.billingPincodeMessage = '';
    this.billingPincodeError = '';
  }

  placeOrder(): void {
    // The button is disabled while this is not empty, so this only ever speaks when something changed
    // after the button was last drawn - either way, the customer is told exactly what is missing.
    this.error = this.orderProblem();
    if (this.error) {
      return;
    }

    this.placingOrder = true;
    this.error = '';

    // Placing an order takes the page over: the panel about an earlier unpaid order would only be in
    // the way. (It comes back if this attempt is not paid for either - see 'showPendingOrder'.) The
    // check above deliberately comes first, so a half-filled form never hides it.
    this.paymentNotCompleted = false;
    this.pendingOrderMessage = '';

    const { customerId, isGuest } = this.getCustomerInfo();

    // Which addresses are being typed here, rather than picked from the address book.
    const typingShipping = this.isNewShippingAddress;
    const typingBilling = !this.billingSameAsShipping && this.isNewBillingAddress;

    this.paymentService.createOrder({
      CustomerID: customerId,
      IsGuest: isGuest,

      // A picked saved address travels as its id (the server uses that row and ignores the flat
      // fields, so nothing is duplicated); a typed one travels field by field and is stored in the
      // address book, ready to be picked next time.
      ShippingAddressId: typingShipping ? 0 : this.selectedShippingAddressId,
      ShippingAddress: typingShipping ? this.shippingAddress.trim() : '',
      ShippingAddressTitle: typingShipping ? this.addressTitle.trim() : '',
      ShippingAddressLine2: typingShipping ? this.addressLine2.trim() : '',
      ShippingContactName: typingShipping ? this.contactName.trim() : '',
      City: typingShipping ? this.city.trim() : '',
      State: typingShipping ? this.state.trim() : '',
      ZipCode: typingShipping ? this.zipCode.trim() : '',
      PhoneNumber: typingShipping ? this.phoneNumber.trim() : '',
      Email: typingShipping ? this.email.trim() : '',

      PaymentMethod: this.paymentMethod,

      // Billing: the delivery address, unless the customer asked for a different one.
      BillingSameAsShipping: this.billingSameAsShipping,
      BillingAddressId: typingBilling ? 0 : this.selectedBillingAddressId,
      BillingContactName: typingBilling ? this.billingContactName.trim() : '',
      BillingAddressLine1: typingBilling ? this.billingAddressLine1.trim() : '',
      BillingAddressLine2: typingBilling ? this.billingAddressLine2.trim() : '',
      BillingCity: typingBilling ? this.billingCity.trim() : '',
      BillingState: typingBilling ? this.billingState.trim() : '',
      BillingZipCode: typingBilling ? this.billingZipCode.trim() : '',
      BillingPhoneNumber: typingBilling ? this.billingPhoneNumber.trim() : '',
      BillingEmail: typingBilling ? this.billingEmail.trim() : ''
    }).subscribe({
      next: (response: CreateOrderResponse) => {
        if (response.Result === 1) {
          // Store any removed/adjusted items info from the backend response
          if (response.RemovedItems && response.RemovedItems.length > 0) {
            this.removedItems = response.RemovedItems;
          }
          if (response.AdjustedItems && response.AdjustedItems.length > 0) {
            this.adjustedItems = response.AdjustedItems;
          }
          this.openRazorpayCheckout(response);
        } else {
          this.error = response.Messages?.join(', ') || 'Failed to create order.';
          this.placingOrder = false;
        }
      },
      error: (err) => {
        this.error = 'Failed to create order. Please try again.';
        this.placingOrder = false;
        console.error(err);
      }
    });
  }

  private openRazorpayCheckout(orderData: CreateOrderResponse): void {
    // Remember the order BEFORE the window opens: if the callback never reaches us, the next visit
    // to this page asks the backend to settle the payment (see resumePendingPayment).
    this.rememberPendingOrder(orderData.OrderId);

    // A fresh window means a fresh payment result - a dismissal of THIS window may be a cancellation.
    this.paymentAttempted = false;

    // 'modal.ondismiss' below is the usual way to learn that the window was closed; this notices it as
    // well, because that callback is not raised by every way the window can go away - and a customer
    // whose window is gone while the button still says 'Processing...' has no way out of the page.
    this.windowWatcher = new PaymentWindowWatcher(
      this.zone,
      () => this.paymentAttempted || this.paymentInFlight || this.orderSuccess,
      () => this.onPaymentWindowDismissed(orderData.OrderId)
    );

    const options = {
      key: orderData.RazorpayKey,
      amount: orderData.Amount * 100, // Amount in paise
      currency: 'INR',
      name: 'Homecuties',
      description: `Order #${orderData.OrderNumber}`,
      order_id: orderData.RazorpayOrderId,
      prefill: {
        name: this.authService.getCurrentCustomer()?.FirstName || '',
        email: this.contactEmail(),
        contact: this.contactPhone()
      },
      theme: {
        color: '#d4a373'
      },
      // Every one of these callbacks is called by Razorpay's own script, outside the Angular zone,
      // where a state change does not start change detection. Hence 'zone.run': without it the page
      // keeps showing 'Processing...' although the payment is long over, which is exactly the bug this
      // page must never have.
      handler: (paymentResponse: any) => this.zone.run(() => {
        // Payment successful - verify on backend
        this.paymentAttempted = true;
        this.paymentInFlight = true;
        this.verifyPayment(orderData.OrderId, paymentResponse);
      }),
      modal: {
        // Razorpay also reports a dismissal when it closes the window after a successful payment, so
        // 'onPaymentWindowDismissed' itself only acts while no payment result has arrived.
        ondismiss: () => this.onPaymentWindowDismissed(orderData.OrderId)
      }
    };

    // The checkout widget is loaded from Razorpay's CDN (index.html); when a network policy blocks it
    // the button would otherwise spin on 'Processing...' for ever.
    if (typeof Razorpay === 'undefined') {
      this.failOrder(
        orderData.OrderId,
        'The payment window could not be opened. Please check your connection and try again.'
      );
      return;
    }

    try {
      const razorpay = new Razorpay(options);
      razorpay.on('payment.failed', (response: any) => this.zone.run(() => {
        // A 'failed' event is not always the last word (the money can still be captured), so the
        // backend is asked to check the payment before the button is released.
        this.paymentAttempted = true;
        this.paymentInFlight = true;
        this.syncPayment(
          orderData.OrderId,
          `Payment failed: ${response.error?.description || 'Please try again.'}`);
      }));

      this.windowWatcher.start();
      razorpay.open();
    } catch (err) {
      console.error(err);
      this.failOrder(
        orderData.OrderId,
        'The payment window could not be opened. Please refresh the page and try again.'
      );
    }
  }

  /**
   * The customer is back on the page and the payment window is gone without a result: it was closed
   * (reported by Razorpay's own 'ondismiss', or noticed by the window watch). The order is not lost -
   * it is saved as Pending, and the panel that this raises says so and offers the two ways forward
   * ('Pay now' right here, or 'My Orders').
   *
   * Both signals can arrive for the same closure, and a payment result may already be on its way, so
   * this only ever acts once and never while a result is still being handled.
   */
  private onPaymentWindowDismissed(orderId: number): void {
    this.windowWatcher?.stop();

    if (this.paymentAttempted || this.paymentInFlight || this.orderSuccess) {
      return;
    }

    this.placingOrder = false;

    // Nothing more is expected from the gateway for this attempt, so the customer is not sent back to
    // the page to be told 'checking your payment' the next time the cart happens to be empty.
    this.clearPendingOrder();

    this.showPendingOrder(
      orderId,
      `Your payment was not completed, so nothing has been charged. ${this.orderNumberFor(orderId)} has been saved ` +
      'as Pending - the items are still reserved for you.');
  }

  /**
   * Puts the 'payment not completed' panel on the page for an order that exists but has not been paid
   * for - the answer to 'I cancelled the payment and nothing happened': the customer is told the state
   * their order is really in, and is not left with an order only the shop can see.
   */
  private showPendingOrder(orderId: number, message: string): void {
    this.pendingOrderId = orderId;
    this.pendingOrderMessage = message;
    this.paymentNotCompleted = true;
    this.error = '';
  }

  /**
   * Pays for the order the panel is about ('Pay now'), which is what turns a cancelled or declined
   * payment into a second chance instead of a support call: the order and its reserved items stay as
   * they are, only the payment is started again.
   *
   * Nothing about that payment is decided here: the backend settles any earlier attempt at Razorpay
   * first, so an order whose money was taken is only confirmed and the customer is never charged twice.
   */
  payPendingOrder(): void {
    if (this.payingPendingOrder || this.pendingOrderId <= 0) {
      return;
    }

    const orderId = this.pendingOrderId;
    this.payingPendingOrder = true;
    this.error = '';

    this.paymentService.retryPayment(orderId).subscribe({
      next: (orderData: CreateOrderResponse) => {
        this.payingPendingOrder = false;

        if (orderData.Result === 1) {
          this.paymentNotCompleted = false;
          this.pendingOrderMessage = '';
          this.openRazorpayCheckout(orderData);
          return;
        }

        // The backend checks the earlier attempt at Razorpay before starting a new one. When that
        // check found the money, the order has just been confirmed - good news, not a failure.
        if (orderData.AlreadySettled) {
          this.completeOrder(orderId);
          return;
        }

        // The order cannot be paid for right now (it was cancelled meanwhile, the gateway is down, ...).
        // The panel stays and says why - 'My Orders' is still one click away.
        this.showPendingOrder(
          orderId,
          orderData.Messages?.join(' ') || 'We could not start the payment for this order.');
      },
      error: (err) => {
        console.error(err);
        this.payingPendingOrder = false;
        this.showPendingOrder(
          orderId,
          'We could not reach the server to start the payment. You have not been charged - please try again.');
      }
    });
  }

  private verifyPayment(orderId: number, paymentResponse: any): void {
    this.paymentService.verifyPayment({
      OrderId: orderId,
      RazorpayPaymentId: paymentResponse.razorpay_payment_id,
      RazorpayOrderId: paymentResponse.razorpay_order_id,
      RazorpaySignature: paymentResponse.razorpay_signature
    }).subscribe({
      next: (result) => {
        if (result.Result === 1) {
          this.completeOrder(orderId);
          return;
        }

        // Verification could not confirm the order (failed signature, capture not settled yet, ...).
        // Ask the backend to look the payment up at Razorpay before telling the customer anything:
        // the money may well have been taken.
        this.syncPayment(orderId, result.Messages?.join(', ') || 'Payment verification failed.');
      },
      error: (err) => {
        console.error(err);
        this.syncPayment(orderId, 'We could not confirm the payment with the server.');
      }
    });
  }

  /**
   * Safety net for every case in which the browser did not learn the payment result: the backend
   * checks the payment at Razorpay and confirms the order when the money was captured. Either way
   * the spinner is stopped - a payment is never left 'Processing...'.
   */
  private syncPayment(orderId: number, fallbackMessage?: string): void {
    this.placingOrder = true;

    this.paymentService.syncPayment(orderId).subscribe({
      next: (result: OrderActionResult) => {
        if (result.Result === 1) {
          this.completeOrder(orderId);
          return;
        }

        this.failOrder(orderId, result.Messages?.join(' ') || fallbackMessage || 'The payment could not be confirmed.');
      },
      error: (err) => {
        console.error(err);
        this.failOrder(
          orderId,
          `${fallbackMessage || 'We could not check the payment.'} Order ${this.orderNumberFor(orderId)} is saved in 'My Orders', ` +
          'where you can check its status, pay for it or cancel it.'
        );
      }
    });
  }

  private completeOrder(orderId: number): void {
    this.orderSuccess = true;
    this.orderNumber = this.orderNumberFor(orderId);
    this.placingOrder = false;
    this.payingPendingOrder = false;
    this.paymentInFlight = false;
    this.paymentNotCompleted = false;
    this.pendingOrderId = 0;
    this.pendingOrderMessage = '';
    this.windowWatcher?.stop();
    this.error = '';
    this.clearPendingOrder();
    this.cartService.clearLocalCart();
  }

  /**
   * The payment did not go through: a declined card, a window that never opened, a payment that could
   * not be confirmed. The order itself exists and is still waiting for its money, so the customer is
   * shown exactly that - the 'payment not completed' panel, with the reason and a way to pay the order
   * - instead of a message about an order they cannot see.
   */
  private failOrder(orderId: number, message: string): void {
    this.placingOrder = false;
    this.payingPendingOrder = false;
    this.paymentInFlight = false;
    this.lastOrderId = orderId;
    this.windowWatcher?.stop();
    this.clearPendingOrder();

    this.showPendingOrder(orderId, message);
  }

  private orderNumberFor(orderId: number): string {
    return `HC${orderId.toString().padStart(6, '0')}`;
  }

  continueShopping(): void {
    this.router.navigate(['/shop']);
  }

  viewMyOrders(): void {
    this.router.navigate(['/my-orders']);
  }
}
