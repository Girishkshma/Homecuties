-- Add OrderShipments.Direction: which leg of an order a parcel row describes.
--
-- OrderShipments was built for one parcel per order (a return was booked as a new purchase, and the
-- unique index said so). Now that a return is a real thing the shop tracks, the SAME order has two
-- legs: the parcel that went out ('Forward') and the parcel coming back, either as a pickup the shop
-- books or as a courier's own return-to-origin ('Reverse'). Both are tracked by the same code
-- (HC.Business.Shipping.ShipmentTrackingService) and both are rows of this table, so the reverse leg
-- gets the same AWB, courier, tracking link, freight charge and status trail as the forward one.
--
-- What this script does:
--   1. adds [Direction] as NOT NULL with the default 'Forward', so every existing row - and every row
--      written without one - is a forward parcel, exactly as before;
--   2. replaces the unique index [IX_OrderShipments_OrderID] (one parcel per ORDER) with
--      [IX_OrderShipments_OrderID_Direction] (one parcel per LEG), which is what lets a second row
--      exist for the return.
--
-- CreateOrderShipmentsTable.sql creates the table with the column and the new index already in place,
-- so this script is for an environment whose table was created before a return had a leg.
--
-- Run once per environment. Safe to run again: the column is added only when it is missing, the
-- constraint and the indexes each follow the same rule, and the old index is dropped only if it is
-- still there.
--
-- Each step is its own batch (GO), and that is required rather than tidiness: SQL Server resolves the
-- names in a batch before any of it runs, so a CHECK or a CREATE INDEX that names [Direction] in the
-- same batch as the ALTER TABLE that adds it fails with "Invalid column name 'Direction'" - the batch
-- is compiled against the table as it was. One batch per step means the column exists by the time the
-- step that names it is compiled. ('SET QUOTED_IDENTIFIER' is not needed here - nothing in this script
-- touches OrderReturns and its filtered index - but every batch keeps to that file's rule too.)

-- 1. The column itself, with the default that makes every existing row - and every row written without
--    one - a forward parcel, exactly as before.
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OrderShipments]') AND type in (N'U'))
BEGIN
    IF COL_LENGTH('dbo.OrderShipments', 'Direction') IS NULL
    BEGIN
        -- Existing rows become 'Forward': every parcel this table held before was one going out.
        ALTER TABLE [dbo].[OrderShipments] ADD [Direction] [varchar](10) NOT NULL
            CONSTRAINT [DF_OrderShipments_Direction] DEFAULT ('Forward');

        PRINT 'OrderShipments.Direction added (existing rows are Forward).';
    END
    ELSE
    BEGIN
        PRINT 'OrderShipments.Direction already exists.';
    END
END
ELSE
BEGIN
    -- Nothing to alter: run CreateOrderShipmentsTable.sql first (it creates the table with the column
    -- and the index already in place).
    PRINT 'OrderShipments table does not exist - run CreateOrderShipmentsTable.sql first.';
END
GO

-- 2. Which leg a parcel is must be one of the two the code knows, so the database says so - in its own
--    batch, because this is the first statement here that names the column added above.
IF COL_LENGTH('dbo.OrderShipments', 'Direction') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.check_constraints
                   WHERE parent_object_id = OBJECT_ID(N'[dbo].[OrderShipments]')
                     AND name = N'CK_OrderShipments_Direction')
    BEGIN
        ALTER TABLE [dbo].[OrderShipments] ADD CONSTRAINT [CK_OrderShipments_Direction]
            CHECK ([Direction] IN ('Forward', 'Reverse'));

        PRINT 'CK_OrderShipments_Direction added.';
    END
    ELSE
    BEGIN
        PRINT 'CK_OrderShipments_Direction already exists.';
    END
END
GO

-- 3. The old index: one parcel per order. Without dropping it no order could carry a second leg.
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OrderShipments]') AND type in (N'U'))
BEGIN
    IF EXISTS (SELECT * FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'[dbo].[OrderShipments]')
                 AND name = N'IX_OrderShipments_OrderID')
    BEGIN
        DROP INDEX [IX_OrderShipments_OrderID] ON [dbo].[OrderShipments];
        PRINT 'Old unique index IX_OrderShipments_OrderID dropped.';
    END
    ELSE
    BEGIN
        PRINT 'Old unique index IX_OrderShipments_OrderID is already gone.';
    END
END
GO

-- 4. One parcel per LEG. An order is always read with both legs at once (forward for the delivery
--    story, reverse for the return), so one index serves both.
--
--    This is the index as it stood when a return got its own leg. An order can now go out in MORE than one
--    parcel, so an environment running this script must follow it with AllowMultipleOrderShipments.sql, which
--    drops the uniqueness here and puts the rule where it belongs (one row per AWB per order per leg).
IF COL_LENGTH('dbo.OrderShipments', 'Direction') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.indexes
                   WHERE object_id = OBJECT_ID(N'[dbo].[OrderShipments]')
                     AND name = N'IX_OrderShipments_OrderID_Direction')
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX [IX_OrderShipments_OrderID_Direction]
            ON [dbo].[OrderShipments] ([OrderID] ASC, [Direction] ASC);

        PRINT 'IX_OrderShipments_OrderID_Direction created (one parcel per leg).';
    END
    ELSE
    BEGIN
        PRINT 'IX_OrderShipments_OrderID_Direction already exists.';
    END
END
GO