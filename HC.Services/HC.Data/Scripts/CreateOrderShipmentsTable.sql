-- Create OrderShipments table: one row per leg of an order that has been handed to a courier.
-- ShipmentTrackingService (HC.Business) writes this table: the shop team records the AWB, the courier
-- and what that courier billed for the parcel here (the consignment itself is created by hand in the
-- provider's own panel), and the tracking pull keeps the courier's own wording, the tracking link and
-- the delivery date on it. An environment whose table already exists needs
-- AddOrderShipmentsFreightCharge.sql for the freight column and AddOrderShipmentsDirection.sql for the
-- direction column and the relaxed index.
-- Without it nothing about the parcel is remembered: 'My Orders' could not say where the parcel is,
-- and the admin order screen would have to keep the AWB in somebody's head.
--
-- One row per PARCEL of an order. An order can go out in more than one parcel - the shop team records a parcel
-- per consignment, each with its own AWB, courier and freight bill - so the index below is on
-- (OrderID, Direction) NON-unique, and what may not be recorded twice is the same AWB for the same order and leg
-- (the filtered unique index). A historical environment whose table still holds the old unique index needs
-- AllowMultipleOrderShipments.sql run as well.
--
-- What is IN a parcel is OrderShipmentItems (see CreateOrderShipmentItemsTable.sql): the units the shop team
-- picked when they recorded it, which is what the parcel's bill is charged to.
--
-- 'SET QUOTED_IDENTIFIER ON' is required here, not decoration: SQL Server refuses to create the FILTERED index
-- below when the session has it OFF, which is what sqlcmd does by default (SSMS and the app both have it on) -
-- the same rule CreateOrderReturnsTable.sql states for its own filtered index.

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OrderShipments]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[OrderShipments](
        [ShipmentID] [bigint] IDENTITY(1,1) NOT NULL,
        [OrderID] [bigint] NOT NULL,
        -- Which leg this parcel is ('Forward' = going out, 'Reverse' = coming back - see
        -- OrderShipment.DirectionForward/DirectionReverse). A returned order carries one row of each leg.
        [Direction] [varchar](10) NOT NULL CONSTRAINT [DF_OrderShipments_Direction] DEFAULT ('Forward'),
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

    ALTER TABLE [dbo].[OrderShipments] ADD CONSTRAINT [CK_OrderShipments_Direction]
        CHECK ([Direction] IN ('Forward', 'Reverse'));

    -- A parcel is always read with its order: the order screen reads every parcel of an order at once, and the
    -- forward legs are what the order's delivery is told from. NON-unique, because an order can go out in more
    -- than one parcel (see the header).
    CREATE NONCLUSTERED INDEX [IX_OrderShipments_OrderID_Direction]
        ON [dbo].[OrderShipments] ([OrderID] ASC, [Direction] ASC);

    -- The same AWB is never recorded twice for the same order and leg: recording a parcel again with a number
    -- already written down CORRECTS that parcel (its courier, its freight charge, the units in it) rather than
    -- adding a second row for the same consignment. A parcel with no AWB yet is exempt - only possible against a
    -- provider that gives no numbers of its own, and only for the moment before one is minted.
    CREATE UNIQUE NONCLUSTERED INDEX [IX_OrderShipments_OrderID_Direction_AwbNumber]
        ON [dbo].[OrderShipments] ([OrderID] ASC, [Direction] ASC, [AwbNumber] ASC)
        WHERE [AwbNumber] IS NOT NULL;

    -- Support reads the parcel the other way round, from the tracking number the customer quotes.
    CREATE NONCLUSTERED INDEX [IX_OrderShipments_AwbNumber]
        ON [dbo].[OrderShipments] ([AwbNumber] ASC);

    PRINT 'OrderShipments table created successfully.';
END
ELSE
BEGIN
    -- This script only creates the table as it stands now: a table from before the freight column needs
    -- AddOrderShipmentsFreightCharge.sql run as well, one from before a return had its own leg needs
    -- AddOrderShipmentsDirection.sql, and one from before an order could go out in more than one parcel needs
    -- AllowMultipleOrderShipments.sql.
    PRINT 'OrderShipments table already exists.';
END
GO
