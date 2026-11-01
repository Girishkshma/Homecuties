-- ============================================================
-- Script: AddAccessTrackingSharedLocation.sql
-- Purpose: Let a portal access carry the point a visitor chose to share - the location their browser
--          reported after they pressed 'Share my location' - beside the place the address was looked up
--          to, and not instead of it.
-- Tables affected:
--   - AccessTracking   (add 'Latitude', 'Longitude' and 'AccuracyMeters', all nullable)
--
-- Why the columns are added and why they are 'float':
--   The tracker has always placed a hit by the address it came from (HC.Business.Geo.IpGeoLocator). A
--   visitor may now agree to be placed more precisely, and what their browser reports is a coordinate -
--   three numbers, not a place name. They are stored as 'float' (T-SQL's double) because that is exactly
--   what the API receives and what it keeps three decimals of (AccessTrackingRules.ResolveSharedPoint,
--   about a hundred metres); a 'decimal' column would claim a fixed number of digits the browser never
--   promised.
--
-- Why they sit beside City/Region/Country rather than replacing them:
--   The place columns are the report's own answer about the caller's address, and they are what the
--   Portal Access screen counts by. The point is the visitor's own addition and is only ever written when
--   they agreed to share it, so:
--     - Latitude/Longitude/AccuracyMeters all NULL  a hit recorded without consent - which is what every
--                                                   hit was before the shop began asking, and what most
--                                                   still are. Such a row reads exactly as it always has.
--     - Latitude/Longitude set                      the visitor agreed, and the row also carries where
--                                                   their device said it was.
--
-- Safe to run on any environment and more than once: each column is asked about before it is added and
-- nothing here rewrites or deletes a row. AddPortalAccessTracking.sql creates a fresh table with these
-- three columns already in it (the API writes them on every insert), so on a database that was just built
-- by that script every step below simply reports that it is already done; this script is what brings a
-- table that predates the share up to the same shape.
-- ============================================================

SET NOCOUNT ON;

-- ============================================================
-- STEP 1: where the visitor said they were, when they agreed to say - added to the table the tracker
--         already writes, and skipped entirely (with a word about it) on a database that has no table.
-- ============================================================
IF OBJECT_ID(N'dbo.AccessTracking', N'U') IS NULL
BEGIN
    PRINT 'AccessTracking does not exist - run AddPortalAccessTracking.sql first. Nothing was changed.';
END
ELSE
BEGIN
    IF COL_LENGTH('dbo.AccessTracking', 'Latitude') IS NULL
    BEGIN
        ALTER TABLE [dbo].[AccessTracking] ADD [Latitude] [float] NULL;
        PRINT 'AccessTracking.Latitude added.';
    END
    ELSE
    BEGIN
        PRINT 'AccessTracking.Latitude already exists.';
    END

    IF COL_LENGTH('dbo.AccessTracking', 'Longitude') IS NULL
    BEGIN
        ALTER TABLE [dbo].[AccessTracking] ADD [Longitude] [float] NULL;
        PRINT 'AccessTracking.Longitude added.';
    END
    ELSE
    BEGIN
        PRINT 'AccessTracking.Longitude already exists.';
    END

    IF COL_LENGTH('dbo.AccessTracking', 'AccuracyMeters') IS NULL
    BEGIN
        ALTER TABLE [dbo].[AccessTracking] ADD [AccuracyMeters] [float] NULL;
        PRINT 'AccessTracking.AccuracyMeters added.';
    END
    ELSE
    BEGIN
        PRINT 'AccessTracking.AccuracyMeters already exists.';
    END
END
GO

-- ============================================================
-- NOTE, NOT RUN BY THIS SCRIPT: the rows written before this script ran have no point at all, and that is
-- correct - none of those visitors was asked. A read that wants the shared hits alone filters them with
-- 'WHERE [Latitude] IS NOT NULL'; there is no index for that, because the log is read newest first over a
-- period and the screen that will want the shared hits will want them inside that same period.
-- ============================================================

-- ============================================================
-- VERIFICATION: uncomment to check what was written
-- ============================================================
-- SELECT c.name, t.name AS Type, c.is_nullable
-- FROM sys.columns c JOIN sys.types t ON c.user_type_id = t.user_type_id
-- WHERE c.object_id = OBJECT_ID('dbo.AccessTracking') AND c.name IN ('Latitude', 'Longitude', 'AccuracyMeters');
--
-- SELECT COUNT(*) AS SharedHits, COUNT(DISTINCT [IP]) AS Visitors
-- FROM [dbo].[AccessTracking] WHERE [Latitude] IS NOT NULL;
