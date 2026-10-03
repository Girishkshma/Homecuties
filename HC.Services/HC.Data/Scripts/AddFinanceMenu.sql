-- ============================================================
-- Script: AddFinanceMenu.sql
-- Purpose: Add the Finance menu - the shop's own books for a period ('/finance') - to the admin panel,
--          and grant it to Super Admin (RoleID = 1) and Admin (RoleID = 2).
-- Tables affected:
--   - AdminMenus            (insert the menu)
--   - AdminMenusRoles       (map it to the two roles)
--   - AdminActivities       (insert the 'View Finance' activity)
--   - AdminActivitiesRoles  (map that activity to the two roles)
--
-- The screen the menu opens reads the money sections ('/orders' or '/finance'): payments, refunds, what the
-- gateway kept, what the parcels cost. That is why the API's finance endpoint accepts either section, and why
-- an admin who may already open the orders section can reach the screen even before this menu is seeded.
-- Seeding it is what puts the link in the sidebar, and what lets a later role be given the books on their own
-- without handing over the order screens.
--
-- Safe to run on any environment and more than once: an existing '/finance' menu is left alone (only switched
-- back on if it had been turned off) and its role and activity mappings are added only where they are missing.
-- ============================================================

SET NOCOUNT ON;

DECLARE @MenuID SMALLINT = (SELECT MenuID FROM AdminMenus WHERE MenuURL = '/finance');
DECLARE @ActivityID SMALLINT;

-- ============================================================
-- STEP 1: the menu itself (top level: ParentMenuID = NULL)
-- ============================================================
IF @MenuID IS NULL
BEGIN
    SELECT @MenuID = ISNULL(MAX(MenuID), 0) + 1 FROM AdminMenus;

    INSERT INTO AdminMenus (MenuID, MenuTitle, MenuDescription, MenuURL, ParentMenuID, IsActive)
    VALUES (
        @MenuID,
        'Finance',
        'The shop''s own books over a period: what the customers paid, what was refunded, what the gateway '
            + 'kept, what the couriers were paid, and what the shop''s own declared margin on the goods was.',
        '/finance',
        NULL,  -- Top-level menu (no parent)
        1      -- Active
    );

    PRINT 'Finance menu added (MenuID ' + CAST(@MenuID AS VARCHAR(10)) + ').';
END
ELSE
BEGIN
    -- It is there but may have been switched off: the code this script ships with expects it, so it goes back on.
    UPDATE AdminMenus SET IsActive = 1 WHERE MenuID = @MenuID;

    PRINT 'Finance menu already exists (MenuID ' + CAST(@MenuID AS VARCHAR(10)) + ') - left as it is.';
END

-- ============================================================
-- STEP 2: who may see it - Super Admin (1) and Admin (2)
-- ============================================================
INSERT INTO AdminMenusRoles (MenuID, RoleID, IsActive)
SELECT @MenuID, roles.RoleID, 1
FROM (VALUES (CAST(1 AS SMALLINT)), (CAST(2 AS SMALLINT))) AS roles(RoleID)
WHERE NOT EXISTS (
    SELECT 1 FROM AdminMenusRoles amr
    WHERE amr.MenuID = @MenuID AND amr.RoleID = roles.RoleID
);

PRINT 'Finance menu mapped to Super Admin (1) and Admin (2).';

-- ============================================================
-- STEP 3: what the menu lets a role do - reading the books, nothing else.
--         The screen writes nothing: pulling the gateway's settlement books (which feeds it) is the
--         orders section's own action on the Dashboard.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM AdminActivities WHERE MenuID = @MenuID AND ActivityTitle = 'View Finance')
BEGIN
    SELECT @ActivityID = ISNULL(MAX(ActivityID), 0) + 1 FROM AdminActivities;

    INSERT INTO AdminActivities (ActivityID, ActivityTitle, MenuID, IsActive)
    VALUES (@ActivityID, 'View Finance', @MenuID, 1);

    INSERT INTO AdminActivitiesRoles (ActivityID, RoleID, IsActive)
    SELECT @ActivityID, roles.RoleID, 1
    FROM (VALUES (CAST(1 AS SMALLINT)), (CAST(2 AS SMALLINT))) AS roles(RoleID)
    WHERE NOT EXISTS (
        SELECT 1 FROM AdminActivitiesRoles aar
        WHERE aar.ActivityID = @ActivityID AND aar.RoleID = roles.RoleID
    );

    PRINT '''View Finance'' activity added (ActivityID ' + CAST(@ActivityID AS VARCHAR(10)) + ').';
END
ELSE
BEGIN
    PRINT '''View Finance'' activity already exists - left as it is.';
END

-- ============================================================
-- VERIFICATION: uncomment to check what was written
-- ============================================================
-- SELECT * FROM AdminMenus WHERE MenuID = @MenuID;
-- SELECT * FROM AdminMenusRoles WHERE MenuID = @MenuID;
-- SELECT * FROM AdminActivities WHERE MenuID = @MenuID;
-- SELECT * FROM AdminActivitiesRoles WHERE ActivityID = @ActivityID;
