-- Create OrderShipmentItems table: which units of an order one parcel carries.
--
-- A parcel used to be recorded without saying what was in it, so an order shipped in two parcels had no way
-- of saying which goods went in which - which is what the courier's bill has to be charged to. This table is
-- that answer: the shop team picks the units (by SKU) and how many of each when they record the parcel on the
-- admin order screen, and the parcel's freight charge is then split across exactly those units
-- (OrderItemMoney.FreightShare, via OrderMoney.Apportion).
--
-- One row per SKU of a parcel ('3 x HC-1042'), so a parcel carrying one unit of three different things is three
-- rows. A SKU may appear in more than one parcel of the same order - an order of four units shipped two and two
-- is one row of Quantity 2 in each parcel - which is why the unique index below is on (ShipmentID, SKU) and not
-- on (OrderID, SKU): what may not be split twice is the same SKU in the SAME parcel. That the parcel quantities
-- of an order never add up to more units than the order actually has is checked when the parcel is saved (see
-- ShipmentTrackingService.SaveAsync), because SQL Server cannot express it as a constraint here.
--
-- Direction is carried on the row (a copy of the parcel's own leg) so a reader can tell a forward parcel's
-- contents from a return's without joining to OrderShipments, which is what the per-unit money read does.
--
-- There is deliberately NO foreign key to SKUs: an order line references a SKU that sits in one inventory, and a
-- parcel is recorded against the order's own lines - tying this table to SKUs as well would refuse to record a
-- parcel for a unit whose SKU row has since been removed, which is exactly when the goods were really sent.

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OrderShipmentItems]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[OrderShipmentItems](
        [ShipmentItemID] [bigint] IDENTITY(1,1) NOT NULL,
        -- The parcel this SKU travelled in. Deleting a parcel takes its contents with it: a row without its
        -- parcel would describe goods that are nowhere.
        [ShipmentID] [bigint] NOT NULL,
        -- The order the parcel belongs to, carried on the row so the contents of every parcel of an order are
        -- read with one seek - and so a stray row can be found without the parcel.
        [OrderID] [bigint] NOT NULL,
        -- Which leg this row's parcel is ('Forward'/'Reverse' - a copy of the parcel's own Direction).
        [Direction] [varchar](10) NOT NULL CONSTRAINT [DF_OrderShipmentItems_Direction] DEFAULT ('Forward'),
        -- The order line's SKU. A SKU with no row in OrderItems for this order is refused when the parcel is
        -- saved: a parcel only ever carries what the order was for.
        [SKU] [varchar](20) NOT NULL,
        -- How many units of this SKU went in this parcel (1 or more). Smallint because an order cannot hold
        -- more than a few hundred units of one thing.
        [Quantity] [smallint] NOT NULL CONSTRAINT [DF_OrderShipmentItems_Quantity] DEFAULT (1),
        [CreatedOn] [datetime] NOT NULL,
        [UpdatedOn] [datetime] NULL,
        CONSTRAINT [PK_OrderShipmentItems] PRIMARY KEY CLUSTERED (
            [ShipmentItemID] ASC
        )
    );

    ALTER TABLE [dbo].[OrderShipmentItems] ADD CONSTRAINT [FK_OrderShipmentItems_OrderShipments]
        FOREIGN KEY ([ShipmentID]) REFERENCES [dbo].[OrderShipments] ([ShipmentID]) ON DELETE CASCADE;

    ALTER TABLE [dbo].[OrderShipmentItems] ADD CONSTRAINT [FK_OrderShipmentItems_Orders]
        FOREIGN KEY ([OrderID]) REFERENCES [dbo].[Orders] ([OrderID]);

    ALTER TABLE [dbo].[OrderShipmentItems] ADD CONSTRAINT [CK_OrderShipmentItems_Direction]
        CHECK ([Direction] IN ('Forward', 'Reverse'));

    -- A parcel carrying nothing is a parcel nobody can bill for, so the count is at least one.
    ALTER TABLE [dbo].[OrderShipmentItems] ADD CONSTRAINT [CK_OrderShipmentItems_Quantity]
        CHECK ([Quantity] > 0);

    -- One row per SKU within a parcel: the same SKU in the same parcel is one row with a count, never two rows.
    CREATE UNIQUE NONCLUSTERED INDEX [IX_OrderShipmentItems_ShipmentID_SKU]
        ON [dbo].[OrderShipmentItems] ([ShipmentID] ASC, [SKU] ASC);

    -- The contents of an order's parcels, read together when the order's money is worked out per unit.
    CREATE NONCLUSTERED INDEX [IX_OrderShipmentItems_OrderID_SKU]
        ON [dbo].[OrderShipmentItems] ([OrderID] ASC, [SKU] ASC);

    PRINT 'OrderShipmentItems table created successfully.';
END
ELSE
BEGIN
    PRINT 'OrderShipmentItems table already exists.';
END
GO
