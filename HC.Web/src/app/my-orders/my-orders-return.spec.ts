import { of } from 'rxjs';
import { MyOrder, OrderShipment, PaymentService } from '../services/payment.service';
import { MyOrdersComponent } from './my-orders.component';

/**
 * The return side of 'My Orders': what the page says about a return - whether the customer asked for it or the
 * courier reported it - and where what they sent back has got to. The rules themselves live on the server; these
 * tests pin down that the page tells the two apart and draws the parcel coming back as its own line, rather than
 * inventing a second story about either.
 */

/** A parcel in the shape the API sends it (see OrderShipmentDto) - only the fields a test is about are named. */
function parcel(fields: Partial<OrderShipment> = {}): OrderShipment {
  return {
    result: 1,
    messages: [],
    orderId: 7001,
    direction: 'Forward',
    isReverse: false,
    hasShipment: true,
    provider: 'Shiprocket',
    reportsTracking: true,
    courierName: 'Delhivery',
    awbNumber: 'AWB-1',
    trackingUrl: '',
    providerStatus: '',
    providerStatusCode: null,
    status: '',
    stage: 'Booked',
    delivered: false,
    closed: false,
    deliveredOn: null,
    lastStatusText: '',
    lastCheckedOn: null,
    orderStatusId: null,
    orderStatus: null,
    ...fields
  };
}

/** An order as the page holds one after 'normalizeMyOrder'. */
function order(fields: Partial<MyOrder> = {}): MyOrder {
  const address = {
    AddressId: 1,
    ContactName: '',
    AddressLine1: '',
    AddressLine2: '',
    City: '',
    State: '',
    Zipcode: '',
    MobileNumber: '',
    EmailId: null
  };

  return {
    OrderId: 7001,
    OrderNumber: 'HC007001',
    OrderDate: '2026-01-15T10:00:00Z',
    TotalAmount: 1499,
    StatusId: 4,
    Status: 'Delivered',
    PaymentStatus: 'Paid',
    IsPaid: true,
    RefundPending: false,
    CanCancel: false,
    CanReturn: false,
    CanWithdrawReturn: false,
    ReturnStatus: null,
    ReturnReason: null,
    ReturnRequestedOn: null,
    ReturnOrigin: null,
    ReturnWindowEndsOn: null,
    CanPay: false,
    ItemCount: 1,
    ShippingAddress: address,
    BillingAddress: address,
    Items: [],
    History: [],
    Shipment: null,
    ReturnShipment: null,
    ...fields
  };
}

