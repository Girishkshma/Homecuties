-- Create OrderShipments table: one row per order that has been handed to a courier.
-- ShipmentTrackingService (HC.Business) writes this table: the shop team records the AWB, the courier
-- and what that courier billed for the parcel here (the consignment itself is created by hand in the
-- provider's own panel), and the tracking pull keeps the courier's own wording, the tracking link and
-- the delivery date on it. An environment whose table already exists needs
-- AddOrderShipmentsFreightCharge.sql for the freight column.
-- Without it nothing about the parcel is remembered: 'My Orders' could not say where the parcel is,
-- and the admin order screen would have to keep the AWB in somebody's head.
--
-- One row per ORDER (unique index below), because a return is booked as a new purchase and a
-- replacement is a new order - the same reasoning that gives OrderPayments one current attempt.
-- Relax that index the day the shop starts tracking a second leg (a pickup back from the customer)
-- on the same order row.

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OrderShipments]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[OrderShipments](
        [ShipmentID] [bigint] IDENTITY(1,1) NOT NULL,
        [OrderID] [bigint] NOT NULL,
        -- The courier aggregator this parcel was booked with ('Shiprocket'), i.e. the adapter that can
        -- track it (see HC.Business.Shipping.IShipmentProvider). A column rather than a constant so a
        -- second provider can be used side by side (see OrderPayments.Provider).
        [Provider] [varchar](20) NOT NULL,
        [CourierName] [varchar](100) NULL,
        -- The AWB ('tracking number') the courier gave the parcel. This is what the tracking call
        -- is made with, so it is indexed below.
        [AwbNumber] [varchar](50) NULL,
        -- The provider's own shipment id (ShiprocketShipmentID), kept so the panel's shipment can be
        -- matched to this order if support has to look it up there.
        [ShiprocketShipmentID] [bigint] NULL,
        -- The tracking page the provider reports for the AWB, shown as a link in 'My Orders'.
        [TrackingUrl] [varchar](500) NULL,
        -- What the courier billed the shop for this parcel (the provider's own freight charge). The shop
        -- team types it in with the AWB on the admin order screen and it is kept for the books, so it is
        -- the shop's own figure: the customer-facing reads of a parcel never carry it (see
        -- ShipmentTrackingService). Null while nobody has recorded it.
        [FreightCharge] [decimal](18, 2) NULL,
        -- The courier's own status wording ('Out for Delivery'), kept verbatim so the shop team reads
        -- what the courier actually said - the shop's own words are derived from it (ShipmentStatusFlow).
        [ProviderStatus] [varchar](100) NULL,
        -- The courier's status code next to the wording, kept for reference/support only (the mapping
        -- reads the wording, see ShipmentStatusFlow).
        [ProviderStatusCode] [int] NULL,
        [DeliveredOn] [datetime] NULL,
        [LastStatusText] [varchar](500) NULL,
        -- When the courier was last asked about this parcel. This is the throttle: 'My Orders' does
        -- not call the courier again for every page open (see Shipping:SyncThrottleMinutes).
        [LastCheckedOn] [datetime] NULL,
        [CreatedOn] [datetime] NOT NULL,
        [UpdatedOn] [datetime] NULL,
        CONSTRAINT [PK_OrderShipments] PRIMARY KEY CLUSTERED (
            [ShipmentID] ASC
        )
    );

    ALTER TABLE [dbo].[OrderShipments] ADD CONSTRAINT [FK_OrderShipments_Orders]
        FOREIGN KEY ([OrderID]) REFERENCES [dbo].[Orders] ([OrderID]);

    -- The shipment of an order is always read by order, and an order has exactly one parcel.
    CREATE UNIQUE NONCLUSTERED INDEX [IX_OrderShipments_OrderID]
        ON [dbo].[OrderShipments] ([OrderID] ASC);

    -- Support reads the parcel the other way round, from the tracking number the customer quotes.
    CREATE NONCLUSTERED INDEX [IX_OrderShipments_AwbNumber]
        ON [dbo].[OrderShipments] ([AwbNumber] ASC);

    PRINT 'OrderShipments table created successfully.';
END
ELSE
BEGIN
    -- This script only creates the table as it stands now: a table from before the freight column needs
    -- AddOrderShipmentsFreightCharge.sql run as well.
    PRINT 'OrderShipments table already exists.';
END
GO
