-- ============================================================
-- Script: SeedOrderStatuses.sql
-- Purpose: Orders.OrderStatusID has a foreign key to OrderStatuses
--          (FK_Orders_OrderStatuses), but the OrderStatuses table is
--          empty - so placing an order always failed with an FK
--          violation. Seed the lifecycle used by the storefront.
-- Tables affected:
--   - OrderStatuses (insert)
-- Notes:
--   1 = Pending   -> set by OrderService.CreateOrderAsync when the order is placed
--   2 = Confirmed -> set by OrderService.VerifyPaymentAsync after the signature check
-- ============================================================

SET NOCOUNT ON;

MERGE dbo.OrderStatuses AS target
USING (VALUES
    (CAST(1 AS smallint), 'Pending'),
    (CAST(2 AS smallint), 'Confirmed'),
    (CAST(3 AS smallint), 'Shipped'),
    (CAST(4 AS smallint), 'Delivered'),
    (CAST(5 AS smallint), 'Cancelled')
) AS source (OrderStatusID, Status)
ON target.OrderStatusID = source.OrderStatusID
WHEN NOT MATCHED BY TARGET THEN
    INSERT (OrderStatusID, Status) VALUES (source.OrderStatusID, source.Status);

PRINT 'OrderStatuses seeded.';

SELECT OrderStatusID, Status FROM dbo.OrderStatuses ORDER BY OrderStatusID;
