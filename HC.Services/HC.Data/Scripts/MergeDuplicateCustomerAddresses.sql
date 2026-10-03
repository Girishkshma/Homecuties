-- Collapse the duplicate rows checkout left in the address book ('My Addresses').
--
-- Checkout used to store a fresh CustomerAddresses row for every order, so a customer who typed the
-- same delivery address on two orders ended up with two identical entries under 'My Addresses' - and
-- each copy then refuses to be deleted, because the order it was written for points at it (see
-- CustomerService.DeleteAddressAsync). The code now reuses the row that is already stored
-- (OrderService.FindMatchingAddressAsync), so no new copies appear; this script clears up the copies
-- that are already in the table.
--
-- Two rows of the same customer are treated as copies when the details that decide where a parcel
-- goes and who is called about it are the same - label, both address lines, city, state, PIN, mobile
-- and recipient - compared without case or surrounding spaces, which is exactly what the code matches
-- on ('bangalore' vs 'Bangalore', 'test' vs 'TEST' are what the live copies differ by). The lowest
-- AddressID is kept; the orders that point at a copy are moved onto the kept row (Orders.
-- BillingAddressID / ShippingAddressID are the only columns that reference this table), and the
-- copies are then deleted. No other value of an order changes.
--
-- A different mobile number, a different recipient or a different label stays its own row - that is a
-- different delivery, not a copy.
--
-- Run once per environment, with the whole change in ONE transaction (it either happens or it does
-- not). Take a backup first. It is a dry run by default - it only lists what it would do - so run it
-- as it is, read the two result sets, then set @Apply = 1 and run it again to merge. Running it again
-- afterwards does nothing, because no copies are left.

SET NOCOUNT ON;

DECLARE @Apply BIT = 0;  -- 0 = show what would happen, 1 = merge the rows listed

IF OBJECT_ID('tempdb..#copies') IS NOT NULL DROP TABLE #copies;

SELECT  ca.AddressID,
        ca.CustomerID,
        MIN(ca.AddressID) OVER (
            PARTITION BY ca.CustomerID,
                         LOWER(LTRIM(RTRIM(ca.AddressTitle))),
                         LOWER(LTRIM(RTRIM(ca.AddressLine1))),
                         LOWER(LTRIM(RTRIM(ISNULL(ca.AddressLine2, '')))),
                         LOWER(LTRIM(RTRIM(ca.City))),
                         LOWER(LTRIM(RTRIM(ca.State))),
                         LOWER(LTRIM(RTRIM(ca.Zipcode))),
                         LOWER(LTRIM(RTRIM(ca.MobileNumber))),
                         LOWER(LTRIM(RTRIM(ISNULL(ca.ContactName, ''))))
        ) AS KeeperAddressID
INTO #copies
FROM dbo.CustomerAddresses AS ca;

-- The copies that fold into the kept row of the same customer.
SELECT  c.CustomerID,
        c.AddressID        AS DuplicateAddressID,
        c.KeeperAddressID  AS KeptAddressID,
        ca.AddressTitle,
        ca.AddressLine1,
        ca.City,
        ca.Zipcode,
        ca.MobileNumber,
        ca.ContactName,
        (SELECT COUNT(*) FROM dbo.Orders o
          WHERE o.BillingAddressID = c.AddressID OR o.ShippingAddressID = c.AddressID) AS OrdersMoved
FROM #copies AS c
JOIN dbo.CustomerAddresses AS ca ON ca.AddressID = c.AddressID
WHERE c.AddressID <> c.KeeperAddressID
ORDER BY c.CustomerID, c.AddressID;

-- The orders whose address moves onto the kept row.
SELECT  o.OrderID,
        o.OrderStatusID,
        o.BillingAddressID,
        o.ShippingAddressID
FROM dbo.Orders AS o
WHERE EXISTS (SELECT 1 FROM #copies c WHERE c.AddressID <> c.KeeperAddressID
                AND (c.AddressID = o.BillingAddressID OR c.AddressID = o.ShippingAddressID))
ORDER BY o.OrderID;

IF @Apply = 0
BEGIN
    PRINT 'Dry run: nothing was changed. Set @Apply = 1 to merge the rows listed above.';
    RETURN;
END

PRINT 'Merging the duplicate addresses...';

BEGIN TRANSACTION;

UPDATE o
SET    o.BillingAddressID = c.KeeperAddressID
FROM   dbo.Orders AS o
JOIN   #copies AS c ON c.AddressID = o.BillingAddressID
WHERE  c.AddressID <> c.KeeperAddressID;

UPDATE o
SET    o.ShippingAddressID = c.KeeperAddressID
FROM   dbo.Orders AS o
JOIN   #copies AS c ON c.AddressID = o.ShippingAddressID
WHERE  c.AddressID <> c.KeeperAddressID;

DELETE ca
FROM   dbo.CustomerAddresses AS ca
JOIN   #copies AS c ON c.AddressID = ca.AddressID
WHERE  c.AddressID <> c.KeeperAddressID;

COMMIT TRANSACTION;

PRINT 'Done. The address book now holds one row per distinct delivery address.';
