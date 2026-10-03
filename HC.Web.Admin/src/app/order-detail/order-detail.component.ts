import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Observable } from 'rxjs';
import { AdminService } from '../services/admin.service';
import { AuthService } from '../services/auth.service';
import { AdminOrderDetail, AdminResult, AdminSaveOrderShipmentRequest, AdminOrderShipment, AdminOrderShipmentItem, AdminOrderItemMoney, AdminShipmentProvider, AdminOrderReturn } from '../models/admin.model';

/**
 * Everything the shop team needs for one order: what was bought (one row per physical unit, with its
 * SKU), where it goes, what the customer paid and the full status history - plus the status workflow,
 * which keeps the stock in step with the order (dispatch, deliver, cancel).
 *
 * A paid order the customer cancelled is not refunded by that cancellation: it only asks for the
 * money back, and the shop team approves it here (see the Refund card), which is what sends it to
 * Razorpay - or records a refund that was made by hand in the Razorpay dashboard.
 */
@Component({
  selector: 'app-order-detail',
  templateUrl: './order-detail.component.html',
  styleUrls: ['./order-detail.component.scss'],
  standalone: false
})
export class OrderDetailComponent implements OnInit {
  order: AdminOrderDetail | null = null;
  isLoading = true;
  loadError = '';

  /** Orders.OrderStatusID = 5 (Cancelled) - a cancellation needs a reason for the customer. */
  readonly cancelledStatusId = 5;

  /** Orders.OrderStatusID = 3 (Shipped) - dispatching an order also books its parcel (see below). */
  readonly shippedStatusId = 3;

  // Status workflow
  newStatusId = 0;
  statusComment = '';

  // Dispatching is also when the parcel becomes real, so a Shipped move asks for it: the provider the
  // parcel was booked with, the consignment number that provider gave, and what it billed for the parcel
  // (for the books, and never shown to the customer). The server records them against the order with the
  // move, and every later courier call is made through that provider.
  statusProvider = '';
  statusAwb = '';
  statusCourier = '';
  /** What the courier billed for this parcel (₹), or null while the team has not recorded it. */
  statusFreight: number | null = null;

  isSavingStatus = false;
  actionMessage = '';
  actionError = false;

  // Refund of a cancelled paid order: the cancellation only asked for the money back, so the shop
  // team approves it here (or records a refund it made by hand in the Razorpay dashboard).
  refundComment = '';
  isSavingRefund = false;
  refundMessage = '';
  refundError = false;

  // Shipment: the parcel the team booked in the provider's own panel. Recording it here is only this
  // side's record of it - booking is not dispatching - so 'Track now' is what asks the courier and
  // follows what it says, and that is what moves the order (and its units) along.
  shipmentProviders: AdminShipmentProvider[] = [];
  providerLoadError = '';
  /** Registry name of the provider the parcel is booked with; blank means the shop's default. */
  shipmentProvider = '';
  shipmentAwb = '';
  shipmentCourier = '';
  shipmentTrackingUrl = '';
  /** What the courier billed for this parcel (₹), or null while the team has not recorded it. */
  shipmentFreight: number | null = null;
  isSavingShipment = false;
  isTrackingShipment = false;
  shipmentMessage = '';
  shipmentError = false;

  /**
   * Which of the order's parcels the card is working on ('OrderShipments.ShipmentID'): the one whose details the
   * card describes and whose form is filled in. Null means the order's first parcel - what the card has always
   * meant by 'the parcel' - and the team picks another from the parcel list when an order went out in more than
   * one.
   */
  selectedShipmentId: number | null = null;

  /**
   * True while the team is booking a parcel that is NOT on the order yet: the form is empty and saving it ADDS a
   * parcel instead of correcting one. This is how an order goes out in more than one parcel - book the first, then
   * 'Record another parcel' for the next consignment.
   */
  bookingNewParcel = false;

  /**
   * What the parcel in the form carries, one entry per SKU of the order - the card's picker. Filled from the parcel
   * being corrected and sent with every save, because a parcel's own courier bill is charged to these units for the
   * books (see HC.Business.OrderItemMoneyWriter).
   */
  shipmentItems: AdminOrderShipmentItem[] = [];

  // Return: a delivered order's return (asked for by the customer), or one the courier's own report raised
  // (a refusal, or a parcel that could not be delivered). The shop team answers it here - approving means
  // the parcel is coming back, so the pickup is recorded on the same card with 'direction: Reverse' - and
  // the order itself only becomes 'Returned', its units on the shelf and its money owed back, when the
  // return is closed with the parcel in the shop (see HC.Business.OrderReturnFlow).
  returnComment = '';
  isDecidingReturn = false;
  returnMessage = '';
  returnError = false;

  /** The pickup coming back: its own AWB, courier and freight, recorded against the return. */
  returnProvider = '';
  returnAwb = '';
  returnCourier = '';
  /** What the courier billed for the pickup (₹), or null while nobody has recorded it. */
  returnFreight: number | null = null;
  isSavingReturnPickup = false;

  /**
   * The optional note the 'parcel received' and 'close return' steps carry. It goes on the order's history, which
   * the customer reads in 'My Orders' - unlike the answer's note, which is required and is what they are told.
   */
  returnStepComment = '';

  /** The units the inspection found broken, by SKU: one of three identical tops can be torn and the others fine. */
  damagedSkus: string[] = [];

  /** What the inspection found, in the team's own words - kept on the return beside who looked and when. */
  returnInspectionComment = '';

  /** One flag per step, so a slow save cannot be started twice from the same card. */
  isMarkingReturnReceived = false;
  isSavingReturnInspection = false;
  isClosingReturn = false;

  constructor(
    private adminService: AdminService,
    private authService: AuthService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    const orderId = Number(this.route.snapshot.paramMap.get('id'));
    if (!orderId) {
      this.router.navigate(['/orders']);
      return;
    }

    this.loadOrder(orderId);
    this.loadShipmentProviders();
  }

  loadOrder(orderId: number): void {
    this.isLoading = true;
    this.loadError = '';

    this.adminService.getOrderDetail(orderId).subscribe({
      next: (data) => {
        this.order = data;
        this.newStatusId = 0;
        this.statusComment = '';
        this.isLoading = false;

        // The parcel already on the order is put back into the form, so correcting an AWB (or adding
        // the courier) starts from what is recorded instead of from an empty box.
        this.prefillShipmentForm();

        // The return's own pickup, when one has been recorded, is put back the same way.
        this.prefillReturnForm();
      },
      error: () => {
        this.loadError = 'The order could not be loaded. Please go back to the order list and try again.';
        this.isLoading = false;
      }
    });
  }

