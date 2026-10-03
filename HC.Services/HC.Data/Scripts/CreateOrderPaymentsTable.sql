-- Create OrderPayments table: one row per payment attempt for an order.
-- OrderService (HC.Business) writes this table when the Razorpay order is created, when the
-- payment is reported (browser callback / webhook / status check) and when a paid order is
-- cancelled (the refund is requested, then approved by the shop team and sent via Razorpay).
-- Without it an order cannot be refunded on cancellation (payment id and amount unknown) and a
-- failed payment cannot be resumed.

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OrderPayments]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[OrderPayments](
        [PaymentID] [bigint] IDENTITY(1,1) NOT NULL,
        [OrderID] [bigint] NOT NULL,
        [Provider] [varchar](20) NOT NULL,
        [RazorpayOrderID] [varchar](50) NULL,
        [RazorpayPaymentID] [varchar](50) NULL,
        [Status] [varchar](20) NOT NULL,
        [Amount] [decimal](18, 2) NOT NULL,
        [AmountInPaise] [int] NOT NULL,
        [FailureCode] [varchar](50) NULL,
        [FailureReason] [varchar](500) NULL,
        [RefundID] [varchar](50) NULL,
        [RefundStatus] [varchar](20) NULL,
        [RefundAmount] [decimal](18, 2) NULL,
        [RefundFailureReason] [varchar](500) NULL,
        [RefundedOn] [datetime] NULL,
        -- A cancellation of a paid order asks for the refund; the shop team approves it (see
        -- AddOrderPaymentsRefundRequest.sql for environments where the table already exists).
        [RefundRequestedOn] [datetime] NULL,
        [RefundRequestedComment] [varchar](500) NULL,
        [CreatedOn] [datetime] NOT NULL,
        [UpdatedOn] [datetime] NULL,
        CONSTRAINT [PK_OrderPayments] PRIMARY KEY CLUSTERED (
            [PaymentID] ASC
        )
    );

    ALTER TABLE [dbo].[OrderPayments] ADD CONSTRAINT [FK_OrderPayments_Orders]
        FOREIGN KEY ([OrderID]) REFERENCES [dbo].[Orders] ([OrderID]);

    -- The payment of an order is always read by order, newest attempt first (see OrderService).
    CREATE NONCLUSTERED INDEX [IX_OrderPayments_OrderID]
        ON [dbo].[OrderPayments] ([OrderID] DESC);

    PRINT 'OrderPayments table created successfully.';
END
ELSE
BEGIN
    PRINT 'OrderPayments table already exists.';
END
GO
