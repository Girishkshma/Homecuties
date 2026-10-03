-- Create OrderReturns table: the return of a delivered order (or of a parcel the customer refused at
-- the doorstep) - the ask itself, who decided it, when the parcel came back and what the inspection
-- found on it.
--
-- An order used to end at Delivered: a customer wanting to send something back had to e-mail the shop,
-- so the return lived in somebody's inbox and nothing about it was on the order - the admin screen
-- could not say whether a return was pending, whether the parcel was on its way back, or whether the
-- money had been given back.
--
-- This table is the ORDER-level record of that return. What it deliberately does NOT hold:
--   * the money  - the refund is the payment's business (OrderPayments.Status/RefundStatus/RefundedOn,
--                  see RazorpayRefunds and AddOrderPaymentsRefundRequest.sql), so a refund can never be
--                  described in two places and OrderReturns carries no amount of its own;
--   * the parcel - the reverse leg is a second OrderShipments row (Direction = 'Reverse', see
--                  AddOrderShipmentsDirection.sql), tracked by the same code as the forward one;
--   * the story  - every step is written to OrderHistory as it happens, which 'My Orders' and the admin
--                  order screen already both show, so there is no second trail to keep in step.
--
-- At most ONE OPEN return per order (the filtered unique index below), so a second return request for
-- the same order is refused by the database and not only by the service: 'a delivered order is returned
-- once, refunded once' rests on it.
--
-- Origin/ReasonCode/Status wording lives in HC.Business (OrderReturnOrigin, OrderReturnReason,
-- OrderReturnStatus) and is exactly what the screen offers and the server accepts - the same split as
-- OrderPayments.Status (OrderPaymentStatus). Origin is checked below because 'which way the ask came in'
-- is structural and will never grow; the reason and status vocabularies are business wording and are
-- guarded in code, like every other status in this database.
--
-- Run once per environment. Safe to run again: the table, its constraints and its two indexes are each
-- created only when they are missing, so a run that was cut short - or one against a table an earlier
-- version of this script created - can simply be run again.
--
-- 'SET QUOTED_IDENTIFIER ON' is required here, not decoration: SQL Server refuses to create the FILTERED
-- index below when the session has it OFF, which is what sqlcmd does by default (SSMS and the app both have
-- it on). The same setting is required of every INSERT/UPDATE/DELETE against this table while that index
-- exists - an application is unaffected (Microsoft.Data.SqlClient sets it on at connect), but an ad-hoc
-- sqlcmd statement against OrderReturns has to say 'SET QUOTED_IDENTIFIER ON;' first. A filtered index is
-- what makes 'one open return per order' a fact of the database rather than a hope, so the setting stays and
-- the file is split into batches around the indexes.

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OrderReturns]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[OrderReturns](
        [ReturnID] [bigint] IDENTITY(1,1) NOT NULL,
        [OrderID] [bigint] NOT NULL,
        -- Where the ask came from: 'Customer' (asked from 'My Orders' on a delivered order) or
        -- 'Courier' (a refusal or a return-to-origin the tracking pull reported on a shipped order -
        -- the courier raises the ask, it never decides it).
        [Origin] [varchar](10) NOT NULL,
        -- The reason in the customer's own words when they gave one ('wrong size'), or the courier's
        -- wording when the parcel came back on its own. Always filled: a return is never recorded
        -- without a reason.
        [Reason] [varchar](500) NOT NULL,
        -- Which reason that is (OrderReturnReason: NotNeeded/WrongItem/Damaged/NotAsDescribed/Other for
        -- the customer, RefusedAtDoor/ReturnedToOrigin for the courier), so returns can be counted by
        -- reason without reading the free text.
        [ReasonCode] [varchar](30) NOT NULL,
        -- Requested -> Arranged -> Received -> Closed, or Rejected/Withdrawn on the way out
        -- (OrderReturnStatus). 'Arranged' means the pickup is booked, 'Received' that the parcel is
        -- physically back with the shop, 'Closed' that the sale has been reversed - the order is
        -- 'Returned', the money is owed back and the units went back on the shelf.
        [Status] [varchar](20) NOT NULL,
        [RequestedOn] [datetime] NOT NULL,
        -- Who asked: the customer's login id, or null when the courier raised it (Origin = 'Courier').
        [RequestedBy] [varchar](100) NULL,
        -- The shop team's answer (Status becomes Approved -> Arranged, or Rejected), when and by whom,
        -- with the note they had to give. Null while the ask is still waiting for one; 'what was
        -- decided' is derived from Status, so it is not stored twice.
        [DecisionOn] [datetime] NULL,
        [DecisionBy] [varchar](100) NULL,
        [DecisionComment] [varchar](500) NULL,
        -- When the parcel was physically back with the shop (the reverse leg's own DeliveredOn is when
        -- the courier says it handed it over; this is the shop confirming it).
        [ReceivedOn] [datetime] NULL,
        -- When the return was closed: its units were put back on the shelf (or written off) and the
        -- order became 'Returned'.
        [ClosedOn] [datetime] NULL,
        -- What the inspection found. Which units came back broken is in the stock trail (SKUHistory
        -- rows moving those SKUs to the 'Damage' status, see SkuAvailability.MarkUnitsDamagedAsync) and
        -- why is in this note. Null while nobody has inspected the parcel - returned units go back on
        -- sale without waiting for it.
        [InspectionOn] [datetime] NULL,
        [InspectionBy] [varchar](100) NULL,
        [InspectionComment] [varchar](500) NULL,
        [CreatedOn] [datetime] NOT NULL,
        [UpdatedOn] [datetime] NULL,
        CONSTRAINT [PK_OrderReturns] PRIMARY KEY CLUSTERED (
            [ReturnID] ASC
        ),
        CONSTRAINT [CK_OrderReturns_Origin] CHECK ([Origin] IN ('Customer', 'Courier'))
    );

    PRINT 'OrderReturns table created successfully.';