  reload(): void {
    if (this.order) {
      this.loadOrder(this.order.orderId);
    }
  }

  /** Colour of the status chip: 1 Pending, 2 Confirmed, 3 Shipped, 4 Delivered, 5 Cancelled, 6 Returned. */
  getStatusBadgeClass(statusId: number): string {
    switch (statusId) {
      case 1: return 'badge badge-pending';
      case 2: return 'badge badge-confirmed';
      case 3: return 'badge badge-shipped';
      case 4: return 'badge badge-delivered';
      case 5: return 'badge badge-cancelled';
      case 6: return 'badge badge-cancelled';
      default: return 'badge';
    }
  }

  getPaymentBadgeClass(isPaid: boolean): string {
    return isPaid ? 'badge badge-paid' : 'badge badge-unpaid';
  }

  /**
   * True while the Refund card has something to say: the money is still owed (the customer's
   * cancellation is waiting for approval, or Razorpay refused a refund) - or it has already gone back,
   * so the card stays on the order as the record of it.
   */
  get showRefundCard(): boolean {
    return !!this.order && (this.order.refundPending || this.hasRefundRecord);
  }

  /** True when a refund of this order is already recorded (approved here, or made by hand). */
  get hasRefundRecord(): boolean {
    const order = this.order;
    if (!order || order.refundPending) {
      return false;
    }

    return !!order.refundedOn || !!order.refundId || (order.refundStatus ?? '').toLowerCase() === 'manual';
  }

  /** Plain reading of the refund state the server wrote on the payment row (see RazorpayRefunds). */
  getRefundStatusText(): string {
    switch ((this.order?.refundStatus ?? '').toLowerCase()) {
      case 'processed': return 'Refunded';
      case 'pending': return 'Refund sent - on its way back to the customer';
      case 'manual': return 'Refunded by hand in the Razorpay dashboard';
      case 'failed': return 'Razorpay refused the refund - the money is still owed';
      default: return this.order?.refundStatus || 'Not started';
    }
  }

  /**
   * Sends the refund the cancelled order is waiting for. What happened is decided on the server (a
   * refusal from Razorpay leaves the money owed; a request with no captured payment on record has to be
   * refunded in the Razorpay dashboard and closed with 'Mark refunded'), so its message is shown as it
   * is and the order is re-read to show the result.
   */
  approveRefund(): void {
    if (!this.order || this.isSavingRefund) {
      return;
    }

    const userId = this.authService.getUser()?.userId ?? 0;
    if (!userId) {
      this.refundMessage = 'Your admin session has no user id - please sign in again.';
      this.refundError = true;
      return;
    }

    this.runRefundAction(
      () => this.adminService.approveOrderRefund(this.order!.orderId, userId),
      'The refund could not be sent. Nothing was changed - please try again.'
    );
  }

  /** Records a refund the shop team already made in the Razorpay dashboard, so the order stops owing it. */
  markRefunded(): void {
    if (!this.order || this.isSavingRefund) {
      return;
    }

    const userId = this.authService.getUser()?.userId ?? 0;
    if (!userId) {
      this.refundMessage = 'Your admin session has no user id - please sign in again.';
      this.refundError = true;
      return;
    }

    this.runRefundAction(
      () => this.adminService.markOrderRefunded(
        this.order!.orderId,
        { comment: this.refundComment.trim() },
        userId
      ),
      'The refund could not be recorded. Nothing was changed - please try again.'
    );
  }

  /** Runs one of the two refund calls and shows the server's own wording, success or refusal. */
  private runRefundAction(action: () => Observable<AdminResult>, failureMessage: string): void {
    this.isSavingRefund = true;
    this.refundMessage = '';
    this.refundError = false;

    action().subscribe({
      next: (result) => {
        this.isSavingRefund = false;
        this.refundError = result.result !== 1;
        this.refundMessage = (result.messages || []).join(' ');

        if (result.result === 1) {
          this.refundComment = '';
          this.loadOrder(this.order!.orderId);
        }
      },
      error: () => {
        this.isSavingRefund = false;
        this.refundError = true;
        this.refundMessage = failureMessage;
      }
    });
  }

  /**
   * The shipping providers this shop is registered with, for the provider picker of the Shipment card
   * and of the dispatch form. The list comes from the API's registry, so it cannot drift from what is
   * really wired up; an entry whose 'configured' is false (registered without credentials) is still
   * offered - a parcel really did go out with it - but is marked 'not set up', because its tracking
   * cannot be fetched until its credentials are on the server.
   */
  private loadShipmentProviders(): void {
    this.providerLoadError = '';

    this.adminService.getShipmentProviders().subscribe({
      next: (providers) => {
        this.shipmentProviders = providers || [];

        // Prefill with the default the shop ships with, so the usual case is a single AWB to type.
        const preferred = this.shipmentProviders.find(p => p.configured && p.isDefault)
          || this.shipmentProviders.find(p => p.configured);

        if (!this.shipmentProvider && preferred) {
          this.shipmentProvider = preferred.name;
        }

        // The dispatch form offers the same providers and starts from the same default, so the two
        // forms cannot disagree about what this shop ships with.
        if (!this.statusProvider && preferred) {
          this.statusProvider = preferred.name;
        }
      },
      error: () => {
        this.providerLoadError = 'The shipping providers could not be listed. A parcel can still be recorded against the provider the shop defaults to.';
      }
    });
  }

  /**
   * The provider as the API described it, or undefined while the list has not arrived or the name is not
   * one this shop is set up with. Both pickers offer exactly that list, so a capability is read from the
   * entry the team is looking at rather than guessed here.
   */
  private providerInfo(name: string): AdminShipmentProvider | undefined {
    return this.shipmentProviders.find(p => p.name === name);
  }

  /**
   * True when the provider chosen has no panel to take an AWB from, so the system mints the parcel's
   * reference itself (the shop's own delivery service): the AWB box is then optional, and a blank one
   * means 'give this parcel a reference' rather than 'the number has not been typed in yet'.
   *
   * A blank choice means 'the provider this shop defaults to', so the default's own answer is used for it
   * - otherwise leaving the picker on its default would quietly make the AWB required again.
   */
  private providerMintsAwb(name: string): boolean {
    const chosen = name ? this.providerInfo(name) : this.shipmentProviders.find(p => p.isDefault);

    return !!chosen?.awbGeneratedBySystem;
  }

