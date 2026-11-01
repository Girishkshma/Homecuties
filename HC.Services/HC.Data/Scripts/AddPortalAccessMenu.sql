-- ============================================================
-- Script: AddPortalAccessMenu.sql
-- Purpose: Add the Portal Access menu - where the shop is being read from, and how often ('/portal-access') -
--          to the admin panel, and grant it to Super Admin (RoleID = 1) and Admin (RoleID = 2).
-- Tables affected:
--   - AdminMenus            (insert the menu)
--   - AdminMenusRoles       (map it to the two roles)
--   - AdminActivities       (insert the 'View Portal Access' activity)
--   - AdminActivitiesRoles  (map that activity to the two roles)
--
-- The screen reads the rows the storefront records on every page load (see HC.Business.AccessTrackingService),
-- and it is what the API checks for ('AdminPolicies.PortalAccess') on 'api/admin/access/summary', '.../log' and
-- '.../purge'. Nothing else is needed for the tracking to work: the hits are recorded whether this menu exists
-- or not - seeding it is what puts the screen in the sidebar and lets a later role be given the country the
-- shop is read from without handing over anything else.
--
-- Safe to run on any environment and more than once: an existing '/portal-access' menu is left alone (only
-- switched back on if it had been turned off) and its role and activity mappings are added only where missing.
-- ============================================================

SET NOCOUNT ON;

DECLARE @MenuID SMALLINT = (SELECT MenuID FROM AdminMenus WHERE MenuURL = '/portal-access');
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
        'Portal Access',
        'Where the shop is being read from, and how often: every visit the storefront records, counted by '
            + 'country, region and city, with the frequency over the period - a location and a count, never a '
            + 'person. The log behind the figures is on the same screen.',
        '/portal-access',
        NULL,  -- Top-level menu (no parent)
        1      -- Active
    );

    PRINT 'Portal Access menu added (MenuID ' + CAST(@MenuID AS VARCHAR(10)) + ').';
END
ELSE
BEGIN
    -- It is there but may have been switched off: the code this script ships with expects it, so it goes back on.
    UPDATE AdminMenus SET IsActive = 1 WHERE MenuID = @MenuID;

    PRINT 'Portal Access menu already exists (MenuID ' + CAST(@MenuID AS VARCHAR(10)) + ') - left as it is.';
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

PRINT 'Portal Access menu mapped to Super Admin (1) and Admin (2).';

-- ============================================================
-- STEP 3: what the menu lets a role do - reading the figures, and the one action that keeps the table from
--         growing without end (removing the history older than a given number of days). Recording a hit needs
--         no activity at all: that is the storefront's own anonymous call ('Utilities/AddAccessTracking'),
--         which no admin role is involved in.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM AdminActivities WHERE MenuID = @MenuID AND ActivityTitle = 'View Portal Access')
BEGIN
    SELECT @ActivityID = ISNULL(MAX(ActivityID), 0) + 1 FROM AdminActivities;

    INSERT INTO AdminActivities (ActivityID, ActivityTitle, MenuID, IsActive)
    VALUES (@ActivityID, 'View Portal Access', @MenuID, 1);

    INSERT INTO AdminActivitiesRoles (ActivityID, RoleID, IsActive)
    SELECT @ActivityID, roles.RoleID, 1
    FROM (VALUES (CAST(1 AS SMALLINT)), (CAST(2 AS SMALLINT))) AS roles(RoleID)
    WHERE NOT EXISTS (
        SELECT 1 FROM AdminActivitiesRoles aar
        WHERE aar.ActivityID = @ActivityID AND aar.RoleID = roles.RoleID
    );

    PRINT '''View Portal Access'' activity added (ActivityID ' + CAST(@ActivityID AS VARCHAR(10)) + ').';
END
ELSE
BEGIN
    PRINT '''View Portal Access'' activity already exists - left as it is.';
END

-- ============================================================
-- VERIFICATION: uncomment to check what was written
-- ============================================================
-- SELECT * FROM AdminMenus WHERE MenuID = @MenuID;
-- SELECT * FROM AdminMenusRoles WHERE MenuID = @MenuID;
-- SELECT * FROM AdminActivities WHERE MenuID = @MenuID;
-- SELECT * FROM AdminActivitiesRoles WHERE ActivityID = @ActivityID;
