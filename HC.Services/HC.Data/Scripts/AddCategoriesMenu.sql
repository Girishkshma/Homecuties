-- ============================================================
-- Script: AddCategoriesMenu.sql
-- Purpose: Add the Categories menu - the catalogue screen ('/categories') - to the admin panel, and grant it to
--          Super Admin (RoleID = 1) and Admin (RoleID = 2).
-- Tables affected:
--   - AdminMenus            (insert the menu)
--   - AdminMenusRoles       (map it to the two roles)
--   - AdminActivities       (insert the activities the screen offers)
--   - AdminActivitiesRoles  (map those activities to the two roles)
--
-- The screen manages the shop's own catalogue: the headings and the shelves beneath them that products are filed
-- under. It adds a category, renames one, moves one - a shelf can be made a heading, and a heading a shelf of
-- another - and takes one out. What may be written is decided by the API (HC.Business.CategoryCatalog, where each
-- rule and its reasons live), and what a role may open is this menu: the category endpoints beside the section the
-- product form already reads ('/products|/users') are guarded by the '/categories' section alone, so a role that is
-- not mapped here in AdminMenusRoles is refused both the page and the calls behind it.
--
-- The Activities below are the vocabulary of the screen for the admin area's own role administration; the section
-- mapping above is what actually grants or refuses the page and its API.
--
-- Safe to run on any environment and more than once: an existing '/categories' menu is left alone (only switched back
-- on if it had been turned off), and its role and activity mappings are added only where they are missing.
-- ============================================================

SET NOCOUNT ON;

DECLARE @MenuID SMALLINT = (SELECT MenuID FROM AdminMenus WHERE MenuURL = '/categories');
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
        'Categories',
        'The shop''s own catalogue: the headings and the shelves beneath them that products are filed under. Add a '
            + 'category, rename one, move one, or take one out.',
        '/categories',
        NULL,  -- Top-level menu (no parent)
        1      -- Active
    );

    PRINT 'Categories menu added (MenuID ' + CAST(@MenuID AS VARCHAR(10)) + ').';
END
ELSE
BEGIN
    UPDATE AdminMenus SET IsActive = 1 WHERE MenuID = @MenuID;

    PRINT 'Categories menu already exists (MenuID ' + CAST(@MenuID AS VARCHAR(10)) + ') - left as it is.';
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

PRINT 'Categories menu mapped to Super Admin (1) and Admin (2).';

-- ============================================================
-- STEP 3: what the screen offers - reading the catalogue, adding to it, editing a category, and taking one out.
--         Each is inserted only when it is not there yet, so a re-run adds nothing twice.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM AdminActivities WHERE MenuID = @MenuID AND ActivityTitle = 'View Categories')
BEGIN
    SELECT @ActivityID = ISNULL(MAX(ActivityID), 0) + 1 FROM AdminActivities;

    INSERT INTO AdminActivities (ActivityID, ActivityTitle, MenuID, IsActive)
    VALUES (@ActivityID, 'View Categories', @MenuID, 1);

    PRINT '''View Categories'' activity added (ActivityID ' + CAST(@ActivityID AS VARCHAR(10)) + ').';
END

IF NOT EXISTS (SELECT 1 FROM AdminActivities WHERE MenuID = @MenuID AND ActivityTitle = 'Add Category')
BEGIN
    SELECT @ActivityID = ISNULL(MAX(ActivityID), 0) + 1 FROM AdminActivities;

    INSERT INTO AdminActivities (ActivityID, ActivityTitle, MenuID, IsActive)
    VALUES (@ActivityID, 'Add Category', @MenuID, 1);

    PRINT '''Add Category'' activity added (ActivityID ' + CAST(@ActivityID AS VARCHAR(10)) + ').';
END

IF NOT EXISTS (SELECT 1 FROM AdminActivities WHERE MenuID = @MenuID AND ActivityTitle = 'Edit Category')
BEGIN
    SELECT @ActivityID = ISNULL(MAX(ActivityID), 0) + 1 FROM AdminActivities;

    INSERT INTO AdminActivities (ActivityID, ActivityTitle, MenuID, IsActive)
    VALUES (@ActivityID, 'Edit Category', @MenuID, 1);

    PRINT '''Edit Category'' activity added (ActivityID ' + CAST(@ActivityID AS VARCHAR(10)) + ').';
END

IF NOT EXISTS (SELECT 1 FROM AdminActivities WHERE MenuID = @MenuID AND ActivityTitle = 'Delete Category')
BEGIN
    SELECT @ActivityID = ISNULL(MAX(ActivityID), 0) + 1 FROM AdminActivities;

    INSERT INTO AdminActivities (ActivityID, ActivityTitle, MenuID, IsActive)
    VALUES (@ActivityID, 'Delete Category', @MenuID, 1);

    PRINT '''Delete Category'' activity added (ActivityID ' + CAST(@ActivityID AS VARCHAR(10)) + ').';
END

-- ============================================================
-- STEP 4: who may do them - the same two roles, wherever a mapping is missing.
-- ============================================================
INSERT INTO AdminActivitiesRoles (ActivityID, RoleID, IsActive)
SELECT activities.ActivityID, roles.RoleID, 1
FROM AdminActivities activities
CROSS JOIN (VALUES (CAST(1 AS SMALLINT)), (CAST(2 AS SMALLINT))) AS roles(RoleID)
WHERE activities.MenuID = @MenuID
AND NOT EXISTS (
    SELECT 1 FROM AdminActivitiesRoles aar
    WHERE aar.ActivityID = activities.ActivityID AND aar.RoleID = roles.RoleID
);

PRINT 'Categories activities mapped to Super Admin (1) and Admin (2).';

-- ============================================================
-- VERIFICATION: uncomment to check what was written
-- ============================================================
-- SELECT * FROM AdminMenus WHERE MenuID = @MenuID;
-- SELECT * FROM AdminMenusRoles WHERE MenuID = @MenuID;
-- SELECT * FROM AdminActivities WHERE MenuID = @MenuID;
-- SELECT aar.* FROM AdminActivitiesRoles aar
--     JOIN AdminActivities a ON a.ActivityID = aar.ActivityID WHERE a.MenuID = @MenuID;
