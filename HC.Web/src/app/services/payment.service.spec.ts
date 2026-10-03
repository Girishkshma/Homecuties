import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { ApiConfigService } from './api.service';
import { normalizeMyOrder, OrderShipment, PaymentService, RawMyOrder, RefreshOrderShipments } from './payment.service';

/**
 * An order as 'Order/GetOrders' sends it, with the fields this file cares about filled in. Everything
 * else is left out on purpose: the extra fields are what 'normalizeMyOrder' has to work out by itself.
 */
function serverOrder(fields: Partial<RawMyOrder>): RawMyOrder {
  return {
    OrderId: 5001,
    OrderNumber: 'HC-5001',
    OrderDate: '2026-01-15T10:00:00Z',
    TotalAmount: 1499,
    Items: [],

    // Whatever the test is about: the status, the payment state and - on purpose - the newer fields a
    // server build may leave out.
    ...fields
  };
}

/**
 * A parcel as 'Order/GetOrders' sends it: the camelCase shape of the API's OrderShipmentDto (the one
 * shape both this page and the admin order screen read). Only the fields a test is about are named.
 */
function serverShipment(fields: Partial<OrderShipment> = {}): OrderShipment {
  return {
    result: 1,
    messages: [],
    orderId: 5001,
    // Every parcel this page reads is the one that went out unless the answer says otherwise: a return's
    // pickup is the same shape with 'isReverse' true.
    direction: 'Forward',
    isReverse: false,
    hasShipment: true,
    provider: 'Shiprocket',
    // A parcel with a courier behind it is the usual case; the shop's own delivery (no courier to ask) is
    // named by the tests that are about it.
    reportsTracking: true,
    courierName: 'Delhivery',
    awbNumber: 'AWB123456',
    trackingUrl: 'https://shiprocket.co/tracking/AWB123456',
    providerStatus: 'Out for Delivery',
    providerStatusCode: 17,
    status: 'Out for delivery',
    stage: 'OutForDelivery',
    delivered: false,
    closed: false,
    deliveredOn: null,
    lastStatusText: 'Out for Delivery',
    lastCheckedOn: '2026-02-01T09:15:00Z',
    orderStatusId: null,
    orderStatus: null,
    ...fields
  };
}

/**
 * Cancelling a PAID order only asks for the refund - the shop team approves it from the admin order
 * screen - so 'My Orders' has to keep showing that money is owed back ('Refund pending') until it has
 * really been given back. These tests pin down what the page is told, including orders answered by a
 * server build that does not send 'RefundPending' yet.
 */
describe('normalizeMyOrder refunds', () => {
  it('should mark a cancelled paid order as refund pending', () => {
    const order = normalizeMyOrder(serverOrder({ Status: 'Cancelled', StatusId: 5, IsPaid: true, RefundPending: true }));

    expect(order.RefundPending).toBeTrue();
  });

  it('should fall back to the pending payment state a server without the field sends', () => {
    // What the API answered before 'RefundPending' existed: the payment state alone says the refund is
    // outstanding, and the customer must still be told about it.
    const order = normalizeMyOrder(serverOrder({ Status: 'Cancelled', StatusId: 5, PaymentStatus: 'Refund pending' }));

    expect(order.RefundPending).toBeTrue();
  });

  it('should not report a refund pending once the money has gone back', () => {
    const order = normalizeMyOrder(serverOrder({ Status: 'Cancelled', StatusId: 5, IsPaid: true, RefundPending: false, PaymentStatus: 'Refunded' }));

    expect(order.RefundPending).toBeFalse();
    expect(order.PaymentStatus).toBe('Refunded');
  });

  it('should not report a refund pending on a paid order that is still on its way', () => {
    const order = normalizeMyOrder(serverOrder({ Status: 'Confirmed', StatusId: 2, IsPaid: true, PaymentStatus: 'Paid' }));

    expect(order.RefundPending).toBeFalse();
  });

  it('should not report a refund pending on a cancelled order that was never charged', () => {
    // A cancelled Pending order: nothing was ever taken, so there is nothing to give back.
    const order = normalizeMyOrder(serverOrder({ Status: 'Cancelled', StatusId: 5, IsPaid: false }));

    expect(order.RefundPending).toBeFalse();
    expect(order.PaymentStatus).toBe('Not charged');
  });

  it('should treat a cancelled order as not paid, the way the API does once money is owed back', () => {
    // A cancelled order is never 'paid' on either side: the money is being returned, not held. The
    // fallback has to agree with the API so the page does not claim a cancelled order is still paid.
    const order = normalizeMyOrder(serverOrder({ Status: 'Cancelled', StatusId: 5, PaymentStatus: 'Refund pending' }));

    expect(order.IsPaid).toBeFalse();
    expect(order.RefundPending).toBeTrue();
  });
});

