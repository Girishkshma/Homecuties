-- ============================================================
-- Script: FixOrderHistoryIdentity.sql
-- Purpose: dbo.OrderHistory.HistoryID is BIGINT NOT NULL but was
--          created WITHOUT IDENTITY and without a default, so no row
--          could ever be inserted:
--            "Cannot insert the value NULL into column 'HistoryID'"
--          EF Core treats HistoryId as a store-generated key, so the
--          storefront could never write the "Order placed" history row.
--          The table is empty, so it is recreated with IDENTITY(1,1).
-- Tables affected:
--   - OrderHistory (recreate) + FK_OrderHistory_Orders (recreate)
-- Safe to run more than once.
-- ============================================================

SET NOCOUNT ON;

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.OrderHistory')
      AND name = 'HistoryID'
      AND is_identity = 0
)
BEGIN
    PRINT 'Recreating dbo.OrderHistory with IDENTITY on HistoryID...';

    IF OBJECT_ID('dbo.FK_OrderHistory_Orders', 'F') IS NOT NULL
        ALTER TABLE dbo.OrderHistory DROP CONSTRAINT FK_OrderHistory_Orders;

    DROP TABLE dbo.OrderHistory;

    CREATE TABLE dbo.OrderHistory
    (
        HistoryID     BIGINT        IDENTITY(1,1) NOT NULL,
        OrderID       BIGINT        NOT NULL,
        HistoryDate   DATETIME      NOT NULL,
        OrderStatusID SMALLINT      NOT NULL,
        Comments      VARCHAR(2000) NOT NULL,
        CONSTRAINT PK_OrderHistory PRIMARY KEY CLUSTERED (HistoryID)
    );

    ALTER TABLE dbo.OrderHistory WITH CHECK
        ADD CONSTRAINT FK_OrderHistory_Orders
        FOREIGN KEY (OrderID) REFERENCES dbo.Orders (OrderID);

    ALTER TABLE dbo.OrderHistory CHECK CONSTRAINT FK_OrderHistory_Orders;

    PRINT 'dbo.OrderHistory recreated.';
END
ELSE
BEGIN
    PRINT 'dbo.OrderHistory already has IDENTITY on HistoryID - nothing to do.';
END

SELECT c.name AS Column_, ty.name AS Type_, c.is_nullable, c.is_identity
FROM sys.columns c
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID('dbo.OrderHistory')
ORDER BY c.column_id;