  /** The same question for the Shipment card's provider picker. */
  get shipmentProviderMintsAwb(): boolean {
    return this.providerMintsAwb(this.shipmentProvider);
  }

  /** The same question for the dispatch form's provider picker (the Shipped move). */
  get statusProviderMintsAwb(): boolean {
    return this.providerMintsAwb(this.statusProvider);
  }

  /** Puts the parcel recorded on the order back into the form (an empty form while there is none). */
  private prefillShipmentForm(): void {
    const shipment = this.shipment;

    this.shipmentAwb = shipment?.awbNumber || '';
    this.shipmentCourier = shipment?.courierName || '';
    this.shipmentTrackingUrl = shipment?.trackingUrl || '';
    this.shipmentFreight = shipment?.freightCharge ?? null;
    this.shipmentItems = (shipment?.items || []).map(item => ({ sku: item.sku, quantity: item.quantity }));

    // The dispatch form is for the same parcel, so it starts from what is already on the order: an
    // order shipped before this form existed (or one whose AWB was corrected on the card) is not asked
    // for it twice.
    this.statusAwb = shipment?.awbNumber || '';
    this.statusCourier = shipment?.courierName || '';
    this.statusFreight = shipment?.freightCharge ?? null;

    if (shipment?.provider) {
      this.shipmentProvider = shipment.provider;
      this.statusProvider = shipment.provider;
    }
  }

  /**
   * Every parcel of this order, forward legs first - what the Shipment card lists. Falls back to the single parcel
   * the API has always carried, so a card read from an older server still shows its parcel.
   */
  get parcels(): AdminOrderShipment[] {
    const all = this.order?.shipments || [];

    if (all.length) {
      return all;
    }

    return this.order?.shipment ? [this.order.shipment] : [];
  }

  /**
   * The parcel the card is describing and editing: the one the team picked, else the order's first - and null while
   * the team is booking a NEW one, which is what keeps a fresh form empty instead of filling itself back in from the
   * first parcel.
   */
  get shipment(): AdminOrderShipment | null {
    if (this.bookingNewParcel) {
      return null;
    }

    if (this.selectedShipmentId) {
      const chosen = this.parcels.find(parcel => parcel.shipmentId === this.selectedShipmentId);

      if (chosen) {
        return chosen;
      }
    }

    return this.order?.shipment ?? this.parcels[0] ?? null;
  }

  /** True while the parcel being described is the order's return pickup rather than a parcel that went out. */
  get shipmentIsReverse(): boolean {
    return this.shipment?.isReverse === true || this.shipment?.direction === 'Reverse';
  }

  /** Shows another parcel's details in the card (and fills the form from it). */
  selectParcel(parcel: AdminOrderShipment): void {
    this.selectedShipmentId = parcel.shipmentId;
    this.bookingNewParcel = false;
    this.shipmentMessage = '';
    this.shipmentError = false;
    this.prefillShipmentForm();
  }

  /**
   * Empties the form for a parcel that is not on the order yet: the next consignment of the same order. The provider
   * is kept, because a shop that booked one parcel with one provider books the next with the same one.
   */
  bookAnotherParcel(): void {
    this.selectedShipmentId = null;
    this.bookingNewParcel = true;
    this.shipmentMessage = '';
    this.shipmentError = false;
    this.shipmentAwb = '';
    this.shipmentCourier = '';
    this.shipmentTrackingUrl = '';
    this.shipmentFreight = null;
    this.shipmentItems = [];
  }

  /** True once an AWB is on the parcel being described; only then does the card say anything about shipping. */
  get hasShipment(): boolean {
    return !!this.shipment && this.shipment.hasShipment;
  }

  /**
   * True once the courier is finished with the parcel (delivered, on its way back, or called off).
   * 'Track now' is pointless from there on, so it is switched off rather than left to fail.
   */
  get shipmentClosed(): boolean {
    return !!this.shipment?.closed;
  }

  /**
   * True when the provider this parcel was recorded with has a courier behind it that can be asked where
   * the parcel is. False for a service the shop arranges itself (a parcel handed over in person, or given
   * to a local courier dealt with by phone): there is nothing to look up, so the card says so and leaves
   * 'Track now' out, and the order is moved along from the status list instead. A parcel read from a
   * server that does not report this is treated as having a courier, which is the safer mistake - the
   * lookup then answers for itself.
   */
  get shipmentHasCourier(): boolean {
    return this.shipment?.reportsTracking !== false;
  }

  /**
   * The order's own units, one entry per SKU with how many of it the order holds: an order line is one row per
   * physical unit, so the rows ARE the count - the same truth the server checks a parcel's contents against.
   */
  get orderUnits(): { sku: string; productTitle: string; units: number }[] {
    const grouped = new Map<string, { sku: string; productTitle: string; units: number }>();

    for (const line of this.order?.items || []) {
      const known = grouped.get(line.sku);

      if (known) {
        known.units += 1;
      } else {
        grouped.set(line.sku, { sku: line.sku, productTitle: line.productTitle || line.productName, units: 1 });
      }
    }

    return Array.from(grouped.values());
  }

  /**
   * How many units of a SKU the order's OTHER parcels already carry. The parcel being edited is left out, because
   * its own units are the ones the form is holding: what is left to give it is the order's units less everything
   * that is already in a different parcel.
   */
  unitsCarriedElsewhere(sku: string): number {
    const editing = this.bookingNewParcel ? null : this.shipment?.shipmentId ?? null;

    return this.parcels
      .filter(parcel => parcel.shipmentId !== editing && !parcel.isReverse)
      .reduce((sum, parcel) => sum + (parcel.items || [])
        .filter(item => item.sku === sku)
        .reduce((carried, item) => carried + item.quantity, 0), 0);
  }

  /** How many units of a SKU this parcel may still carry - none once the order's other parcels have taken them. */
  unitsLeftFor(sku: string): number {
    const onOrder = this.orderUnits.find(unit => unit.sku === sku);

    return Math.max(0, (onOrder?.units ?? 0) - this.unitsCarriedElsewhere(sku));
  }

  /** How many units of a SKU the parcel in the form says it carries (0 while it says nothing about it). */
  carriedQuantity(sku: string): number {
    return this.shipmentItems.find(item => item.sku === sku)?.quantity ?? 0;
  }