/**
 * The parcel travels with the order ('Shipment' on each row), so 'My Orders' can show where it is before
 * any courier has been asked - and an order with no parcel has to say nothing about shipping rather than
 * draw an empty card.
 */
describe('normalizeMyOrder parcels', () => {
  it('should keep the parcel the API sent with the order', () => {
    const order = normalizeMyOrder(serverOrder({ Status: 'Shipped', StatusId: 3, Shipment: serverShipment() }));

    expect(order.Shipment?.awbNumber).toBe('AWB123456');
    expect(order.Shipment?.courierName).toBe('Delhivery');
    expect(order.Shipment?.stage).toBe('OutForDelivery');
    expect(order.Shipment?.trackingUrl).toBe('https://shiprocket.co/tracking/AWB123456');
  });

  it('should answer no parcel when the order was sent without one', () => {
    // A confirmed order that has not been handed to a courier yet: the page asks 'hasParcel' and gets
    // undefined, so nothing about shipping is drawn at all.
    const order = normalizeMyOrder(serverOrder({ Status: 'Confirmed', StatusId: 2 }));

    expect(order.Shipment).toBeNull();
  });

  it('should answer no parcel for a row that has no AWB on it yet', () => {
    // A shipment may exist before the shop team has typed the AWB in. Until then there is no parcel to
    // track, and offering a 'Track parcel' button for it would only fail.
    const order = normalizeMyOrder(serverOrder({ Status: 'Confirmed', StatusId: 2, Shipment: serverShipment({ hasShipment: false, awbNumber: '' }) }));

    expect(order.Shipment).toBeNull();
  });

  it('should fill in what a parcel recorded a moment ago does not say yet', () => {
    // Just recorded, so the courier has not been asked: the strip must still have usable values instead
    // of blanks, and the badge falls back to the 'Unknown' stage.
    const order = normalizeMyOrder(serverOrder({ Status: 'Confirmed', StatusId: 2, Shipment: serverShipment({ providerStatus: '', status: '', stage: undefined, trackingUrl: undefined }) }));

    expect(order.Shipment?.stage).toBe('Unknown');
    expect(order.Shipment?.trackingUrl).toBe('');
    expect(order.Shipment?.delivered).toBeFalse();
    expect(order.Shipment?.closed).toBeFalse();
  });

  /**
   * An order carries two parcels once a return has been approved: the one that went out and the one coming
   * back. They are answered in the same shape, told apart by 'isReverse', and 'My Orders' draws them in two
   * strips - so one leg can never be mistaken for the other.
   */
  it('should keep the parcel coming back apart from the parcel that went out', () => {
    const order = normalizeMyOrder(serverOrder({
      Status: 'Delivered',
      StatusId: 4,
      Shipment: serverShipment({ providerStatus: 'Delivered', status: 'Delivered', stage: 'Delivered' }),
      ReturnShipment: serverShipment({
        direction: 'Reverse',
        isReverse: true,
        awbNumber: 'AWB-RETURN-1',
        providerStatus: 'RTO In Transit',
        status: 'Returning to the shop',
        stage: 'Rto'
      })
    }));

    expect(order.Shipment?.isReverse).toBeFalse();
    expect(order.Shipment?.awbNumber).toBe('AWB123456');
    expect(order.ReturnShipment?.isReverse).toBeTrue();
    expect(order.ReturnShipment?.direction).toBe('Reverse');
    expect(order.ReturnShipment?.stage).toBe('Rto');
  });

  it('should answer no return parcel on an order that never had one', () => {
    const order = normalizeMyOrder(serverOrder({ Status: 'Delivered', StatusId: 4, Shipment: serverShipment() }));

    expect(order.ReturnShipment).toBeNull();
  });

  it('should read a parcel a server build sent before returns had legs as the one that went out', () => {
    // The older answer has no 'isReverse' at all: it can only ever be the delivery, because a return's own
    // parcel did not exist when that build was written.
    const order = normalizeMyOrder(serverOrder({
      Status: 'Confirmed',
      StatusId: 2,
      Shipment: serverShipment({ direction: undefined, isReverse: undefined })
    }));

    expect(order.Shipment?.isReverse).toBeFalse();
    expect(order.Shipment?.direction).toBe('Forward');
  });

  it('should remember who asked for the return, so the page can word it right', () => {
    const courier = normalizeMyOrder(serverOrder({
      Status: 'Delivered',
      StatusId: 4,
      ReturnStatus: 'Requested',
      ReturnOrigin: 'Courier',
      ReturnReason: 'Returned to sender (could not be delivered)'
    }));

    const customer = normalizeMyOrder(serverOrder({
      Status: 'Delivered',
      StatusId: 4,
      ReturnStatus: 'Requested',
      ReturnOrigin: 'Customer'
    }));

    expect(courier.ReturnOrigin).toBe('Courier');
    expect(customer.ReturnOrigin).toBe('Customer');
  });
});

