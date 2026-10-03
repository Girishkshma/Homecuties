-- Create the two settlement tables: what the gateway settled to the shop's bank account, and what it
-- settled it on.
--
--   Settlements      one row per Razorpay settlement (setl_...): the money that reached the bank, the
--                    bank's own reference (UTR) and Razorpay's status of it.
--   SettlementItems  one row per LINE of a settlement - the payment that was settled, the refund that was
--                    taken out of it, an adjustment or a transfer. This is Razorpay's recon report for the
--                    day ('GET /v1/settlements/recon/combined'), which is the only source that says
--                    authoritatively what the gateway kept out of a payment (fee + the GST on it).
--
-- Until now the shop only knew what the CUSTOMER paid (OrderPayments). The gateway's own charge was read
-- off the capture payload where it happened to be there, but nothing in the system could say what was
-- actually settled to the bank: a settlement comes a day or two after the payment, it covers however many
-- payments were in it, a refund nets off against it and an adjustment lands on it too. The income
-- statement the Finance screen reports on needs those lines, which is why they are stored rather than
-- re-fetched every time a screen is opened.
--
-- What these tables deliberately do NOT do:
--   * they do not own the money the customer paid - that stays on OrderPayments (Amount/AmountInPaise).
--     A settlement line is what the GATEWAY did with it, so the two can be compared instead of one
--     overwriting the other (see RazorpaySettlements.AmountDisagrees, which reports a disagreement);
--   * they do not move an order or a parcel. A refund Razorpay settled is still the refund of the payment
--     row (OrderPayments.Status/RefundStatus, see RazorpayRefunds) - the settlement line is only the bank
--     side of the same money.
--
-- RazorpayEntityID + ItemType is the identity of a line: for a payment line the entity id is the payment
-- (pay_...), for a refund it is the refund (rfnd_...), and neither is ever settled twice, so the unique
-- index below is what makes the daily pull safe to re-run - and safe to re-run over days it has already
-- stored. A line is UPDATED when the gateway says something new about it (settled_at, on_hold, the
-- settlement it belongs to), never duplicated.
--
-- There is deliberately NO foreign key to OrderPayments: RazorpayOrderID is Razorpay's own id of an order
-- it took money for, and the shop only has a payment row for the ones its own checkout created (a payment
-- taken in the Razorpay dashboard, or one from years ago, has a settlement line and no row here). The pull
-- matches by RazorpayOrderID in code (see RazorpaySettlements.SyncAsync) and counts the lines it could not
-- match, so that gap is a number the shop can see rather than a foreign key error.
--
-- Run once per environment. Safe to run again: each table, constraint and index is created only when it is
-- missing, so a run that was cut short can simply be run again.

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Settlements]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Settlements](
        [SettlementID] [bigint] IDENTITY(1,1) NOT NULL,
        -- Razorpay's own id of the settlement (setl_...): the same id every line of the settlement carries
        -- in its 'settlement_id', so a pull that started from the recon report can still name the
        -- settlement the money came in.
        [RazorpaySettlementID] [varchar](50) NOT NULL,
        -- The bank's own reference of the transfer (Razorpay's 'utr'), which is what the shop's bank
        -- statement shows beside the amount - the thread a payment to the bank is followed with.
        [Utr] [varchar](50) NULL,
        -- What was settled, in rupees and in paise, with the gateway's own fee/tax totals for the
        -- settlement ('GET /v1/settlements/'). Razorpay reports those as 0 for a normal settlement (the
        -- fees were already taken per payment) and as the real figures for one that did not, so both
        -- spellings are kept rather than one being assumed.
        [AmountInPaise] [int] NULL,
        [Amount] [decimal](18,2) NULL,
        [FeesInPaise] [int] NULL,
        [Fees] [decimal](18,2) NULL,
        [TaxInPaise] [int] NULL,
        [Tax] [decimal](18,2) NULL,
        [Currency] [varchar](3) NULL,
        -- Razorpay's status of the settlement ('created' / 'processed' / 'failed').
        [Status] [varchar](20) NULL,
        -- When Razorpay created the settlement, and the day its lines were settled to the bank - the
        -- earliest 'settled_at' it reported for them, which is the day the money left. Written by the pull
        -- and only ever moved earlier, so a re-pull of the same days does not make the books jump.
        [GatewayCreatedOn] [datetime] NULL,
        [SettledOn] [datetime] NULL,
        [CreatedOn] [datetime] NOT NULL,
        [UpdatedOn] [datetime] NULL,
        CONSTRAINT [PK_Settlements] PRIMARY KEY CLUSTERED (
            [SettlementID] ASC
        )
    );

    PRINT 'Settlements table created successfully.';
