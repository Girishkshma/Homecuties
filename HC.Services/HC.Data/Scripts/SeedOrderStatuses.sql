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
--   3 = Shipped   -> set by the shop team once the parcel is with the courier
--   4 = Delivered -> set when the courier says the parcel arrived
--   5 = Cancelled -> the one way out while the order is still in the shop's hands
--   6 = Returned  -> a delivered order whose parcel came back: the shop team closes the return
--                    (OrderReturnStatus.Closed), the money is owed back and the units go back on the
--                    shelf. Terminal, like Cancelled.
-- ============================================================

SET NOCOUNT ON;

MERGE dbo.OrderStatuses AS target
USING (VALUES
    (CAST(1 AS smallint), 'Pending'),
    (CAST(2 AS smallint), 'Confirmed'),
    (CAST(3 AS smallint), 'Shipped'),
    (CAST(4 AS smallint), 'Delivered'),
    (CAST(5 AS smallint), 'Cancelled'),
    (CAST(6 AS smallint), 'Returned')
) AS source (OrderStatusID, Status)
ON target.OrderStatusID = source.OrderStatusID
WHEN NOT MATCHED BY TARGET THEN
    INSERT (OrderStatusID, Status) VALUES (source.OrderStatusID, source.Status);

PRINT 'OrderStatuses seeded.';

SELECT OrderStatusID, Status FROM dbo.OrderStatuses ORDER BY OrderStatusID;