  /** True when the parcel in the form says it carries this SKU. */
  isCarried(sku: string): boolean {
    return this.carriedQuantity(sku) > 0;
  }

  /**
   * Ticks or unticks a SKU of the parcel in the form: unticking takes it out of the parcel, ticking gives it one
   * unit - which the count beside it can then be raised from.
   */
  toggleCarried(sku: string, carried: boolean): void {
    if (carried) {
      this.setCarried(sku, 1);
    } else {
      this.shipmentItems = this.shipmentItems.filter(item => item.sku !== sku);
    }
  }

  /**
   * Puts the number of units of a SKU in this parcel, kept inside what the order really has left - the server refuses
   * more, and a wrong number here would be a delivery nobody can account for. Zero takes the SKU out of the parcel.
   */
  setCarried(sku: string, quantity: number): void {
    const wanted = Math.min(Math.max(0, Math.floor(Number(quantity) || 0)), this.unitsLeftFor(sku));
    const rest = this.shipmentItems.filter(item => item.sku !== sku);

    this.shipmentItems = wanted > 0
      ? [...rest, { sku, quantity: wanted }].sort((left, right) => left.sku.localeCompare(right.sku))
      : rest;
  }

  /** What the parcel in the form carries, as one sentence ('2 x HC-1042, 1 x HC-7'). */
  get shipmentItemsText(): string {
    return this.shipmentItems.map(item => `${item.quantity} x ${item.sku}`).join(', ');
  }

  /** What is left to dispatch: the order's units less what its parcels already carry, per SKU. */
  get unitsStillToShip(): { sku: string; units: number }[] {
    return this.orderUnits
      .map(unit => ({ sku: unit.sku, units: unit.units - this.unitsCarriedElsewhere(unit.sku) }))
      .filter(unit => unit.units > 0);
  }

  /**
   * Colour of the parcel badge, from the step the server worked out for the courier's status (see
   * HC.Business.ShipmentStatusFlow): the parcel on its way, or the end of it.
   */
  getShipmentStageBadgeClass(stage?: string): string {
    switch (stage) {
      case 'Booked': return 'badge badge-shipment-booked';
      case 'InTransit': return 'badge badge-shipment-transit';
      case 'OutForDelivery': return 'badge badge-shipment-out';
      case 'Delivered': return 'badge badge-delivered';
      case 'Undelivered': return 'badge badge-shipment-undelivered';
      case 'Refused': return 'badge badge-shipment-undelivered';
      case 'Rto': return 'badge badge-shipment-return';
      case 'Cancelled': return 'badge badge-cancelled';
      default: return 'badge';
    }
  }

  /**
   * Where the parcel is, in the clearest words available: the shop's own wording for what the courier
   * said, and - when the shop has none for it - the courier's own sentence. Read the same way as the
   * customer's page, so both screens describe one parcel in one wording.
   */
  getShipmentStatusText(): string {
    const shipment = this.shipment;
    return shipment ? (shipment.status || shipment.providerStatus) : '';
  }

  /**
   * What the card says where the parcel is: the shop's own wording for what the courier said, the
   * courier's own sentence when the shop has none, or - when there is neither - why there is nothing. A
   * parcel with no courier behind it is never going to report anything, which is not the same thing as one
   * that has simply not been asked yet, so the two are not said in the same words.
   */
  getShipmentStatusLine(): string {
    return this.getShipmentStatusText() ||
      (this.shipmentHasCourier ? 'Not asked the courier yet' : "Carried by the shop's own arrangement");
  }

  /**
   * The courier's latest sentence, shown only when the shop has wording of its own for it - otherwise
   * the line above is already that sentence and repeating it would read as two updates.
   */
  getShipmentCourierText(): string {
    const shipment = this.shipment;
    if (!shipment || !shipment.lastStatusText) {
      return '';
    }

    return shipment.lastStatusText === shipment.providerStatus ? '' : shipment.lastStatusText;
  }

  /** The return this order carries, or null while it has never had one. */
  get orderReturn(): AdminOrderReturn | null {
    return this.order?.return ?? null;
  }

  /** True while the shop team can still answer the return - exactly when the card offers its two buttons. */
  get canDecideReturn(): boolean {
    return !!this.orderReturn?.canDecide;
  }

  /** True while the parcel can be booked back in - exactly when the card offers 'Parcel received'. */
  get canMarkReceivedReturn(): boolean {
    return !!this.orderReturn?.canMarkReceived;
  }

  /** True while the returned parcel can be looked over - the inspection, before the return is closed. */
  get canInspectReturn(): boolean {
    return !!this.orderReturn?.canInspect;
  }

  /** True while the return can be closed - the step that makes the order 'Returned' and asks for the refund. */
  get canCloseReturn(): boolean {
    return !!this.orderReturn?.canClose;
  }

  /** True when the unit with this SKU is ticked as broken and will be written off when the inspection is saved. */
  isDamagedSku(sku: string): boolean {
    return this.damagedSkus.indexOf(sku) >= 0;
  }

  /** The pickup the return is coming back on, or null while none has been recorded. */
  get returnShipment(): AdminOrderShipment | null {
    return this.orderReturn?.shipment ?? null;
  }

  /**
   * True while the return is approved but nothing has been recorded about the parcel yet - when the card
   * offers the pickup form. A return whose parcel is already known (a courier carrying it back on its own
   * reports it on the tracking) simply shows it instead.
   */
  get returnPickupNeeded(): boolean {
    const current = this.orderReturn;

    return !!current && !this.returnShipment &&
      (current.status === 'Arranged' || current.status === 'Received');
  }

  /** The pickup form's provider picker, asked the same question as the Shipment card's. */
  get returnProviderMintsAwb(): boolean {
    return this.providerMintsAwb(this.returnProvider);
  }

  /** The return's state for the card's badge, in the shop team's words. */
  getReturnStatusText(): string {
    switch (this.orderReturn?.status) {
      case 'Requested': return 'Waiting for an answer';
      case 'Arranged': return 'Approved - parcel coming back';
      case 'Received': return 'Parcel received';
      case 'Closed': return 'Returned and closed';
      case 'Rejected': return 'Refused';
      case 'Withdrawn': return 'Taken back by the customer';
      default: return '';
    }
  }

