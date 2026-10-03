-- Add OrderShipments.FreightCharge: what the courier billed the shop for this parcel.
-- The shop team types it in on the admin order screen together with the AWB - when the order is
-- dispatched, or from the Shipment card afterwards - so the cost of a parcel is known here instead of
-- only in the provider's own panel, and the books can be read without it. It is the shop's own figure:
-- the customer-facing reads of a parcel never carry it (see HC.Business.Shipping.ShipmentTrackingService).
--
-- CreateOrderShipmentsTable.sql creates the table with this column already in place, so this script is
-- for an environment whose table was created before the column existed.
--
-- Run once per environment. Safe to run again: the column is added only when it is missing.

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OrderShipments]') AND type in (N'U'))
BEGIN
    IF COL_LENGTH('dbo.OrderShipments', 'FreightCharge') IS NULL
    BEGIN
        ALTER TABLE [dbo].[OrderShipments] ADD [FreightCharge] [decimal](18, 2) NULL;
        PRINT 'OrderShipments.FreightCharge added.';
    END
    ELSE
    BEGIN
        PRINT 'OrderShipments.FreightCharge already exists.';
    END
END
ELSE
BEGIN
    -- Nothing to alter: run CreateOrderShipmentsTable.sql first (it creates the table with the column
    -- already in place).
    PRINT 'OrderShipments table does not exist - run CreateOrderShipmentsTable.sql first.';
END
GO
