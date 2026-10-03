-- Add the refund-request columns to OrderPayments: a customer cancelling a PAID order no longer
-- refunds it on the spot - the cancellation asks for the refund and the shop team approves it from
-- the admin order screen (Admin -> Orders -> the order -> Approve refund), which is when Razorpay is
-- called. These two columns are what makes an unpaid refund request visible:
--
--   RefundRequestedOn      when the customer cancelled the paid order, i.e. when the refund was asked for
--   RefundRequestedComment why it was asked for ("Order cancelled by the customer")
--
-- The payment row's Status reads 'RefundRequested' while the approval is outstanding, 'Refunded'
-- once the money has gone back (RefundID/RefundedOn) and 'RefundFailed' when Razorpay refused it
-- (RefundFailureReason, retried from the same screen or the Razorpay dashboard).
--
-- Run once per environment. Safe to run again: each column is added only when it is missing.

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OrderPayments]') AND type in (N'U'))
BEGIN
    IF COL_LENGTH('dbo.OrderPayments', 'RefundRequestedOn') IS NULL
    BEGIN
        ALTER TABLE [dbo].[OrderPayments] ADD [RefundRequestedOn] [datetime] NULL;
        PRINT 'OrderPayments.RefundRequestedOn added.';
    END
    ELSE
    BEGIN
        PRINT 'OrderPayments.RefundRequestedOn already exists.';
    END

    IF COL_LENGTH('dbo.OrderPayments', 'RefundRequestedComment') IS NULL
    BEGIN
        ALTER TABLE [dbo].[OrderPayments] ADD [RefundRequestedComment] [varchar](500) NULL;
        PRINT 'OrderPayments.RefundRequestedComment added.';
    END
    ELSE
    BEGIN
        PRINT 'OrderPayments.RefundRequestedComment already exists.';
    END
END
ELSE
BEGIN
    -- Nothing to alter: run CreateOrderPaymentsTable.sql first (it creates the table with both
    -- columns already in place).
    PRINT 'OrderPayments table does not exist - run CreateOrderPaymentsTable.sql first.';
END
GO
