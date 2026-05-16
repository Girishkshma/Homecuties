-- ============================================================
-- Script: AddCustomerManagementMenu.sql
-- Purpose: Add Customer Management menu to the admin panel
--          Only Super Admin (RoleID=1) and Admin (RoleID=2)
--          should have access to manage customers.
-- Tables affected:
--   - AdminMenus (insert menu)
--   - AdminMenusRoles (assign menu to roles)
--   - AdminActivities (insert activities for the menu)
--   - AdminActivitiesRoles (assign activities to roles)
-- ============================================================

-- ============================================================
-- STEP 1: Find the next available MenuID
-- ============================================================
DECLARE @NextMenuID SMALLINT;
SELECT @NextMenuID = ISNULL(MAX(MenuID), 0) + 1 FROM AdminMenus;

-- ============================================================
-- STEP 2: Find the next available ActivityID
-- ============================================================
DECLARE @NextActivityID SMALLINT;
SELECT @NextActivityID = ISNULL(MAX(ActivityID), 0) + 1 FROM AdminActivities;

-- ============================================================
-- STEP 3: Insert the Customer Management menu
--         (ParentMenuID = NULL means it's a top-level menu)
-- ============================================================
INSERT INTO AdminMenus (MenuID, MenuTitle, MenuDescription, MenuURL, ParentMenuID, IsActive)
VALUES (
    @NextMenuID,
    'Customers',
    'Manage customer accounts, view customer details, and update customer statuses.',
    '/customers',
    NULL,  -- Top-level menu (no parent)
    1      -- Active
);

-- ============================================================
-- STEP 4: Assign the Customer Management menu to roles
--         RoleID 1 = Super Admin
--         RoleID 2 = Admin
-- ============================================================
INSERT INTO AdminMenusRoles (MenuID, RoleID, IsActive)
VALUES
    (@NextMenuID, 1, 1),  -- Super Admin
    (@NextMenuID, 2, 1);  -- Admin

-- ============================================================
-- STEP 5: Insert activities for the Customer Management menu
--         These define what actions users can perform
-- ============================================================

-- Activity: View Customers
INSERT INTO AdminActivities (ActivityID, ActivityTitle, MenuID, IsActive)
VALUES (@NextActivityID, 'View Customers', @NextMenuID, 1);

-- Assign View Customers to Super Admin and Admin
INSERT INTO AdminActivitiesRoles (ActivityID, RoleID, IsActive)
VALUES
    (@NextActivityID, 1, 1),  -- Super Admin
    (@NextActivityID, 2, 1);  -- Admin

SET @NextActivityID = @NextActivityID + 1;

-- Activity: View Customer Details
INSERT INTO AdminActivities (ActivityID, ActivityTitle, MenuID, IsActive)
VALUES (@NextActivityID, 'View Customer Details', @NextMenuID, 1);

-- Assign View Customer Details to Super Admin and Admin
INSERT INTO AdminActivitiesRoles (ActivityID, RoleID, IsActive)
VALUES
    (@NextActivityID, 1, 1),  -- Super Admin
    (@NextActivityID, 2, 1);  -- Admin

SET @NextActivityID = @NextActivityID + 1;

-- Activity: Update Customer Status
INSERT INTO AdminActivities (ActivityID, ActivityTitle, MenuID, IsActive)
VALUES (@NextActivityID, 'Update Customer Status', @NextMenuID, 1);

-- Assign Update Customer Status to Super Admin and Admin
INSERT INTO AdminActivitiesRoles (ActivityID, RoleID, IsActive)
VALUES
    (@NextActivityID, 1, 1),  -- Super Admin
    (@NextActivityID, 2, 1);  -- Admin

SET @NextActivityID = @NextActivityID + 1;

-- Activity: Search Customers
INSERT INTO AdminActivities (ActivityID, ActivityTitle, MenuID, IsActive)
VALUES (@NextActivityID, 'Search Customers', @NextMenuID, 1);

-- Assign Search Customers to Super Admin and Admin
INSERT INTO AdminActivitiesRoles (ActivityID, RoleID, IsActive)
VALUES
    (@NextActivityID, 1, 1),  -- Super Admin
    (@NextActivityID, 2, 1);  -- Admin

-- ============================================================
-- VERIFICATION: Uncomment to verify the inserted data
-- ============================================================
-- SELECT * FROM AdminMenus WHERE MenuID = @NextMenuID;
-- SELECT * FROM AdminMenusRoles WHERE MenuID = @NextMenuID;
-- SELECT * FROM AdminActivities WHERE MenuID = @NextMenuID;
-- SELECT * FROM AdminActivitiesRoles WHERE ActivityID IN (
--     SELECT ActivityID FROM AdminActivities WHERE MenuID = @NextMenuID
-- );

PRINT 'Customer Management menu has been added successfully.';
PRINT 'MenuID: ' + CAST(@NextMenuID AS VARCHAR);
PRINT 'Assigned to roles: Super Admin (RoleID=1), Admin (RoleID=2)';