describe('MyOrdersComponent returns', () => {
  let component: MyOrdersComponent;
  let refreshAnswer: { result: number; messages: string[]; shipments: OrderShipment[] };

  beforeEach(() => {
    refreshAnswer = { result: 1, messages: [], shipments: [] };

    component = new MyOrdersComponent(
      { refreshShipments: () => of(refreshAnswer) } as unknown as PaymentService,
      {} as never,
      {} as never,
      {} as never
    );
  });

  /** An order the customer asked to send back, and the same state raised by the courier's own report. */
  it('should word a courier-raised return apart from the customers own ask', () => {
    const asked = component.returnNote(order({
      ReturnStatus: 'Requested',
      ReturnOrigin: 'Customer',
      ReturnReason: 'No longer needed'
    }));

    const reported = component.returnNote(order({
      ReturnStatus: 'Requested',
      ReturnOrigin: 'Courier',
      ReturnReason: 'Returned to sender (could not be delivered)'
    }));

    expect(asked).toContain('You asked to send HC007001 back');
    expect(asked).toContain('No longer needed');

    expect(reported).toContain('The courier has told us');
    expect(reported).toContain('Returned to sender (could not be delivered)');
    expect(reported).not.toContain('You asked to send');
  });

  /**
   * The words for each later step: an approved return says the parcel is on its way back, one that is back says
   * the refund goes out as soon as the return is closed, and a closed one says the money has been placed with the
   * team - because closing a return asks for the refund rather than sending it.
   */
  it('should say what happens next at every step of a return', () => {
    expect(component.returnNote(order({ ReturnStatus: 'Arranged' }))).toContain('on its way back to us');
    expect(component.returnNote(order({ ReturnStatus: 'Received' }))).toContain('back with us');
    expect(component.returnNote(order({ ReturnStatus: 'Closed' }))).toContain('placed with our team');
    expect(component.returnNote(order({ ReturnStatus: 'Rejected' }))).toContain('could not accept the return');
    expect(component.returnNote(order({ ReturnStatus: 'Withdrawn' }))).toContain('took your return request back');
    expect(component.returnNote(order())).toBe('');
  });

  /** The parcel coming back is drawn only once there is one, exactly like the parcel that went out. */
  it('should draw the return parcel only when one has been recorded', () => {
    expect(component.hasReturnParcel(order())).toBeFalse();
    expect(component.hasReturnParcel(order({ ReturnShipment: parcel({ isReverse: true, hasShipment: false }) })))
      .toBeFalse();
    expect(component.hasReturnParcel(order({ ReturnShipment: parcel({ isReverse: true }) }))).toBeTrue();
  });

  /**
   * The status line falls back to the courier's own words (the shop has none for every wording a courier uses),
   * and the badge is coloured by the stage - a parcel coming back to us is not the green of a delivery.
   */
  it('should read the return parcel the same way as the delivery', () => {
    const onItsWayBack = order({
      ReturnShipment: parcel({
        isReverse: true,
        providerStatus: 'RTO In Transit',
        status: 'Returning to the shop',
        stage: 'Rto',
        trackingUrl: 'https://shiprocket.co/tracking/AWB-1'
      })
    });

    expect(component.returnParcelStatusText(onItsWayBack)).toBe('Returning to the shop');
    expect(component.returnParcelBadgeClass(onItsWayBack)).toBe('bg-dark');
    expect(component.returnParcelTrackingUrl(onItsWayBack)).toContain('shiprocket');

    const unknownWording = order({
      ReturnShipment: parcel({ isReverse: true, providerStatus: 'Some new RTO wording', status: '', stage: 'Unknown' })
    });

    expect(component.returnParcelStatusText(unknownWording)).toBe('Some new RTO wording');

    // A refusal is the customer's own 'no' at the door: the same amber as a failed attempt, not green.
    const refused = order({ ReturnShipment: parcel({ isReverse: true, stage: 'Refused' }) });

    expect(component.returnParcelBadgeClass(refused)).toBe('bg-warning text-dark');
  });

  /**
   * A parcel the shop collects itself has no courier to ask: the page offers no 'Refresh return' for it, and it
   * never claims a courier has nothing more to report.
   */
  it('should keep quiet about a courier on a parcel the shop collects itself', () => {
    const collected = order({
      ReturnShipment: parcel({ isReverse: true, reportsTracking: false, closed: true, stage: 'Delivered' })
    });

    expect(component.returnParcelHasCourier(collected)).toBeFalse();
    expect(component.returnParcelNumberLabel(collected)).toBe('Reference');
    expect(component.isReturnParcelClosed(collected)).toBeFalse();
  });

  /**
   * The courier pull answers with both legs of every order it looked at. They are folded onto the order by leg,
   * so a return's pickup can never overwrite the delivery - and an order the answer says nothing about is left
   * exactly as it was.
   */
  it('should fold both legs of the pull onto the order they belong to', () => {
    component.orders = [
      order({ OrderId: 7001, Shipment: parcel({ awbNumber: 'AWB-DELIVERY' }) }),
      order({ OrderId: 7002, Shipment: parcel({ orderId: 7002, awbNumber: 'AWB-OTHER' }) })
    ];

    refreshAnswer = {
      result: 1,
      messages: [],
      shipments: [
        parcel({ isReverse: true, direction: 'Reverse', awbNumber: 'AWB-RETURN', status: 'Returning to the shop' })
      ]
    };

    component.trackParcels();

    const first = component.orders[0];
    const second = component.orders[1];

    // The return parcel lands on its own order, and the delivery of that order is untouched.
    expect(first.ReturnShipment?.awbNumber).toBe('AWB-RETURN');
    expect(first.Shipment?.awbNumber).toBe('AWB-DELIVERY');

    // An order the answer says nothing about keeps exactly what it had.
    expect(second.Shipment?.awbNumber).toBe('AWB-OTHER');
    expect(second.ReturnShipment).toBeNull();
  });
});
