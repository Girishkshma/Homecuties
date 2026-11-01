-- ============================================================
-- Script: AddPortalAccessTracking.sql
-- Purpose: Make 'AccessTracking' carry a portal access the way the admin screen reads it - from which
--          location, and how often - on a database that already has the table, and create the table on
--          one that has never had it.
-- Tables affected:
--   - AccessTracking   (widen the address and place columns, add 'Portal' and 'CountryCode', index the
--                       columns the report reads, seed the portal of the rows already there)
--   - V_ACCESSTRACKING (created only when a database has no view at all - the report does not read it)
--
-- Why the columns grow:
--   IP   15 -> 45    an IPv6 address is up to 45 characters. Cut to 15, every IPv6 visitor shares one
--                    address, so a hundred of them read as one visitor from one place.
--   URL  150 -> 400  a page address carries its query string ('/search?q=lamp', '/product/10004?ref=...'),
--                    which is what makes one page distinct from another, and 150 characters was reached
--                    long before a real address ended.
--   City/Region/Country 20 -> 100  place names are longer than 20 characters ('Andaman and Nicobar
--                    Islands', 'Tamil Nadu', 'United Kingdom'), and a name that does not fit fails the
--                    whole insert - the hit would be lost rather than stored unplaced.
--
-- Why 'Portal' and 'CountryCode':
--   Portal       which shop front the hit came from ('Web' for the storefront, 'Admin' for the admin
--                area), so one table counts both without a second schema. The rows written before this
--                script ran all came from the storefront, so they are seeded as 'Web'.
--   CountryCode  the ISO 3166-1 alpha-2 code the location lookup names beside the country ('IN', 'US'),
--                which is what keeps two spellings of one country from reading as two.
--
-- Why Latitude/Longitude/AccuracyMeters are in the CREATE TABLE below:
--   The API writes the point a visitor agreed to share on every hit it records, so a table made without
--   those columns would refuse the insert rather than store the hit unplaced (see
--   AddAccessTrackingSharedLocation.sql, which adds them to a table that already exists). They are
--   nullable and are left empty on a table that is created here: a point is only ever written when a
--   visitor shares one, and none of the rows this script has ever written came with one.
--
-- Safe to run on any environment and more than once: every step asks whether it has already been done
-- and does nothing when it has. Nothing here deletes a row.
-- ============================================================

SET NOCOUNT ON;

-- ============================================================
-- STEP 1: the table itself, for a database that has never had one
-- ============================================================
IF OBJECT_ID(N'dbo.AccessTracking', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AccessTracking](
        [ID] [bigint] IDENTITY(1,1) NOT NULL,
        [IP] [varchar](45) NOT NULL,
        [URL] [varchar](400) NOT NULL,
        [AccessOn] [datetime] NOT NULL,
        [City] [varchar](100) NULL,
        [Region] [varchar](100) NULL,
        [Country] [varchar](100) NULL,
        [Organization] [varchar](200) NULL,
        [Portal] [varchar](20) NULL,
        [CountryCode] [varchar](2) NULL,
        [Latitude] [float] NULL,
        [Longitude] [float] NULL,
        [AccuracyMeters] [float] NULL,
        CONSTRAINT [PK_AccessTracking] PRIMARY KEY CLUSTERED ([ID] ASC)
    );

    PRINT 'AccessTracking table created.';
END
ELSE
BEGIN
    PRINT 'AccessTracking table already exists - columns are checked one by one below.';
END
GO

-- ============================================================
-- STEP 2: the address and the page address, wide enough to hold what is really written
-- ============================================================
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.AccessTracking') AND name = 'IP' AND max_length < 45
)
BEGIN
    ALTER TABLE [dbo].[AccessTracking] ALTER COLUMN [IP] [varchar](45) NOT NULL;
    PRINT 'AccessTracking.IP widened to varchar(45) - IPv6 addresses are kept whole.';
END

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.AccessTracking') AND name = 'URL' AND max_length < 400
)
BEGIN
    ALTER TABLE [dbo].[AccessTracking] ALTER COLUMN [URL] [varchar](400) NOT NULL;
    PRINT 'AccessTracking.URL widened to varchar(400) - the query string is kept.';
END
GO

-- ============================================================
-- STEP 3: the place columns, wide enough for a real place name
-- ============================================================
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.AccessTracking') AND name = 'City' AND max_length < 100
)
BEGIN
    ALTER TABLE [dbo].[AccessTracking] ALTER COLUMN [City] [varchar](100) NULL;
    PRINT 'AccessTracking.City widened to varchar(100).';
END

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.AccessTracking') AND name = 'Region' AND max_length < 100
)
BEGIN
    ALTER TABLE [dbo].[AccessTracking] ALTER COLUMN [Region] [varchar](100) NULL;
    PRINT 'AccessTracking.Region widened to varchar(100).';
END

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.AccessTracking') AND name = 'Country' AND max_length < 100
)
BEGIN
    ALTER TABLE [dbo].[AccessTracking] ALTER COLUMN [Country] [varchar](100) NULL;
    PRINT 'AccessTracking.Country widened to varchar(100).';