  /**
   * Colour of the return badge, reusing the parcel colours so one card cannot read as two different things:
   * what is waiting for the team, what is on its way, and what is finished.
   */
  getReturnBadgeClass(): string {
    switch (this.orderReturn?.status) {
      case 'Requested': return 'badge badge-shipment-undelivered';
      case 'Arranged': return 'badge badge-shipment-return';
      case 'Received': return 'badge badge-shipment-transit';
      case 'Closed': return 'badge badge-delivered';
      case 'Rejected': return 'badge badge-cancelled';
      default: return 'badge';
    }
  }

  /**
   * Answers the return: approving it says the parcel is coming back (the pickup is booked on this card
   * next), refusing it leaves the order exactly as it was - either way the order, its units and its money
   * stay untouched until the return is closed with the parcel in the shop.
   *
   * The note is required because it is what the customer is told (the API refuses an empty one too), and
   * the order is re-read on success so the card, the history and the return all say the same thing.
   */
  decideReturn(approved: boolean): void {
    if (!this.order || this.isDecidingReturn) {
      return;
    }

    if (!this.returnComment.trim()) {
      this.returnMessage = approved
        ? 'Say why the return is approved - the customer is told this note.'
        : 'Say why the return is refused - the customer is told this note.';
      this.returnError = true;
      return;
    }

    const userId = this.authService.getUser()?.userId ?? 0;
    if (!userId) {
      this.returnMessage = 'Your admin session has no user id - please sign in again.';
      this.returnError = true;
      return;
    }

    this.isDecidingReturn = true;
    this.returnMessage = '';
    this.returnError = false;

    this.adminService.decideOrderReturn(this.order.orderId, approved, this.returnComment.trim(), userId).subscribe({
      next: (result) => {
        this.isDecidingReturn = false;
        this.returnError = result.result !== 1;
        this.returnMessage = (result.messages || []).join(' ');

        if (result.result === 1) {
          this.returnComment = '';
          this.loadOrder(this.order!.orderId);
        }
      },
      error: () => {
        this.isDecidingReturn = false;
        this.returnError = true;
        this.returnMessage = 'The return could not be answered. Nothing was saved - please try again.';
      }
    });
  }

  /**
   * Records the parcel the return is coming back on: the pickup the team booked with the courier, or the
   * courier's own return-to-origin. It is the same API call the Shipment card uses, told which leg it is
   * ('direction: Reverse'), so the return's AWB, courier, tracking link and freight are kept exactly like
   * the delivery's - and the parcel that went out is never touched by it.
   */
  recordReturnPickup(): void {
    if (!this.order || this.isSavingReturnPickup) {
      return;
    }

    if (!this.returnAwb.trim() && !this.returnProviderMintsAwb) {
      this.returnMessage = 'Type the AWB the courier gave the pickup.';
      this.returnError = true;
      return;
    }

    if (this.returnFreight !== null && this.returnFreight < 0) {
      this.returnMessage =
        'The freight charge cannot be negative - enter what the courier billed for this pickup, or leave it blank.';
      this.returnError = true;
      return;
    }

    const userId = this.authService.getUser()?.userId ?? 0;
    if (!userId) {
      this.returnMessage = 'Your admin session has no user id - please sign in again.';
      this.returnError = true;
      return;
    }

    const request: AdminSaveOrderShipmentRequest = {
      provider: this.returnProvider || null,
      awbNumber: this.returnAwb.trim(),
      courierName: this.returnCourier.trim() || null,
      freightCharge: this.returnFreight,

      // The one thing that makes this the return's own parcel rather than the delivery's.
      direction: 'Reverse'
    };

    this.isSavingReturnPickup = true;
    this.returnMessage = '';
    this.returnError = false;

    this.adminService.saveOrderShipment(this.order.orderId, request, userId).subscribe({
      next: (result) => {
        this.isSavingReturnPickup = false;
        this.returnError = result.result !== 1;
        this.returnMessage = (result.messages || []).join(' ');

        if (result.result === 1) {
          this.loadOrder(this.order!.orderId);
        }
      },
      error: () => {
        this.isSavingReturnPickup = false;
        this.returnError = true;
        this.returnMessage = 'The pickup could not be recorded. Nothing was saved - please try again.';
      }
    });
  }

  /**
   * 'Parcel received': the parcel is physically back in the shop. The return becomes 'Received' and its units come
   * off the delivery pools - off sale until the return is closed - while the order and the money are left exactly
   * as they are, because both move at the close. The optional note goes on the order's history, which the customer
   * reads in 'My Orders', and the order is re-read on success so the card, the history and the return agree.
   */
  markReturnReceived(): void {
    if (!this.order || this.isMarkingReturnReceived) {
      return;
    }

    const userId = this.authService.getUser()?.userId ?? 0;
    if (!userId) {
      this.returnMessage = 'Your admin session has no user id - please sign in again.';
      this.returnError = true;
      return;
    }

    this.isMarkingReturnReceived = true;
    this.returnMessage = '';
    this.returnError = false;

    this.adminService.markOrderReturnReceived(this.order.orderId, this.returnStepComment.trim(), userId).subscribe({
      next: (result) => {
        this.isMarkingReturnReceived = false;
        this.returnError = result.result !== 1;
        this.returnMessage = (result.messages || []).join(' ');

        if (result.result === 1) {
          this.returnStepComment = '';
          this.loadOrder(this.order!.orderId);
        }
      },
      error: () => {
        this.isMarkingReturnReceived = false;
        this.returnError = true;
        this.returnMessage = 'The parcel could not be booked back in. Nothing was saved - please try again.';
      }
    });
  }

  /** Ticks or unticks one unit for the inspection - per unit, because one of three identical tops can be torn. */
  toggleDamagedSku(sku: string): void {
    this.damagedSkus = this.isDamagedSku(sku)
      ? this.damagedSkus.filter(current => current !== sku)
      : [...this.damagedSkus, sku];
  }