/**
 * 'Order/RefreshShipments' is the courier pull 'My Orders' makes when it opens and when 'Refresh parcel'
 * is tapped. These tests pin down the address and the body (the customer comes from the token, so there
 * is nothing to send), and that the answer is handed back untouched - the page shows the parcels and the
 * note straight from it, and a courier that could not be reached must not look like a broken page.
 */
describe('PaymentService.refreshShipments', () => {
  let service: PaymentService;
  let http: HttpTestingController;
  let baseServUrl: string;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule]
    });

    service = TestBed.inject(PaymentService);
    http = TestBed.inject(HttpTestingController);
    baseServUrl = TestBed.inject(ApiConfigService).getBaseServUrl();
  });

  afterEach(() => http.verify());

  it('should ask the API for the customer parcels with nothing but an empty body', () => {
    const answered: RefreshOrderShipments = {
      result: 1,
      messages: [],
      shipments: [serverShipment({ orderStatusId: 3, orderStatus: 'Shipped' })]
    };

    const results: RefreshOrderShipments[] = [];
    service.refreshShipments().subscribe((fresh) => results.push(fresh));

    const request = http.expectOne(baseServUrl + 'Order/RefreshShipments');
    expect(request.request.method).toBe('POST');
    // Nothing identifying is sent: the customer is taken from the token, and an empty body is the signal
    // to look at every live parcel of that customer.
    expect(request.request.body).toEqual({});
    request.flush(answered);

    expect(results.length).toBe(1);
    expect(results[0]).toEqual(answered);
  });

  it('should hand back a courier that could not be reached with its own note', () => {
    // The API answers result 1 with the reason: the parcel keeps the status it already had, so the page
    // has nothing to draw and only the sentence to show - this must not arrive as an error.
    const results: RefreshOrderShipments[] = [];
    service.refreshShipments().subscribe((fresh) => results.push(fresh));

    http.expectOne(baseServUrl + 'Order/RefreshShipments').flush({
      result: 1,
      messages: ['Shiprocket could not be reached for AWB123456 - the last known status is shown.'],
      shipments: []
    });

    expect(results.length).toBe(1);
    expect(results[0].result).toBe(1);
    expect(results[0].shipments.length).toBe(0);
    expect(results[0].messages[0]).toContain('could not be reached');
  });

  it('should report a failed call so the page can keep the orders it already has', () => {
    let failed = false;
    service.refreshShipments().subscribe({ error: () => (failed = true) });

    http.expectOne(baseServUrl + 'Order/RefreshShipments').flush(
      { Result: 0, Messages: ['Server error'] },
      { status: 500, statusText: 'Server Error' }
    );

    expect(failed).toBeTrue();
  });
});
