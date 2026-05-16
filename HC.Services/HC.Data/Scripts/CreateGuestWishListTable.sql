-- Create GuestWishList table for guest users' wishlist items
-- This table has a FK to GuestCustomers table (not Customers table)

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[GuestWishList]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[GuestWishList](
        [CustomerID] [bigint] NOT NULL,
        [ProductID] [int] NOT NULL,
        [AddedOn] [datetime] NOT NULL,
        CONSTRAINT [PK_GuestWishList] PRIMARY KEY CLUSTERED (
            [CustomerID] ASC,
            [ProductID] ASC
        )
    );

    ALTER TABLE [dbo].[GuestWishList] ADD CONSTRAINT [FK_GuestWishList_GuestCustomers]
        FOREIGN KEY ([CustomerID]) REFERENCES [dbo].[GuestCustomers] ([CustomerID]);

    ALTER TABLE [dbo].[GuestWishList] ADD CONSTRAINT [FK_GuestWishList_Products]
        FOREIGN KEY ([ProductID]) REFERENCES [dbo].[Products] ([ProductID]);

    PRINT 'GuestWishList table created successfully.';
END
ELSE
BEGIN
    PRINT 'GuestWishList table already exists.';
END
GO
