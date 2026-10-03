-- Allow more than one parcel per order per leg.
--
-- OrderShipments was built as one row per order per leg: the unique index [IX_OrderShipments_OrderID_Direction]
-- said so, and it was true while a consignment was always a single parcel. An order of six things can now go out
-- in three parcels - the shop team records a parcel per consignment, each with its own AWB, courier and freight
-- bill, and each carrying the units that are really in it (OrderShipmentItems, see
-- CreateOrderShipmentItemsTable.sql). Two parcels of the same order are two real consignments, so the index that
-- forbade the second one has to go.
--
-- What this script does:
--   1. drops the unique index [IX_OrderShipments_OrderID_Direction] and recreates it NON-unique under the same
--      name, because the read it serves has not changed: "the parcels of this order, of this leg", one seek;
--   2. adds [IX_OrderShipments_OrderID_Direction_AwbNumber] UNIQUE and FILTERED on AwbNumber IS NOT NULL, which
--      is what replaces the rule the old index really stood for: the same AWB is never recorded twice for the
--      same order and leg. Recording a parcel again with an AWB already written down therefore CORRECTS that
--      parcel (a courier, a freight charge, a tracking link and the units in it can all be put right) instead of
--      adding a second row for the same consignment. A parcel with no AWB yet - only possible against a provider
--      that gives no numbers of its own, and only for the moment before one is minted - is exempt, because two
--      blank ones are not two of the same thing.
--
-- CreateOrderShipmentsTable.sql creates the table with this shape already in place, so this script is for an
-- environment whose table was created before an order could go out in more than one parcel.
--
-- Run once per environment. Safe to run again: the index is dropped only if it is still unique, each index is
-- created only when it is missing, and every step is its own batch (GO) so a name this script adds is compiled
-- against a table that already has it.
--
-- 'SET QUOTED_IDENTIFIER ON' is required here, not decoration: SQL Server refuses to create the FILTERED index
-- below when the session has it OFF, which is what sqlcmd does by default (SSMS and the app both have it on) -
-- the same rule CreateOrderReturnsTable.sql states for its own filtered index. The same setting is required of
-- every INSERT/UPDATE/DELETE against this table while that index exists; an application is unaffected
-- (Microsoft.Data.SqlClient sets it on at connect), but an ad-hoc sqlcmd statement against OrderShipments has to
-- say 'SET QUOTED_IDENTIFIER ON;' first. What the filtered index buys - one row per AWB per leg - is worth it.

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- 1. The old rule: one parcel per order per leg. Without dropping it no order could carry a second parcel.
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OrderShipments]') AND type in (N'U'))
BEGIN
    IF EXISTS (SELECT * FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'[dbo].[OrderShipments]')
                 AND name = N'IX_OrderShipments_OrderID_Direction'
                 AND is_unique = 1)
    BEGIN
        DROP INDEX [IX_OrderShipments_OrderID_Direction] ON [dbo].[OrderShipments];
        PRINT 'Unique index IX_OrderShipments_OrderID_Direction dropped (an order may now go out in more than one parcel).';
    END
    ELSE
    BEGIN
        PRINT 'Index IX_OrderShipments_OrderID_Direction is already non-unique (or absent) - nothing to drop.';
    END
END
ELSE
BEGIN
    -- Nothing to alter: run CreateOrderShipmentsTable.sql first (it creates the table with this shape already).
    PRINT 'OrderShipments table does not exist - run CreateOrderShipmentsTable.sql first.';
END
GO

-- 2. The same index, non-unique: the read it serves ("this order's parcels, this leg") is unchanged, it simply
--    now answers with a list. An order with one parcel reads exactly as it did before.
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OrderShipments]') AND type in (N'U'))
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.indexes
                   WHERE object_id = OBJECT_ID(N'[dbo].[OrderShipments]')
                     AND name = N'IX_OrderShipments_OrderID_Direction')
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_OrderShipments_OrderID_Direction]
            ON [dbo].[OrderShipments] ([OrderID] ASC, [Direction] ASC);

        PRINT 'IX_OrderShipments_OrderID_Direction created (non-unique: one or more parcels per leg).';
    END
    ELSE
    BEGIN
        PRINT 'IX_OrderShipments_OrderID_Direction already exists.';
    END
END
GO

-- 3. What the old unique index really stood for, kept: one row per AWB per order per leg. A parcel is corrected
--    by recording it again with the AWB it already has; a parcel with a NEW AWB is a new consignment and gets its
--    own row. Filtered on AwbNumber IS NOT NULL so the (briefly) AWB-less row of a provider that mints its own
--    reference is not held to a rule about a number it does not have yet.
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OrderShipments]') AND type in (N'U'))
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.indexes
                   WHERE object_id = OBJECT_ID(N'[dbo].[OrderShipments]')
                     AND name = N'IX_OrderShipments_OrderID_Direction_AwbNumber')
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX [IX_OrderShipments_OrderID_Direction_AwbNumber]
            ON [dbo].[OrderShipments] ([OrderID] ASC, [Direction] ASC, [AwbNumber] ASC)
            WHERE [AwbNumber] IS NOT NULL;

        PRINT 'IX_OrderShipments_OrderID_Direction_AwbNumber created (the same AWB is never recorded twice).';
    END
    ELSE
    BEGIN
        PRINT 'IX_OrderShipments_OrderID_Direction_AwbNumber already exists.';
    END
END
GO