END
ELSE
BEGIN
    PRINT 'OrderReturns table already exists.';
END
GO

-- The next three go in guarded batches of their own rather than inside the CREATE TABLE: a table made by an
-- earlier version of this script (or by a run that stopped half way) has no constraints and no index, and
-- re-running the script has to finish the job rather than print 'already exists' and walk away.

-- Where an ask came from is a closed set - 'Customer' or 'Courier' - so the database says so.
IF NOT EXISTS (SELECT * FROM sys.check_constraints
               WHERE parent_object_id = OBJECT_ID(N'[dbo].[OrderReturns]')
                 AND name = N'CK_OrderReturns_Origin')
BEGIN
    ALTER TABLE [dbo].[OrderReturns] ADD CONSTRAINT [CK_OrderReturns_Origin]
        CHECK ([Origin] IN ('Customer', 'Courier'));

    PRINT 'CK_OrderReturns_Origin added.';
END

IF NOT EXISTS (SELECT * FROM sys.foreign_keys
               WHERE parent_object_id = OBJECT_ID(N'[dbo].[OrderReturns]')
                 AND name = N'FK_OrderReturns_Orders')
BEGIN
    ALTER TABLE [dbo].[OrderReturns] ADD CONSTRAINT [FK_OrderReturns_Orders]
        FOREIGN KEY ([OrderID]) REFERENCES [dbo].[Orders] ([OrderID]);

    PRINT 'FK_OrderReturns_Orders added.';
END
GO

-- One open return per order (SQL Server leaves 'Closed'/'Rejected'/'Withdrawn' rows out of the filter, so
-- the history of past returns is kept while a second live one is impossible). This is the index that makes
-- 'a delivered order is returned once, refunded once' true, and what OrderReturnStatus.Open must match.
IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'[dbo].[OrderReturns]')
                 AND name = N'IX_OrderReturns_OrderID_Open')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_OrderReturns_OrderID_Open]
        ON [dbo].[OrderReturns] ([OrderID] ASC)
        WHERE [Status] IN ('Requested', 'Arranged', 'Received');

    PRINT 'IX_OrderReturns_OrderID_Open created (one open return per order).';
END
ELSE
BEGIN
    PRINT 'IX_OrderReturns_OrderID_Open already exists.';
END
GO

-- The shop team's list is read by status ('what is waiting for an answer', 'what is on its way back'),
-- newest ask first.
IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'[dbo].[OrderReturns]')
                 AND name = N'IX_OrderReturns_Status')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OrderReturns_Status]
        ON [dbo].[OrderReturns] ([Status] ASC, [RequestedOn] ASC);

    PRINT 'IX_OrderReturns_Status created.';
END
ELSE
BEGIN
    PRINT 'IX_OrderReturns_Status already exists.';
END
GO