END
GO

-- ============================================================
-- STEP 4: which shop front the hit came from, and the country's own code
-- ============================================================
IF COL_LENGTH('dbo.AccessTracking', 'Portal') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccessTracking] ADD [Portal] [varchar](20) NULL;
    PRINT 'AccessTracking.Portal added.';
END
ELSE
BEGIN
    PRINT 'AccessTracking.Portal already exists.';
END

IF COL_LENGTH('dbo.AccessTracking', 'CountryCode') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccessTracking] ADD [CountryCode] [varchar](2) NULL;
    PRINT 'AccessTracking.CountryCode added.';
END
ELSE
BEGIN
    PRINT 'AccessTracking.CountryCode already exists.';
END
GO

-- ============================================================
-- STEP 5: the rows written before 'Portal' existed all came from the storefront, and saying so is what
--         lets the report tell the storefront's visitors from the admin team's sessions. Only rows with no
--         portal at all are touched, so running this again writes nothing.
-- ============================================================
UPDATE [dbo].[AccessTracking]
SET [Portal] = 'Web'
WHERE [Portal] IS NULL;

PRINT 'Rows with no portal set to ''Web'' (' + CAST(@@ROWCOUNT AS VARCHAR(20)) + ' row(s) - 0 on a second run).';
GO

-- ============================================================
-- STEP 6: the indexes the report reads, so a month of traffic is not a table scan per screen
-- ============================================================
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.AccessTracking') AND name = 'IX_AccessTracking_AccessOn'
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessTracking_AccessOn]
        ON [dbo].[AccessTracking] ([AccessOn] DESC)
        INCLUDE ([IP], [Portal], [City], [Region], [Country], [CountryCode]);

    PRINT 'Index IX_AccessTracking_AccessOn created.';
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.AccessTracking') AND name = 'IX_AccessTracking_Portal_AccessOn'
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessTracking_Portal_AccessOn]
        ON [dbo].[AccessTracking] ([Portal], [AccessOn] DESC)
        INCLUDE ([IP], [Country], [CountryCode], [Region], [City]);

    PRINT 'Index IX_AccessTracking_Portal_AccessOn created.';
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.AccessTracking') AND name = 'IX_AccessTracking_Country_AccessOn'
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessTracking_Country_AccessOn]
        ON [dbo].[AccessTracking] ([Country], [AccessOn] DESC)
        INCLUDE ([IP], [Region], [City], [CountryCode]);

    PRINT 'Index IX_AccessTracking_Country_AccessOn created.';
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.AccessTracking') AND name = 'IX_AccessTracking_IP_AccessOn'
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessTracking_IP_AccessOn]
        ON [dbo].[AccessTracking] ([IP], [AccessOn] DESC);

    PRINT 'Index IX_AccessTracking_IP_AccessOn created - counting visitors is the report''s heaviest read.';
END
GO

-- ============================================================
-- STEP 7: the view this table has always had, for a database that has none. It is not what the admin
--         screen reads (the screen counts every portal and every country), and an existing view is left
--         exactly as it is.
-- ============================================================
IF OBJECT_ID(N'dbo.V_ACCESSTRACKING', N'V') IS NULL
BEGIN
    EXEC (N'
        CREATE VIEW [dbo].[V_ACCESSTRACKING]
        AS
        SELECT [IP], [URL], [AccessOn],
               CAST(YEAR([AccessOn]) AS VARCHAR(4)) + ''-'' + RIGHT(''0'' + CAST(MONTH([AccessOn]) AS VARCHAR(2)), 2) AS [Year-Month],
               [City], [Region], [Country], [Organization]
        FROM [dbo].[AccessTracking]
        WHERE [Country] = ''India'';');

    PRINT 'View V_ACCESSTRACKING created.';
END
ELSE
BEGIN
    PRINT 'View V_ACCESSTRACKING already exists - left as it is.';
END
GO

-- ============================================================
-- NOTE, NOT RUN BY THIS SCRIPT: the rows written before this script ran have no 'CountryCode' (the location was
-- looked up by a different, earlier tracker) and no region-level code, so the report groups them by country name
-- alone while the new rows are grouped by their ISO code as well. Nothing is rewritten here, because the old
-- rows are not wrong - they are simply older than the columns. They keep their place in the history and are
-- removed by the screen's own retention action when they are no longer wanted.
-- ============================================================

-- ============================================================
-- VERIFICATION: uncomment to check what was written
-- ============================================================
-- SELECT c.name, t.name AS Type, c.max_length, c.is_nullable
-- FROM sys.columns c JOIN sys.types t ON c.user_type_id = t.user_type_id
-- WHERE c.object_id = OBJECT_ID('dbo.AccessTracking') ORDER BY c.column_id;
--
-- SELECT i.name, i.type_desc FROM sys.indexes i WHERE i.object_id = OBJECT_ID('dbo.AccessTracking');
--
-- SELECT [Portal], COUNT(*) AS Hits, COUNT(DISTINCT [IP]) AS Visitors FROM [dbo].[AccessTracking] GROUP BY [Portal];