  /**
   * Saves the inspection: every ticked unit is written off - out of every sellable pool for good, so closing the
   * return can never put it back on sale - and the note is kept on the return with who looked and when. Saving
   * with nothing ticked is still an inspection: 'we looked and found nothing' is worth having on the record, which
   * is exactly what the server writes down.
   */
  inspectReturn(): void {
    if (!this.order || this.isSavingReturnInspection) {
      return;
    }

    const userId = this.authService.getUser()?.userId ?? 0;
    if (!userId) {
      this.returnMessage = 'Your admin session has no user id - please sign in again.';
      this.returnError = true;
      return;
    }

    this.isSavingReturnInspection = true;
    this.returnMessage = '';
    this.returnError = false;

    this.adminService
      .markOrderReturnUnitsDamaged(
        this.order.orderId, this.damagedSkus, this.returnInspectionComment.trim(), userId)
      .subscribe({
        next: (result) => {
          this.isSavingReturnInspection = false;
          this.returnError = result.result !== 1;
          this.returnMessage = (result.messages || []).join(' ');

          if (result.result === 1) {
            this.damagedSkus = [];
            this.returnInspectionComment = '';
            this.loadOrder(this.order!.orderId);
          }
        },
        error: () => {
          this.isSavingReturnInspection = false;
          this.returnError = true;
          this.returnMessage = 'The inspection could not be saved. Nothing was written off - please try again.';
        }
      });
  }

  /**
   * Closes the return - the step that settles it: the order becomes 'Returned', every unit the return brought back
   * goes on sale again (the written-off ones stay written off) and the refund of what the customer paid is asked
   * for. The Refund card on this same order is where that money is actually sent, so nothing leaves for the
   * gateway here. Only a return whose parcel is back in the shop can be closed, and the card only offers this then.
   */
  closeReturn(): void {
    if (!this.order || this.isClosingReturn) {
      return;
    }

    const userId = this.authService.getUser()?.userId ?? 0;
    if (!userId) {
      this.returnMessage = 'Your admin session has no user id - please sign in again.';
      this.returnError = true;
      return;
    }

    this.isClosingReturn = true;
    this.returnMessage = '';
    this.returnError = false;

    this.adminService.closeOrderReturn(this.order.orderId, this.returnStepComment.trim(), userId).subscribe({
      next: (result) => {
        this.isClosingReturn = false;
        this.returnError = result.result !== 1;
        this.returnMessage = (result.messages || []).join(' ');

        if (result.result === 1) {
          this.returnStepComment = '';
          this.loadOrder(this.order!.orderId);
        }
      },
      error: () => {
        this.isClosingReturn = false;
        this.returnError = true;
        this.returnMessage = 'The return could not be closed. Nothing was saved - please try again.';
      }
    });
  }

  /** Puts the return's own parcel back into the pickup form (an empty form while there is none). */
  private prefillReturnForm(): void {
    const shipment = this.returnShipment;

    this.returnAwb = shipment?.awbNumber || '';
    this.returnCourier = shipment?.courierName || '';
    this.returnFreight = shipment?.freightCharge ?? null;
  }

  /**
   * Records (or corrects) the parcel of this order: the AWB the team booked in the provider's panel (or
   * the blank box that asks the server to mint one, for a provider that gives none), with the courier, the
   * tracking link and what that courier billed for the parcel when they have them. This never moves the
   * order - booking a parcel is not dispatching it - so the order only follows the courier's own reports
   * ('Track now'). The message coming back is the server's own wording, and the order is re-read on
   * success so the history shows the reference it wrote down.
   */
  recordShipment(): void {
    if (!this.order || this.isSavingShipment) {
      return;
    }

    // The AWB is the courier's own number, so a blank box is a number that has not been typed in yet -
    // except for a provider that gives the parcel a reference of its own, where blank is exactly the point
    // (the server mints it). The server refuses a blank against any other provider, so this only saves a
    // round trip.
    if (!this.shipmentAwb.trim() && !this.shipmentProviderMintsAwb) {
      this.shipmentMessage = 'Type the AWB the provider gave this parcel.';
      this.shipmentError = true;
      return;
    }

    // What the courier billed is optional (the panel shows it when the parcel is billed, which can be
    // later), but a negative charge is always a keystroke slip.
    if (this.shipmentFreight !== null && this.shipmentFreight < 0) {
      this.shipmentMessage =
        'The freight charge cannot be negative - enter what the courier billed for this parcel, or leave it blank.';
      this.shipmentError = true;
      return;
    }

    // A parcel going out has to say what is in it: its own courier bill is charged to those units for the books, and
    // a parcel nobody can say what it carried is a cost the shop cannot put anywhere. (The server refuses it in the
    // same words - this only saves a round trip.) The order's units are picked from the Items list above.
    if (this.shipmentItems.length === 0) {
      this.shipmentMessage =
        'Pick the units this parcel carries. A parcel going out with nothing in it is one nobody can bill for - ' +
        'tick the goods above and set how many of each are in it.';
      this.shipmentError = true;
      return;
    }

    const overrun = this.shipmentItems.find(item => item.quantity > this.unitsLeftFor(item.sku));
    if (overrun) {
      this.shipmentMessage =
        `This order only has ${this.unitsLeftFor(overrun.sku)} of '${overrun.sku}' left for this parcel - ` +
        'some of them are already in another parcel.';
      this.shipmentError = true;
      return;
    }

    const userId = this.authService.getUser()?.userId ?? 0;
    if (!userId) {
      this.shipmentMessage = 'Your admin session has no user id - please sign in again.';
      this.shipmentError = true;
      return;
    }

    const request: AdminSaveOrderShipmentRequest = {
      // Which parcel this is: the one the card is showing, so correcting a second parcel of the same order corrects
      // THAT one. Null while booking a new one, which is what makes the server treat a number it has not seen as a
      // new consignment rather than as a correction of the first parcel (see IShipmentTrackingService.SaveAsync).
      shipmentId: this.bookingNewParcel ? null : this.shipment?.shipmentId ?? null,

      // Blank means 'the provider the shop defaults to', which is what a one-aggregator shop leaves it as.
      provider: this.shipmentProvider || null,
      awbNumber: this.shipmentAwb.trim(),
      courierName: this.shipmentCourier.trim() || null,
      trackingUrl: this.shipmentTrackingUrl.trim() || null,

      // What the courier billed for this parcel, for the books. A blank box sends nothing, which leaves
      // whatever is recorded alone - so the figure can be added on a later visit.
      freightCharge: this.shipmentFreight,

      // What is in the parcel, which is what that bill is charged to (see OrderItemMoneyWriter). Sent always, because
      // the picker is filled from the parcel: an unticked SKU is the team saying this parcel does not carry it.
      items: this.shipmentItems.map(item => ({ sku: item.sku, quantity: item.quantity }))
    };

    this.isSavingShipment = true;
    this.shipmentMessage = '';
    this.shipmentError = false;

    this.adminService.saveOrderShipment(this.order.orderId, request, userId).subscribe({
      next: (result) => {
        this.isSavingShipment = false;
        this.shipmentError = result.result !== 1;
        this.shipmentMessage = (result.messages || []).join(' ');

        if (result.result === 1) {
          // The parcel the server wrote is the one the card keeps showing - a second parcel must not leave the form
          // looking as if nothing happened, and the order is re-read so the list, the history and the per-unit
          // figures are all the ones on record.
          this.selectedShipmentId = result.shipmentId || null;
          this.bookingNewParcel = false;

          this.loadOrder(this.order!.orderId);
        }
      },
      error: () => {
        this.isSavingShipment = false;
        this.shipmentError = true;
        this.shipmentMessage = 'The parcel could not be saved. Nothing was changed - please try again.';
      }
    });
  }