END
ELSE
BEGIN
    PRINT 'Settlements table already exists.';
END
GO

-- A settlement id names one settlement, so the pull cannot store the same settlement twice - this is what
-- lets the fetched settlement and the recon report talk about one settlement instead of each adding a row.
IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'[dbo].[Settlements]')
                 AND name = N'UQ_Settlements_RazorpaySettlementID')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UQ_Settlements_RazorpaySettlementID]
        ON [dbo].[Settlements] ([RazorpaySettlementID] ASC);

    PRINT 'UQ_Settlements_RazorpaySettlementID created.';
END
ELSE
BEGIN
    PRINT 'UQ_Settlements_RazorpaySettlementID already exists.';
END
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SettlementItems]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[SettlementItems](
        [SettlementItemID] [bigint] IDENTITY(1,1) NOT NULL,
        -- The settlement this line was part of, when the pull knows it: the recon report names a settlement
        -- id on every line, but the settlement row itself is fetched separately and may not have been seen
        -- yet, which is why this is nullable.
        [SettlementID] [bigint] NULL,
        -- Razorpay's id of the thing that was settled: 'pay_...' on a payment line, 'rfnd_...' on a refund,
        -- and Razorpay's own id of an adjustment or a transfer. With ItemType this is the line.
        [RazorpayEntityID] [varchar](50) NOT NULL,
        -- Razorpay's own word for the kind of line ('payment', 'refund', 'adjustment', 'transfer'): the
        -- CHECK below holds the set and the wording lives in HC.Business.RazorpaySettlements, so a line is
        -- never stored in a spelling the code cannot read.
        [ItemType] [varchar](20) NOT NULL,
        -- The ids of the transaction behind the line. On a payment line the entity id IS the payment
        -- (Razorpay leaves 'payment_id' null there), and on a refund line the entity id is the refund while
        -- 'payment_id' is the payment it came off - which is how a refund line is traced back to the
        -- payment row whose money it took back.
        [RazorpayPaymentID] [varchar](50) NULL,
        [RazorpayOrderID] [varchar](50) NULL,
        [RazorpayRefundID] [varchar](50) NULL,
        [RazorpaySettlementID] [varchar](50) NULL,
        [SettlementUtr] [varchar](50) NULL,
        -- The money, exactly as Razorpay spoke it (paise) and in rupees beside it, the same pair every
        -- other money column in this database keeps. 'Amount' is the size of the transaction, 'Debit' what
        -- the settlement took out of the shop's account (a refund, an adjustment against it) and 'Credit'
        -- what it paid in - so a line carries one of the two and the other is 0.
        [AmountInPaise] [int] NULL,
        [Amount] [decimal](18,2) NULL,
        [DebitInPaise] [int] NULL,
        [Debit] [decimal](18,2) NULL,
        [CreditInPaise] [int] NULL,
        [Credit] [decimal](18,2) NULL,
        -- What the gateway kept for taking the money (the MDR) and the GST on it, as the settlement reports
        -- them - the authoritative figure the payment row is corrected with (ChargesSource = 'Recon', see
        -- HC.Business.OrderPaymentCharges and RazorpaySettlements.ApplyReconCharges).
        [FeeAmountInPaise] [int] NULL,
        [FeeAmount] [decimal](18,2) NULL,
        [TaxAmountInPaise] [int] NULL,
        [TaxAmount] [decimal](18,2) NULL,
        -- Amount - Fee - GST: worked out the one way this database offers (OrderPaymentCharges.Net) and
        -- stored beside Razorpay's own 'credit' on purpose. When the two disagree, the gateway's 'fee'
        -- already includes the GST on it (or the other way round) - the one thing that cannot be guessed,
        -- and what RazorpaySettlements.CreditAgreesWithFee reports.
        [NetAmount] [decimal](18,2) NULL,
        [PaymentMethod] [varchar](30) NULL,
        -- Whether the settlement of this line is on hold, and whether it has been settled at all.
        [IsOnHold] [bit] NULL,
        [IsSettled] [bit] NULL,
        -- When the transaction behind the line happened, and when it was settled to the bank.
        [GatewayCreatedOn] [datetime] NULL,
        [SettledOn] [datetime] NULL,
        -- The dispute this line belongs to, when it is one - a chargeback is taken out of a settlement.
        [DisputeID] [varchar](50) NULL,
        [Description] [varchar](200) NULL,
        -- The day the recon report this line came from was asked for: the only thing that says which pull a
        -- line was first seen in. Razorpay settles a transaction on one day, so a re-pull of that day
        -- always describes the same lines.
        [ReconDay] [date] NULL,
        [CreatedOn] [datetime] NOT NULL,
        [UpdatedOn] [datetime] NULL,
        CONSTRAINT [PK_SettlementItems] PRIMARY KEY CLUSTERED (
            [SettlementItemID] ASC
        ),
        CONSTRAINT [CK_SettlementItems_ItemType] CHECK ([ItemType] IN ('payment', 'refund', 'adjustment', 'transfer'))
    );

    PRINT 'SettlementItems table created successfully.';
