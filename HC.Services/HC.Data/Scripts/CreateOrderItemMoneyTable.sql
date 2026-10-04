-- Create OrderItemMoney table: what each unit of an order really carried, money-wise.
--
-- The gateway's charge for taking the money, the GST on that charge, the courier's bill for the parcel and the
-- output GST inside what the customer paid are all recorded against the ORDER - there is one payment row and one
-- courier bill per order. That is enough for the shop's own books, but it cannot answer "what did this SKU make?",
-- and it cannot answer a partner whose goods only some of an order's lines are: their part of the order's money
-- has always been worked out at read time, by what their lines were worth against the whole order
-- (HC.Business.OrderMoney.Share), which is a guess about the split rather than a record of it.
--
-- This table IS that split, written down: one row per order line (OrderID + SKU), holding the line's own part of
-- the order's gateway fee, the GST on it, the couriers' bills, and the output GST the line itself carried. The
-- figures are cut by what each line was worth (OrderMoney.LineValue) and the odd paisa is given to the largest
-- remainders, so a line's parts always add back up to the order figure they came from
-- (OrderMoney.Apportion - the one rule, pinned in HC.Tests).
--
-- It is a DERIVED table: nothing is ever incremented here. One writer recomputes an order's rows from the sources
-- of truth - the payment row (OrderPayments), the parcels and their contents (OrderShipments/OrderShipmentItems)
-- and the order's own lines - every time one of them changes (see HC.Business.OrderItemMoneyWriter), so a capture,
-- a settlement recon correction, a parcel's freight and a checkout's GST all land here without any of them having
-- to know about the others. An order whose rows are missing from here is one nothing has touched since this table
-- was created; the backfill script (BackfillOrderItemMoney.sql) fills those in.
--
-- The columns are nullable where the figure may genuinely be unknown rather than nothing: a parcel with no courier
-- bill recorded yet has no freight to share (FreightShare is NULL), which must not read as "this line's freight
-- was free". OutputGst is NOT NULL with a default of 0, because the tax inside what the customer paid is worked
-- out from the order line itself and is therefore always known - a line the checkout charged no GST on really did
-- carry none.

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OrderItemMoney]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[OrderItemMoney](
        -- The order line this row is about: Orders plus the SKU its OrderItems row names, which is this row's
        -- whole key - one row per line, and a line has nothing to split into.
        [OrderID] [bigint] NOT NULL,
        [SKU] [varchar](20) NOT NULL,
        -- The output GST inside what the customer paid for this line - the tax the shop is holding for the
        -- government on it, worked out from the line's own price, discounts and rate: the line's own rate on its
        -- taxable value, taken to the rupee like the price it is part of (ProductPricing.GstAmount, which is the
        -- figure the writer and the backfill both read it with - the same arithmetic the Finance screen's OutputGst
        -- total is the sum of).
        [OutputGst] [decimal](18, 2) NOT NULL CONSTRAINT [DF_OrderItemMoney_OutputGst] DEFAULT (0),
        -- This line's part of what the gateway kept for taking the order's money (the MDR). NULL when no payment
        -- of this order has a charge recorded - unknown, and not a free payment.
        [GatewayFee] [decimal](18, 2) NULL,
        -- This line's part of the GST the gateway charged on its own fee: input credit the shop can set against
        -- what it owes.
        [GatewayTax] [decimal](18, 2) NULL,
        -- This line's part of what the couriers billed for the parcels that carried it. NULL when no parcel
        -- carrying this line has a freight charge recorded - unknown, and not free carriage.
        [FreightShare] [decimal](18, 2) NULL,
        -- Where the gateway charges above came from: 'Payment' (a capture's own report), 'Recon' (the settlement
        -- row the bank was paid on, which is the authoritative one and is never corrected back - see
        -- OrderPaymentCharges) or NULL when the order has no recorded gateway charge at all. The same
        -- vocabulary the payment row uses, so a per-line figure and the payment it came from can never
        -- describe two different charges.
        [ChargesSource] [varchar](20) NULL,
        [CreatedOn] [datetime] NOT NULL,
        [UpdatedOn] [datetime] NULL,
        CONSTRAINT [PK_OrderItemMoney] PRIMARY KEY CLUSTERED (
            [OrderID] ASC,
            [SKU] ASC
        )
    );

    -- The order is the key's own first column, so a whole order's books are read with one seek; this constraint
    -- is what makes such a row recognisably this shop's and not a stray figure.
    ALTER TABLE [dbo].[OrderItemMoney] ADD CONSTRAINT [FK_OrderItemMoney_Orders]
        FOREIGN KEY ([OrderID]) REFERENCES [dbo].[Orders] ([OrderID]);

    -- "What has this SKU made?" is read straight off the table by the Finance screen's per-SKU breakdown.
    CREATE NONCLUSTERED INDEX [IX_OrderItemMoney_SKU]
        ON [dbo].[OrderItemMoney] ([SKU] ASC);

    PRINT 'OrderItemMoney table created successfully.';
END
ELSE
BEGIN
    PRINT 'OrderItemMoney table already exists.';
END
GO
