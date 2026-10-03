import { of } from 'rxjs';
import { AdminOrderDetail, AdminOrderItem, AdminOrderReturn, AdminResult } from '../models/admin.model';
import { AdminService as AdminApiService } from '../services/admin.service';
import { AuthService } from '../services/auth.service';
import { OrderDetailComponent } from './order-detail.component';

/**
 * The closing steps of a return on the admin order screen: booking the parcel back in, writing off what came back
 * broken and closing the return. The rules live on the server (see HC.Business.OrderReturnFlow) - these tests pin
 * down that the card asks for them at the right moment and sends exactly what the team ticked.
 */

/** The return of an order as the order detail sends it - only the fields a test is about are named. */
function returnState(fields: Partial<AdminOrderReturn> = {}): AdminOrderReturn {
  return {
    returnId: 9001,
    origin: 'Customer',
    originLabel: 'Requested by the customer',
    status: 'Arranged',
    reasonCode: 'NotNeeded',
    reasonLabel: 'No longer needed',
    reason: 'ordered two by mistake',
    requestedOn: new Date('2026-02-01T10:00:00Z'),
    canDecide: false,
    canMarkReceived: true,
    canInspect: false,
    canClose: false,
    isOpen: true,
    ...fields
  };
}

/** The units of the order, one row per physical unit (OrderItems carries the SKU). */
function unit(sku: string, productName = 'Linen top'): AdminOrderItem {
  return { sku, productName, productTitle: 'Sand / M' } as AdminOrderItem;
}

/** The order as the order detail endpoint sends it. */
function detail(orderReturn: AdminOrderReturn | null, items: AdminOrderItem[]): AdminOrderDetail {
  return { orderId: 7001, orderNumber: 'HC007001', items, return: orderReturn ?? undefined } as AdminOrderDetail;
}

describe('OrderDetailComponent return steps', () => {
  /**
   * The card must offer exactly the steps the server will accept: the three flags are the server's own answer
   * (OrderReturnStatus.CanMarkReceived / CanInspect / CanClose), never the page's own guess.
   */
  it('should offer the steps the return is ready for', () => {
    const component = build(returnState({ status: 'Received', canMarkReceived: false, canInspect: true, canClose: true }), []);

    expect(component.canMarkReceivedReturn).toBeFalse();
    expect(component.canInspectReturn).toBeTrue();
    expect(component.canCloseReturn).toBeTrue();
  });

  it('should offer nothing more on a return that is already closed', () => {
    const component = build(
      returnState({ status: 'Closed', canMarkReceived: false, canInspect: false, canClose: false }), []);

    expect(component.canMarkReceivedReturn).toBeFalse();
    expect(component.canInspectReturn).toBeFalse();
    expect(component.canCloseReturn).toBeFalse();
    expect(component.getReturnStatusText()).toBe('Returned and closed');
  });

  /** An order with no return at all has no card, and so no step to offer. */
  it('should offer no step on an order that never had a return', () => {
    const component = build(null, []);

    expect(component.orderReturn).toBeNull();
    expect(component.canMarkReceivedReturn).toBeFalse();
    expect(component.canInspectReturn).toBeFalse();
    expect(component.canCloseReturn).toBeFalse();
  });

  /**
   * Ticking is per unit, because one of three identical tops can come back torn while the other two are fine:
   * the same SKU ticked twice is one unit, and unticking takes it out again.
   */
  it('should tick returned units one by one', () => {
    const component = build(
      returnState({ status: 'Received', canMarkReceived: false, canInspect: true, canClose: true }),
      [unit('SKU-1'), unit('SKU-2'), unit('SKU-3')]);

    component.toggleDamagedSku('SKU-1');
    component.toggleDamagedSku('SKU-3');
    component.toggleDamagedSku('SKU-1');

    expect(component.isDamagedSku('SKU-1')).toBeFalse();
    expect(component.isDamagedSku('SKU-2')).toBeFalse();
    expect(component.isDamagedSku('SKU-3')).toBeTrue();
    expect(component.damagedSkus).toEqual(['SKU-3']);
  });

  /** What the team ticked is what the API is sent, with their note - and the ticks are cleared once saved. */
  it('should send the units that were ticked with the inspection', () => {
    const sent: { skus: string[]; comment: string }[] = [];

    const component = build(
      returnState({ status: 'Received', canMarkReceived: false, canInspect: true, canClose: true }),
      [unit('SKU-1'), unit('SKU-2')],
      {
        markOrderReturnUnitsDamaged: (_id: number, skus: string[], comment: string) => {
          sent.push({ skus, comment });
          return of({ result: 1, messages: ['1 unit taken out of the sellable stock as damaged.'] } as AdminResult);
        }
      });

    component.toggleDamagedSku('SKU-2');
    component.returnInspectionComment = 'one top is torn at the seam';
    component.inspectReturn();

    expect(sent.length).toBe(1);
    expect(sent[0].skus).toEqual(['SKU-2']);
    expect(sent[0].comment).toBe('one top is torn at the seam');
    expect(component.damagedSkus).toEqual([]);
    expect(component.returnMessage).toContain('1 unit taken out of the sellable stock');
  });

  /**
   * A session with no user id is stopped before anything is sent: the order's trail names whoever did it, so a
   * step that cannot say who did it is not worth saving.
   */
  it('should refuse to book a parcel back in without a signed-in user', () => {
    let called = false;

    const component = build(
      returnState(),
      [],
      { markOrderReturnReceived: () => { called = true; return of({ result: 1, messages: ['ok'] } as AdminResult); } },
      null);

    component.markReturnReceived();

    expect(called).toBeFalse();
    expect(component.returnError).toBeTrue();
    expect(component.returnMessage).toContain('no user id');
  });

  /** Closing the return sends the note and re-reads the order, so the card, the history and the money agree. */
  it('should close the return and re-read the order', () => {
    const sent: string[] = [];
    let reloaded = 0;

    const component = build(
      returnState({ status: 'Received', canMarkReceived: false, canInspect: true, canClose: true }),
      [unit('SKU-1')],
      {
        closeOrderReturn: (_id: number, comment: string) => {
          sent.push(comment);
          return of({
            result: 1,
            messages: ["The return is closed - 1 unit back on sale, and the order is now 'Returned'."]
          } as AdminResult);
        },
        getOrderDetail: () => {
          reloaded++;
          return of(detail(returnState({ status: 'Closed', canClose: false }), []));
        }
      });

    component.returnStepComment = 'returned in full - please refund';
    component.closeReturn();

    expect(sent).toEqual(['returned in full - please refund']);
    expect(reloaded).toBeGreaterThan(0);
    expect(component.returnMessage).toContain('The return is closed');
  });

  /**
   * The component with stub services: only the API calls a test is about are named, the provider list is always
   * empty (the cards that use it are not what is being tested here), and the signed-in user is there unless a
   * test says otherwise.
   */
  function build(
    orderReturn: AdminOrderReturn | null,
    items: AdminOrderItem[],
    api: Record<string, unknown> = {},
    user: { userId: number } | null = { userId: 7 }
  ): OrderDetailComponent {
    const service = {
      getOrderDetail: () => of(detail(orderReturn, items)),
      getShipmentProviders: () => of([]),
      ...api
    };

    const component = new OrderDetailComponent(
      service as unknown as AdminApiService,
      { getUser: () => user } as unknown as AuthService,
      {} as never,
      {} as never
    );

    // The card is drawn from the order it read, so the stub's answer is loaded the way the page loads it -
    // nothing below would be true on a component that never read one.
    component.loadOrder(7001);

    return component;
  }
});
