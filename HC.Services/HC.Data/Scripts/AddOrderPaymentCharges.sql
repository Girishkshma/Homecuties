-- Add the money columns to OrderPayments: what the gateway charged the shop for taking a payment
-- (MDR + GST on it), what the shop actually keeps, and how a refund went back.
--
-- Until now the payment row knew only what the CUSTOMER paid. Razorpay's own charge for taking it was
-- thrown away, so the shop could not say what an order really earned (see HC.Business.RazorpaySettlements
-- for the daily pull that keeps these columns right, and the Finance screen that reports on them):
--
--   FeeAmountInPaise / FeeAmount          the gateway's charge, as Razorpay speaks it (paise) and in rupees
--   TaxAmountInPaise / TaxAmount          GST on that charge (Razorpay reports fee and tax separately)
--   NetAmount                             Amount - FeeAmount - TaxAmount - what the shop keeps
--   PaymentMethod                         card / upi / netbanking / wallet ...
--   GatewayChargedOn                      when the gateway took the money (Razorpay's created_at)
--   ChargesSource                         'Payment' (read off the capture) or 'Recon' (the daily pull,
--                                         which wins: the recon row is the authoritative figure)
--   RefundAmountInPaise                   what Razorpay really gave back (the refund entity's amount)
--   RefundArn                             the bank reference of the refund (acquirer_data.arn)
--   RefundSpeedProcessed                  normal / optimum / instant
--
-- Run once per environment. Safe to run again: each column is added only when it is missing.

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OrderPayments]') AND type in (N'U'))
BEGIN
    IF COL_LENGTH('dbo.OrderPayments', 'FeeAmountInPaise') IS NULL
    BEGIN
        ALTER TABLE [dbo].[OrderPayments] ADD [FeeAmountInPaise] [int] NULL;
        PRINT 'OrderPayments.FeeAmountInPaise added.';
    END
    ELSE
    BEGIN
        PRINT 'OrderPayments.FeeAmountInPaise already exists.';
    END

    IF COL_LENGTH('dbo.OrderPayments', 'FeeAmount') IS NULL
    BEGIN
        ALTER TABLE [dbo].[OrderPayments] ADD [FeeAmount] [decimal](18, 2) NULL;
        PRINT 'OrderPayments.FeeAmount added.';
    END
    ELSE
    BEGIN
        PRINT 'OrderPayments.FeeAmount already exists.';
    END

    IF COL_LENGTH('dbo.OrderPayments', 'TaxAmountInPaise') IS NULL
    BEGIN
        ALTER TABLE [dbo].[OrderPayments] ADD [TaxAmountInPaise] [int] NULL;
        PRINT 'OrderPayments.TaxAmountInPaise added.';
    END
    ELSE
    BEGIN
        PRINT 'OrderPayments.TaxAmountInPaise already exists.';
    END

    IF COL_LENGTH('dbo.OrderPayments', 'TaxAmount') IS NULL
    BEGIN
        ALTER TABLE [dbo].[OrderPayments] ADD [TaxAmount] [decimal](18, 2) NULL;
        PRINT 'OrderPayments.TaxAmount added.';
    END
    ELSE
    BEGIN
        PRINT 'OrderPayments.TaxAmount already exists.';
    END

    IF COL_LENGTH('dbo.OrderPayments', 'NetAmount') IS NULL
    BEGIN
        ALTER TABLE [dbo].[OrderPayments] ADD [NetAmount] [decimal](18, 2) NULL;
        PRINT 'OrderPayments.NetAmount added.';
    END
    ELSE
    BEGIN
        PRINT 'OrderPayments.NetAmount already exists.';
    END

    IF COL_LENGTH('dbo.OrderPayments', 'PaymentMethod') IS NULL
    BEGIN
        ALTER TABLE [dbo].[OrderPayments] ADD [PaymentMethod] [varchar](30) NULL;
        PRINT 'OrderPayments.PaymentMethod added.';
    END
    ELSE
    BEGIN
        PRINT 'OrderPayments.PaymentMethod already exists.';
    END

    IF COL_LENGTH('dbo.OrderPayments', 'GatewayChargedOn') IS NULL
    BEGIN
        ALTER TABLE [dbo].[OrderPayments] ADD [GatewayChargedOn] [datetime] NULL;
        PRINT 'OrderPayments.GatewayChargedOn added.';
    END
    ELSE
    BEGIN
        PRINT 'OrderPayments.GatewayChargedOn already exists.';
    END

    IF COL_LENGTH('dbo.OrderPayments', 'ChargesSource') IS NULL
    BEGIN
        ALTER TABLE [dbo].[OrderPayments] ADD [ChargesSource] [varchar](20) NULL;
        PRINT 'OrderPayments.ChargesSource added.';
    END
    ELSE
    BEGIN
        PRINT 'OrderPayments.ChargesSource already exists.';
    END

    IF COL_LENGTH('dbo.OrderPayments', 'RefundAmountInPaise') IS NULL
    BEGIN
        ALTER TABLE [dbo].[OrderPayments] ADD [RefundAmountInPaise] [int] NULL;
        PRINT 'OrderPayments.RefundAmountInPaise added.';
    END
    ELSE
    BEGIN
        PRINT 'OrderPayments.RefundAmountInPaise already exists.';
    END

    IF COL_LENGTH('dbo.OrderPayments', 'RefundArn') IS NULL
    BEGIN
        ALTER TABLE [dbo].[OrderPayments] ADD [RefundArn] [varchar](50) NULL;
        PRINT 'OrderPayments.RefundArn added.';
    END
    ELSE
    BEGIN
        PRINT 'OrderPayments.RefundArn already exists.';
    END

    IF COL_LENGTH('dbo.OrderPayments', 'RefundSpeedProcessed') IS NULL
    BEGIN
        ALTER TABLE [dbo].[OrderPayments] ADD [RefundSpeedProcessed] [varchar](20) NULL;
        PRINT 'OrderPayments.RefundSpeedProcessed added.';
    END
    ELSE
    BEGIN
        PRINT 'OrderPayments.RefundSpeedProcessed already exists.';
    END

    -- The Finance screen reads the charges of a period (see AdminDashboardService.Finance.cs), so the
    -- day the money was taken is what it filters on.
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_OrderPayments_GatewayChargedOn')
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_OrderPayments_GatewayChargedOn]
            ON [dbo].[OrderPayments] ([GatewayChargedOn] DESC);
        PRINT 'OrderPayments index IX_OrderPayments_GatewayChargedOn created.';
    END
    ELSE
    BEGIN
        PRINT 'OrderPayments index IX_OrderPayments_GatewayChargedOn already exists.';
    END
END
ELSE
BEGIN
    -- Nothing to alter: run CreateOrderPaymentsTable.sql first (it creates the table with these
    -- columns already in place).
    PRINT 'OrderPayments table does not exist - run CreateOrderPaymentsTable.sql first.';
END
GO
