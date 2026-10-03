-- Backfill OrderItemMoney: open the per-line books of the orders that were sold before the table existed.
--
-- OrderItemMoney is derived - one writer recomputes an order's rows from the payment rows, the parcels and their
-- contents and the order's own lines (see HC.Business.OrderItemMoneyWriter) - but only for the things that HAPPEN
-- to an order. Orders nothing has touched since the table was created have no rows at all, and the Finance
-- screen's per-SKU breakdown cannot speak for them (it says so, and names this script).
--
-- This script does that one pass, in SQL, for every such order: it writes the same figures the writer would, from
-- the same rules -
--
--   * what each unit was worth to the customer (its taxable value at the rate the checkout charged - the CGST rate
--     alone, see OrderMoney.ChargedGstRate) is the weight every order-level figure is split by;
--   * the output GST of a line is the tax inside what the customer paid for its units - order-line arithmetic, and
--     the figure the Finance screen's OutputGst total is the sum of;
--   * the gateway's charge (the fee the payment row holds, and the GST on it) is spread over ALL the order's units
--     and the courier's bill for a parcel over the units THAT parcel carried (falling back to the whole order when
--     nobody said what was in it), with the odd paisa given to the largest remainders so the parts add back up to
--     the amount exactly (OrderMoney.Apportion);
--   * 'not known' is NULL, never 0: an order no gateway has charged has no fee to split, and a parcel nobody has
--     billed has no freight to share.
--
-- IT IS A BACKFILL, NOT THE RULE. The writer is the rule, and it rewrites these very rows the next time an order is
-- touched (a capture, a settlement pull, a parcel's freight charge). The SQL below is a faithful copy of it, and
-- where the two could disagree - the last paisa of a split whose remainders tie to within the tenth decimal place a
-- decimal(38,10) quotient can carry - the app's own figures win, because they are the ones that will be written.
-- The check at the end of this script prints any order whose per-line gateway fee does not add back up to the
-- charge on its payment rows, so a disagreement is something you READ here rather than discover in the books.
--
-- Run it once after CreateOrderItemMoneyTable.sql. Safe to run again: by default it only writes the orders that
-- have NO per-unit rows yet, so a second run changes nothing. Set @RebuildEveryOrder to 1 to delete and rewrite the
-- rows of every order instead (useful if a copy of the table is suspected of being wrong - the app will not mind:
-- it recomputes rather than increments).
--
-- Run in ONE batch (this file has no GO at all): the whole thing is one transaction, and a backfill that stopped
-- halfway would leave orders half-written.

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

-- 0 or 1. 0 - fill in the orders that have no per-unit rows yet, which is what a first run wants. 1 - rebuild every
-- order's rows, deleting what is there for them first.
DECLARE @RebuildEveryOrder bit = 0;

IF OBJECT_ID(N'[dbo].[OrderItemMoney]') IS NULL
BEGIN
    PRINT 'OrderItemMoney does not exist - run CreateOrderItemMoneyTable.sql first, then this script.';

    RETURN;
END

IF OBJECT_ID(N'[dbo].[OrderShipmentItems]') IS NULL
BEGIN
    PRINT 'OrderShipmentItems does not exist - run CreateOrderShipmentItemsTable.sql first. Without it no parcel can ' +
          'say what it carries, and every bill would be spread over the whole order.';

    RETURN;
END

BEGIN TRY
    BEGIN TRANSACTION;

    -- The orders this run writes.
    IF OBJECT_ID('tempdb..#Orders') IS NOT NULL DROP TABLE #Orders;

    SELECT DISTINCT items.OrderID
    INTO #Orders
    FROM dbo.OrderItems items
    WHERE @RebuildEveryOrder = 1
       OR NOT EXISTS (SELECT 1 FROM dbo.OrderItemMoney money WHERE money.OrderID = items.OrderID);

    CREATE UNIQUE CLUSTERED INDEX IX_Orders ON #Orders (OrderID);

    -- The order's own lines, one row per physical unit (OrderItems carries one row per unit - there is no quantity
    -- column), with the two figures the split needs from each: what the unit was worth to the customer, and the
    -- output GST inside that.
    --
    -- The arithmetic is the C#'s, written out: the taxable value is the price less both discounts, the weight is
    -- that at the rate the checkout charged (the CGST rate alone - see OrderMoney.ChargedGstRate), and the tax is
    -- the same value at the same rate. Everything is carried at decimal(38,10) so a long division lands where the
    -- app's does, and Ordinal is each unit's position among the order's SKUs - the tie-break the odd paise are given
    -- by, matching the order the writer puts its units in.
    IF OBJECT_ID('tempdb..#Units') IS NOT NULL DROP TABLE #Units;

    SELECT
        items.OrderID,
        items.SKU,
        CONVERT(bigint, ROW_NUMBER() OVER (PARTITION BY items.OrderID ORDER BY items.SKU)) AS Ordinal,
        CONVERT(decimal(38, 10), (items.UnitPrice
            - (items.UnitPrice * items.DiscountPercent / 100)
            - (items.UnitPrice * items.AdditionalDiscountPercent / 100))
            * (1 + items.CGSTPercent / 100)) AS UnitValue,
        CONVERT(decimal(38, 10), (items.UnitPrice
            - (items.UnitPrice * items.DiscountPercent / 100)
            - (items.UnitPrice * items.AdditionalDiscountPercent / 100))
            * items.CGSTPercent / 100) AS UnitGst
    INTO #Units
    FROM dbo.OrderItems items
    INNER JOIN #Orders orders ON orders.OrderID = items.OrderID;

    -- What the whole order is worth, and how many units it holds.
    IF OBJECT_ID('tempdb..#OrderWeights') IS NOT NULL DROP TABLE #OrderWeights;

    SELECT OrderID, SUM(UnitValue) AS TotalWeight, COUNT_BIG(*) AS Units
    INTO #OrderWeights
    FROM #Units
    GROUP BY OrderID;

    -- The weights actually used. An order whose lines were all worth nothing (given away, say) still has a fee to
    -- carry, so its units share it equally rather than the figure being dropped - the rule OrderMoney.Apportion
    -- states, expressed here as a weight of 1 each. OrderWeight is therefore never 0, which is what keeps every
    -- division below a real one.
    IF OBJECT_ID('tempdb..#Weights') IS NOT NULL DROP TABLE #Weights;

    SELECT
        units.OrderID,
        units.SKU,
        units.Ordinal,
        CASE WHEN weights.TotalWeight = 0 THEN CONVERT(decimal(38, 10), 1) ELSE units.UnitValue END AS Weight,
        CASE WHEN weights.TotalWeight = 0
             THEN CONVERT(decimal(38, 10), weights.Units)
             ELSE weights.TotalWeight END AS OrderWeight
    INTO #Weights
    FROM #Units units
    INNER JOIN #OrderWeights weights ON weights.OrderID = units.OrderID;

    -- What the gateway kept for taking each order's money, from the payment rows that hold it - the same rows and
    -- the same statuses the Finance screen reads (OrderMoney.TakenStatuses). An order with no such row, or whose
    -- rows carry no charge yet, is deliberately absent: its per-line fee is NULL ('not known'), not 0.
    IF OBJECT_ID('tempdb..#Charges') IS NOT NULL DROP TABLE #Charges;

    SELECT
        payments.OrderID,
        SUM(ISNULL(payments.FeeAmount, 0)) AS Fee,
        SUM(ISNULL(payments.TaxAmount, 0)) AS Tax,
        MAX(CASE WHEN payments.ChargesSource = 'Recon' THEN 1 ELSE 0 END) AS HasRecon
    INTO #Charges
    FROM dbo.OrderPayments payments
    INNER JOIN #Orders orders ON orders.OrderID = payments.OrderID
    WHERE payments.Status IN ('Captured', 'RefundRequested', 'RefundFailed', 'Refunded')
      AND (payments.FeeAmount IS NOT NULL OR payments.TaxAmount IS NOT NULL)
    GROUP BY payments.OrderID;

    -- Everything that has to be split, and over what: one row per (amount, unit that carries a part of it). Three
    -- kinds share the shape - the gateway's fee, the GST on it, and a parcel's courier bill - because the rule is
    -- one rule whatever the amount is (OrderMoney.Apportion).
    IF OBJECT_ID('tempdb..#Carry') IS NOT NULL DROP TABLE #Carry;

    CREATE TABLE #Carry (
        Kind varchar(20) NOT NULL,
        KeyId bigint NOT NULL,
        OrderID bigint NOT NULL,
        SKU varchar(20) NOT NULL,
        Ordinal bigint NOT NULL,
        Weight decimal(38, 10) NOT NULL,
        CarriedWeight decimal(38, 10) NOT NULL,
        AmountPaise int NOT NULL
    );

    -- The gateway's charge is the ORDER's, so every unit of the order carries a part of it. The amounts are read in
    -- paise because a paisa is the coin the odd part of a split is given in; the columns hold two decimals, so the
    -- multiplication is exact.
    INSERT INTO #Carry (Kind, KeyId, OrderID, SKU, Ordinal, Weight, CarriedWeight, AmountPaise)
    SELECT 'Fee', charges.OrderID, weights.OrderID, weights.SKU, weights.Ordinal,
           weights.Weight, weights.OrderWeight, CONVERT(int, ROUND(charges.Fee * 100, 0))
    FROM #Charges charges
    INNER JOIN #Weights weights ON weights.OrderID = charges.OrderID;

    INSERT INTO #Carry (Kind, KeyId, OrderID, SKU, Ordinal, Weight, CarriedWeight, AmountPaise)
    SELECT 'Tax', charges.OrderID, weights.OrderID, weights.SKU, weights.Ordinal,
           weights.Weight, weights.OrderWeight, CONVERT(int, ROUND(charges.Tax * 100, 0))
    FROM #Charges charges
    INNER JOIN #Weights weights ON weights.OrderID = charges.OrderID;

    -- A quantity of three is three carried units, so the units a parcel holds are expanded one at a time - which is
    -- what makes the split land on the units that were really in it rather than on the SKUs as lumps. The tally is
    -- as long as the longest quantity recorded, so no unit is ever left out of its parcel's bill.
    IF OBJECT_ID('tempdb..#Tally') IS NOT NULL DROP TABLE #Tally;

    DECLARE @MaxQuantity int = (SELECT ISNULL(MAX(Quantity), 1) FROM dbo.OrderShipmentItems);

    IF @MaxQuantity < 1 SET @MaxQuantity = 1;

    ;WITH numbers AS (
        SELECT TOP (@MaxQuantity) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS N
        FROM sys.all_objects
    )
    SELECT N INTO #Tally FROM numbers;

    -- The courier's bill, for the parcels that went out and have one: spread over the units the parcel says it
    -- carries (OrderShipmentItems), and - when it says nothing, or what it says names goods this order no longer has
    -- - over the whole order instead, because losing the bill would shrink the shop's own costs. A reverse leg is
    -- deliberately left out: its bill is the RETURN's cost, and the Finance screen has never counted it against the
    -- sale (see AdminDashboardService.Finance).
    INSERT INTO #Carry (Kind, KeyId, OrderID, SKU, Ordinal, Weight, CarriedWeight, AmountPaise)
    SELECT 'Freight', shipments.ShipmentID, shipments.OrderID, weights.SKU, weights.Ordinal,
           CASE WHEN SUM(weights.Weight) OVER (PARTITION BY shipments.ShipmentID) = 0
                THEN CONVERT(decimal(38, 10), 1) ELSE weights.Weight END,
           CASE WHEN SUM(weights.Weight) OVER (PARTITION BY shipments.ShipmentID) = 0
                THEN CONVERT(decimal(38, 10), COUNT_BIG(*) OVER (PARTITION BY shipments.ShipmentID))
                ELSE SUM(weights.Weight) OVER (PARTITION BY shipments.ShipmentID) END,
           CONVERT(int, ROUND(shipments.FreightCharge * 100, 0))
    FROM dbo.OrderShipments shipments
    INNER JOIN #Orders orders ON orders.OrderID = shipments.OrderID
    INNER JOIN dbo.OrderShipmentItems carried ON carried.ShipmentID = shipments.ShipmentID
    INNER JOIN #Weights weights ON weights.OrderID = shipments.OrderID AND weights.SKU = carried.SKU
    INNER JOIN #Tally tally ON tally.N <= carried.Quantity
    WHERE shipments.Direction = 'Forward' AND shipments.FreightCharge IS NOT NULL;

    INSERT INTO #Carry (Kind, KeyId, OrderID, SKU, Ordinal, Weight, CarriedWeight, AmountPaise)
    SELECT 'Freight', shipments.ShipmentID, shipments.OrderID, weights.SKU, weights.Ordinal,
           weights.Weight, weights.OrderWeight, CONVERT(int, ROUND(shipments.FreightCharge * 100, 0))
    FROM dbo.OrderShipments shipments
    INNER JOIN #Orders orders ON orders.OrderID = shipments.OrderID
    INNER JOIN #Weights weights ON weights.OrderID = shipments.OrderID
    WHERE shipments.Direction = 'Forward' AND shipments.FreightCharge IS NOT NULL
      AND NOT EXISTS (
            SELECT 1
            FROM dbo.OrderShipmentItems carried
            INNER JOIN #Weights own ON own.OrderID = shipments.OrderID AND own.SKU = carried.SKU
            WHERE carried.ShipmentID = shipments.ShipmentID);

    -- The split, written out the way OrderMoney.Apportion does it. Each part is its exact share of the amount as a
    -- fraction of the carried weight, TRUNCATED TOWARDS ZERO so no part can be over its share; the amounts left over
    -- - at most one paisa per unit - are then given one at a time to the units that lost the most to that truncation
    -- (the largest remainders), with the unit's own position breaking a tie. The parts therefore add back up to the
    -- amount exactly, which is what makes a per-line report tie to the order it was cut from.
    IF OBJECT_ID('tempdb..#Split') IS NOT NULL DROP TABLE #Split;

    ;WITH shares AS (
        SELECT
            carry.Kind, carry.KeyId, carry.OrderID, carry.SKU, carry.Ordinal, carry.AmountPaise,
            CONVERT(decimal(38, 10), CONVERT(decimal(38, 10), carry.AmountPaise)
                * carry.Weight / carry.CarriedWeight) AS RawPaise
        FROM #Carry carry
    ),
    parts AS (
        SELECT
            Kind, KeyId, OrderID, SKU, Ordinal, AmountPaise, RawPaise,
            CASE WHEN RawPaise >= 0 THEN FLOOR(RawPaise) ELSE CEILING(RawPaise) END AS FloorPaise
        FROM shares
    ),
    leftovers AS (
        SELECT Kind, KeyId, OrderID,
               MAX(AmountPaise) - SUM(FloorPaise) AS ChangePaise
        FROM parts
        GROUP BY Kind, KeyId, OrderID
    ),
    ranked AS (
        SELECT
            parts.Kind, parts.OrderID, parts.SKU, parts.FloorPaise, parts.RawPaise,
            leftovers.ChangePaise,
            ROW_NUMBER() OVER (PARTITION BY parts.Kind, parts.KeyId, parts.OrderID
                               ORDER BY ABS(parts.RawPaise - parts.FloorPaise) DESC, parts.Ordinal) AS RankNo
        FROM parts
        INNER JOIN leftovers
            ON leftovers.Kind = parts.Kind
           AND leftovers.KeyId = parts.KeyId
           AND leftovers.OrderID = parts.OrderID
    )
    SELECT
        ranked.Kind,
        ranked.OrderID,
        ranked.SKU,
        CONVERT(int, ranked.FloorPaise)
            + CASE WHEN ranked.RankNo <= ABS(ranked.ChangePaise) THEN SIGN(ranked.ChangePaise) ELSE 0 END AS PartPaise
    INTO #Split
    FROM ranked;

    -- One row per order line, folded back up from the units: what its units were charged in output GST, and the
    -- parts of the three order-level figures that landed on them.
    IF OBJECT_ID('tempdb..#Money') IS NOT NULL DROP TABLE #Money;

    SELECT
        units.OrderID,
        units.SKU,
        CONVERT(decimal(18, 2), SUM(units.UnitGst)) AS OutputGst
    INTO #Money
    FROM #Units units
    GROUP BY units.OrderID, units.SKU;

    IF OBJECT_ID('tempdb..#Parts') IS NOT NULL DROP TABLE #Parts;

    SELECT
        split.OrderID,
        split.SKU,
        FeePaise = SUM(CASE WHEN split.Kind = 'Fee' THEN split.PartPaise ELSE 0 END),
        TaxPaise = SUM(CASE WHEN split.Kind = 'Tax' THEN split.PartPaise ELSE 0 END),
        FreightPaise = SUM(CASE WHEN split.Kind = 'Freight' THEN split.PartPaise ELSE 0 END)
    INTO #Parts
    FROM #Split split
    GROUP BY split.OrderID, split.SKU;

    -- The orders that actually had a parcel bill to split - so that a line with no freight part is written as NULL
    -- ('not known') rather than as 0.00 ('carried for free'), which is the distinction the whole table keeps.
    IF OBJECT_ID('tempdb..#Billed') IS NOT NULL DROP TABLE #Billed;

    SELECT DISTINCT shipments.OrderID
    INTO #Billed
    FROM dbo.OrderShipments shipments
    INNER JOIN #Orders orders ON orders.OrderID = shipments.OrderID
    WHERE shipments.Direction = 'Forward' AND shipments.FreightCharge IS NOT NULL;

    -- Rebuilding means the rows this script would write are removed first, so what is left is exactly what it is
    -- about to write. A normal run writes only the orders that have no rows at all (the WHERE below).
    IF @RebuildEveryOrder = 1
    BEGIN
        DELETE money
        FROM dbo.OrderItemMoney money
        INNER JOIN #Orders orders ON orders.OrderID = money.OrderID;
    END

    DECLARE @Written int = 0;

    INSERT INTO dbo.OrderItemMoney (OrderID, SKU, OutputGst, GatewayFee, GatewayTax, FreightShare, ChargesSource, CreatedOn)
    SELECT
        money.OrderID,
        money.SKU,
        money.OutputGst,
        -- A charge the gateway has not reported is 'not known', and stays NULL: a line's part of the fee is nothing
        -- to write down until something says what the gateway kept.
        CASE WHEN charges.OrderID IS NULL
             THEN NULL ELSE CONVERT(decimal(18, 2), ISNULL(parts.FeePaise, 0)) / 100 END,
        CASE WHEN charges.OrderID IS NULL
             THEN NULL ELSE CONVERT(decimal(18, 2), ISNULL(parts.TaxPaise, 0)) / 100 END,
        CASE WHEN billed.OrderID IS NULL
             THEN NULL ELSE CONVERT(decimal(18, 2), ISNULL(parts.FreightPaise, 0)) / 100 END,
        -- Where those charges came from, in the payment row's own vocabulary: the settlement's word is the
        -- authoritative one and is never corrected back (see OrderPaymentCharges), so one recon row among an order's
        -- payments makes the whole figure the bank's.
        CASE WHEN charges.OrderID IS NULL THEN NULL
             WHEN charges.HasRecon = 1 THEN 'Recon'
             ELSE 'Payment' END,
        GETUTCDATE()
    FROM #Money money
    LEFT JOIN #Charges charges ON charges.OrderID = money.OrderID
    LEFT JOIN #Billed billed ON billed.OrderID = money.OrderID
    LEFT JOIN #Parts parts ON parts.OrderID = money.OrderID AND parts.SKU = money.SKU
    WHERE NOT EXISTS (
            SELECT 1
            FROM dbo.OrderItemMoney existing
            WHERE existing.OrderID = money.OrderID AND existing.SKU = money.SKU);

    SET @Written = @@ROWCOUNT;

    COMMIT TRANSACTION;

    -- The count is taken into a variable first: CONCAT does not accept a subquery in its arguments
    -- ('Subqueries are not allowed in this context').
    DECLARE @Orders int = (SELECT COUNT(*) FROM #Orders);

    PRINT CONCAT('Backfilled ', @Written, ' per-unit row(s) for ', @Orders, ' order(s).');

    -- The check, in plain terms: any order whose per-line gateway fee does not add back up to the charge on its
    -- payment rows is a disagreement between this script and the app, and is printed here rather than left to be
    -- found in the books. The app rewrites these rows the next time each order is touched, so a difference is
    -- temporary - but it is worth knowing about. At most ten are named; the count says how many there really are.
    DECLARE @Mismatched int = 0;

    SELECT @Mismatched = COUNT(*)
    FROM (
        SELECT money.OrderID
        FROM dbo.OrderItemMoney money
        INNER JOIN #Charges charges ON charges.OrderID = money.OrderID
        GROUP BY money.OrderID, charges.Fee, charges.Tax
        HAVING ABS(CONVERT(decimal(18, 2), SUM(ISNULL(money.GatewayFee, 0))) - charges.Fee) > 0.00
            OR ABS(CONVERT(decimal(18, 2), SUM(ISNULL(money.GatewayTax, 0))) - charges.Tax) > 0.00
    ) mismatched;

    IF @Mismatched > 0
    BEGIN
        PRINT CONCAT(@Mismatched, ' order(s) do not add back up to the charge on their payment row - the app rewrites ',
            'these the next time each order is touched, and the ones to look at are:');

        SELECT TOP (10)
            money.OrderID,
            charges.Fee,
            charges.Tax,
            PerLineFee = CONVERT(decimal(18, 2), SUM(ISNULL(money.GatewayFee, 0))),
            PerLineTax = CONVERT(decimal(18, 2), SUM(ISNULL(money.GatewayTax, 0)))
        FROM dbo.OrderItemMoney money
        INNER JOIN #Charges charges ON charges.OrderID = money.OrderID
        GROUP BY money.OrderID, charges.Fee, charges.Tax
        HAVING ABS(CONVERT(decimal(18, 2), SUM(ISNULL(money.GatewayFee, 0))) - charges.Fee) > 0.00
            OR ABS(CONVERT(decimal(18, 2), SUM(ISNULL(money.GatewayTax, 0))) - charges.Tax) > 0.00;
    END
    ELSE
    BEGIN
        PRINT 'Every line written adds back up to the charge on its order''s payment row.';
    END
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT CONCAT('Backfill failed and was rolled back: ', ERROR_MESSAGE());

    THROW;
END CATCH