END
ELSE
BEGIN
    PRINT 'SettlementItems table already exists.';
END
GO

-- The next four go in guarded batches of their own rather than inside the CREATE TABLE: a table made by an
-- earlier version of this script (or by a run that stopped half way) has no constraints and no indexes, and
-- re-running the script has to finish the job rather than print 'already exists' and walk away.

-- One line per thing settled, which is what makes the pull idempotent: a day pulled twice updates its lines
-- and adds none. ItemType is part of the key because it is Razorpay's own word for the kind of line and is
-- read back as one (see RazorpaySettlements.ItemTypes).
IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'[dbo].[SettlementItems]')
                 AND name = N'UQ_SettlementItems_Entity')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UQ_SettlementItems_Entity]
        ON [dbo].[SettlementItems] ([RazorpayEntityID] ASC, [ItemType] ASC);

    PRINT 'UQ_SettlementItems_Entity created.';
END
ELSE
BEGIN
    PRINT 'UQ_SettlementItems_Entity already exists.';
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys
               WHERE parent_object_id = OBJECT_ID(N'[dbo].[SettlementItems]')
                 AND name = N'FK_SettlementItems_Settlements')
BEGIN
    ALTER TABLE [dbo].[SettlementItems] WITH CHECK ADD CONSTRAINT [FK_SettlementItems_Settlements]
        FOREIGN KEY ([SettlementID]) REFERENCES [dbo].[Settlements] ([SettlementID]);

    PRINT 'FK_SettlementItems_Settlements added.';
END
ELSE
BEGIN
    PRINT 'FK_SettlementItems_Settlements already exists.';
END
GO

-- The pull reads the lines of an order's payment by Razorpay's own order id (see
-- RazorpaySettlements.SyncAsync, which is what corrects a payment row's charges to 'Recon'), and the
-- Finance screen reads a day's lines by the day they were settled.
IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'[dbo].[SettlementItems]')
                 AND name = N'IX_SettlementItems_RazorpayOrderID')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_SettlementItems_RazorpayOrderID]
        ON [dbo].[SettlementItems] ([RazorpayOrderID] ASC);

    PRINT 'IX_SettlementItems_RazorpayOrderID created.';
END
ELSE
BEGIN
    PRINT 'IX_SettlementItems_RazorpayOrderID already exists.';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'[dbo].[SettlementItems]')
                 AND name = N'IX_SettlementItems_SettledOn')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_SettlementItems_SettledOn]
        ON [dbo].[SettlementItems] ([SettledOn] DESC);

    PRINT 'IX_SettlementItems_SettledOn created.';
END
ELSE
BEGIN
    PRINT 'IX_SettlementItems_SettledOn already exists.';
END
GO