  /**
   * 'Track now': asks the courier about this order's parcel right now and records what it said - which
   * is also the only thing that moves the order for a parcel (out for delivery, delivered, coming back),
   * so the screen is re-read afterwards to show the new status, history and units. A courier that cannot
   * be reached answers with its own sentence and leaves the order exactly as it was.
   */
  trackShipment(): void {
    if (!this.order || this.isTrackingShipment || !this.hasShipment) {
      return;
    }

    const userId = this.authService.getUser()?.userId ?? 0;
    if (!userId) {
      this.shipmentMessage = 'Your admin session has no user id - please sign in again.';
      this.shipmentError = true;
      return;
    }

    this.isTrackingShipment = true;
    this.shipmentMessage = '';
    this.shipmentError = false;

    this.adminService.trackOrderShipment(this.order.orderId, userId).subscribe({
      next: (result) => {
        this.isTrackingShipment = false;
        this.shipmentError = result.result !== 1;
        this.shipmentMessage = (result.messages || []).join(' ');

        if (result.result === 1) {
          this.loadOrder(this.order!.orderId);
        }
      },
      error: () => {
        this.isTrackingShipment = false;
        this.shipmentError = true;
        this.shipmentMessage = 'The courier could not be asked. The order was left as it was - please try again.';
      }
    });
  }

  /**
   * True when there is money to read on this order: the gateway took it. The server answers that per payment row
   * ('moneyTaken', see HC.Business.OrderMoney.TookMoney) rather than the app deciding it from the status, so
   * 'what counts as money taken' is one rule and not two. A pending order whose payment was never captured has
   * none, and the card says so instead of showing a column of zeroes that reads like a loss.
   */
  get hasOrderMoney(): boolean {
    return !!this.order?.payment?.moneyTaken;
  }

  /** What the gateway took for this order (0 while nothing has been paid). */
  get moneyTaken(): number {
    return this.order?.payment?.amount ?? 0;
  }

  /** The payment state in the team's own words - the same reading the Refund card spells out. */
  get paymentStatusText(): string {
    switch ((this.order?.payment?.status ?? '').toLowerCase()) {
      case 'captured':
        return 'Captured - the money is the shop\u2019s';
      case 'refundrequested':
        return 'Refund asked for - waiting for the team';
      case 'refundfailed':
        return 'Razorpay refused the refund - still owed back';
      case 'refunded':
        return 'Refunded - the money went back';
      case 'authorized':
        return 'Authorised but not captured';
      case 'failed':
        return 'The attempt failed';
      case 'created':
        return 'Started, nothing taken';
      default:
        return this.order?.payment?.status || 'not on record';
    }
  }

  /**
   * What has gone back to the customer. Only a refund that was actually SENT counts (see HC.Business.OrderMoney): a
   * refund merely asked for - the customer's cancellation, waiting for the team's approval on the Refund card - has
   * not moved any money yet, and is shown as owed there rather than as money out here.
   */
  get moneyGivenBack(): number {
    const order = this.order;
    if (!order || !order.refundedOn) {
      return 0;
    }

    return order.refundAmount ?? 0;
  }

  /**
   * What the gateway kept for taking this order's money: its charge plus the GST on it. Null while the gateway has
   * not reported them - a capture that arrived before Razorpay worked the fee out has none - because unknown is not
   * the same as nothing, and the card says 'not reported' rather than ₹0.00.
   */
  get gatewayCharges(): number | null {
    const payment = this.order?.payment;
    if (!payment || (payment.feeAmount == null && payment.taxAmount == null)) {
      return null;
    }

    return (payment.feeAmount ?? 0) + (payment.taxAmount ?? 0);
  }

  /** Where that charge came from, in the words the payment row uses (see HC.Business.OrderPaymentCharges). */
  get chargesSourceText(): string {
    switch ((this.order?.payment?.chargesSource ?? '').toLowerCase()) {
      case 'recon':
        return 'the settlement pull - what the bank was settled on';
      case 'payment':
        return 'the capture itself - an estimate the settlement pull can still correct';
      default:
        return 'nothing recorded yet';
    }
  }

  /** What the couriers billed for every parcel of this order, when the team recorded it (0 otherwise). */
  get courierCost(): number {
    return this.parcels.reduce((sum, parcel) => sum + (parcel.freightCharge ?? 0), 0);
  }

  /**
   * What each of this order's lines really carried, money-wise - the recorded split (see the API's
   * AdminOrderItemMoney). Empty for an order whose per-unit figures have not been written yet, which the card says
   * rather than showing zeros.
   */
  get lineMoney(): AdminOrderItemMoney[] {
    return this.order?.lineMoney || [];
  }

  /** How many units the per-unit table covers: the order's own units, one row per physical unit. */
  get lineMoneyUnits(): number {
    return this.lineMoney.reduce((sum, money) => sum + money.units, 0);
  }

  /** The output GST of those lines - the tax held for the government on them. */
  get lineMoneyGst(): number {
    return this.lineMoney.reduce((sum, money) => sum + money.outputGst, 0);
  }

  /**
   * The lines' parts of the gateway's charge and the GST on it. A line whose charge is not known counts as nothing
   * rather than as zero, so the total is what IS known - the table marks the rows that are short, one by one.
   */
  get lineMoneyFee(): number {
    return this.lineMoney.reduce((sum, money) => sum + (money.gatewayFee ?? 0), 0);
  }

  get lineMoneyTax(): number {
    return this.lineMoney.reduce((sum, money) => sum + (money.gatewayTax ?? 0), 0);
  }

  /** The lines' parts of the couriers' bills - what the parcels that carried them cost. */
  get lineMoneyFreight(): number {
    return this.lineMoney.reduce((sum, money) => sum + (money.freightShare ?? 0), 0);
  }

  /**
   * The margin the shop declared on this order's lines: 'Profit margin %' per unit, read as the profit's share of
   * what the customer paid (see HC.Business.OrderMoney.DeclaredProfit). It is the shop's own figure, not a purchase
   * price - nothing in the system records what the goods cost to buy.
   */
  get declaredMargin(): number {
    return this.order
      ? this.order.items.reduce((sum, item) => sum + item.unitPrice * item.profitMarginPercent / 100, 0)
      : 0;
  }

  /** What the shop declared those lines cost: what was charged for them, less the margin above. */
  get declaredCost(): number {
    return this.itemsTotal - this.declaredMargin;
  }

  /**
   * What the order left the shop once everyone else has been paid: the money taken, less what went back, less the
   * gateway's charge, less the courier's bill, less what the shop declared the goods cost.
   *
   * Null while the gateway's charge is unknown: a total with a missing line in it is not a total, and showing one
   * would be the same mistake as reading an unreported fee as a free payment.
   */
  get leftAfterTheGoods(): number | null {
    if (!this.hasOrderMoney || this.gatewayCharges == null) {
      return null;
    }

    return this.moneyTaken - this.moneyGivenBack - this.gatewayCharges - this.courierCost - this.declaredCost;
  }

  /** What the checkout charged: OrderItems holds one row per unit, each carrying its unit price. */
  get itemsTotal(): number {
    return this.order ? this.order.items.reduce((sum, item) => sum + item.unitPrice, 0) : 0;
  }

  /** The delivery/packaging/storage charges recorded on the order lines. */
  get chargesTotal(): number {
    return this.order
      ? this.order.items.reduce((sum, item) => sum + item.deliveryCharge + item.packagingCharge + item.storageCharge, 0)
      : 0;
  }

  get selectedStatusName(): string {
    if (!this.order || !this.newStatusId) {
      return '';
    }

    const status = this.order.availableStatuses.find(s => s.statusId === Number(this.newStatusId));
    return status ? status.status : '';
  }

  /**
   * True while the step chosen is Shipped - the one step that also asks about the parcel: which provider
   * it went out with, the consignment number (AWB) that provider gave, and what the courier billed for
   * it. None of it is required, because an order can be dispatched without a courier at all (handed over
   * in person, or given to a delivery service this shop is not set up with) - and then there is no parcel
   * to record, and no freight was billed.
   */
  get isShippedMove(): boolean {
    return Number(this.newStatusId) === this.shippedStatusId;
  }

  /**
   * Moves the order to the selected step. The shop team's own rules live on the server (only the
   * steps of 'availableStatuses' are allowed and a cancellation needs a reason), so the message
   * coming back is shown as it is.
   */
  updateStatus(): void {
    if (!this.order) {
      return;
    }

    if (!this.newStatusId) {
      this.actionMessage = 'Select the status to move this order to.';
      this.actionError = true;
      return;
    }

    // The consignment number and the provider together decide whether anything about the delivery is
    // written down at all: a number the team typed is a parcel whoever carries it, and a blank one is a
    // parcel against a provider that gives the parcel a reference of its own - the shop's own service,
    // which is what that provider is in the list for. An order with neither is simply dispatched, and a
    // parcel can still be recorded later from the Shipment card if one turns out to exist. The server
    // decides all of this for real (see IShipmentTrackingService.RecordsParcel), so this only saves a round
    // trip - and its own wording is shown if anything slips past it.
    const consignment = this.isShippedMove ? this.statusAwb.trim() : '';
    const recordsParcel = this.isShippedMove && (consignment.length > 0 || this.statusProviderMintsAwb);

    // What the courier billed is kept on the parcel it was billed for, so a figure with no parcel to live
    // on has nowhere to go - the server refuses it too.
    if (this.isShippedMove && !recordsParcel && this.statusFreight !== null) {
      this.actionMessage =
        'A freight charge is kept on the parcel it was billed for, so give this one a consignment number ' +
        '(AWB), or pick the provider that carries it without one - the shop\'s own service, which gives ' +
        'the parcel a reference itself. If nothing was billed for it, clear the freight charge.';
      this.actionError = true;
      return;
    }

    // What the courier billed is optional, but a negative charge is always a keystroke slip.
    if (this.isShippedMove && this.statusFreight !== null && this.statusFreight < 0) {
      this.actionMessage =
        'The freight charge cannot be negative - enter what the courier billed for this parcel, or leave it blank.';
      this.actionError = true;
      return;
    }

    const userId = this.authService.getUser()?.userId ?? 0;
    if (!userId) {
      this.actionMessage = 'Your admin session has no user id - please sign in again.';
      this.actionError = true;
      return;
    }

    this.isSavingStatus = true;
    this.actionMessage = '';
    this.actionError = false;

    this.adminService.updateOrderStatus(
      this.order.orderId,
      {
        statusId: Number(this.newStatusId),
        comments: this.statusComment,

        // Sent with a Shipped move, and only when the move records a parcel - that is what everything about
        // the parcel goes with: the provider it went out with (blank means 'the provider this shop defaults
        // to', which is what a one-provider shop sends), the courier, and what that courier billed. A blank
        // AWB against a provider that mints its own reference is sent as it is, and the server writes the
        // parcel one (see IShipmentTrackingService.RecordsParcel). A dispatch with no parcel at all records
        // nothing, so there is nothing here to send for it.
        provider: recordsParcel ? (this.statusProvider || null) : null,
        awbNumber: consignment || null,
        courierName: recordsParcel ? (this.statusCourier.trim() || null) : null,

        // What the courier billed for the parcel, for the books (never shown to the customer). A blank box
        // means 'keep whatever the parcel already holds'.
        freightCharge: recordsParcel ? this.statusFreight : null
      },
      userId
    ).subscribe({
      next: (result) => {
        this.isSavingStatus = false;
        this.actionError = result.result !== 1;
        this.actionMessage = (result.messages || []).join(' ');

        if (result.result === 1) {
          this.loadOrder(this.order!.orderId);
        }
      },
      error: () => {
        this.isSavingStatus = false;
        this.actionError = true;
        this.actionMessage = 'The order status could not be changed. Nothing was saved - please try again.';
      }
    });
  }
}
