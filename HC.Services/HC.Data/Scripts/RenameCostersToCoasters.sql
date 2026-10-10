-- ============================================================
-- Script: RenameCostersToCoasters.sql
-- Purpose: The shelf beneath the 'Households' heading is spelled 'Costers' in
--          Categories (CategoryID 12), but the one product filed on it is a
--          'Tea Coaster' (ProductID 10018): the row is misspelled for the thing it
--          holds. That name is not internal - the shop's customers read it on the
--          home page's category tile, the shop's category buttons and the side
--          menu, and the shop reads it on the dashboard's 'Stock by category'
--          card - so it is corrected to 'Coasters'.
-- Tables affected:
--   - Categories (CategoryID 12, CategoryName only; no other column moves)
-- Nothing else changes with it: the row keeps its id 12, keeps its parent 4
-- ('Households'), and the product links are untouched, because the app reads a
-- category by id and never by name.
-- HC.Web keys the home page's tile artwork off the NAME (HomeComponent.categoryImageMap),
-- so that map - and the SVG it points at - were renamed in the same change.
-- Safe to run more than once.
-- ============================================================

SET NOCOUNT ON;

-- Told apart by the id, so a row someone has already renamed, or an id the shop has
-- reused for a different shelf, is left alone. The name check keeps the correction from
-- ever creating a second 'Coasters'.
IF EXISTS (SELECT 1 FROM dbo.Categories WHERE CategoryID = 12 AND CategoryName = 'Costers')
   AND NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE CategoryName = 'Coasters')
BEGIN
    UPDATE dbo.Categories
    SET CategoryName = 'Coasters'
    WHERE CategoryID = 12;

    PRINT 'Categories.CategoryID 12 renamed: Costers -> Coasters.';
END
ELSE
BEGIN
    PRINT 'Nothing to do: CategoryID 12 is not named Costers, or another row already holds that name.';
END

-- What the 'Households' heading now reads, and how much is filed under each of its shelves.
SELECT c.CategoryID,
       c.CategoryName,
       (SELECT COUNT(*) FROM dbo.ProductCategories pc WHERE pc.CategoryID = c.CategoryID) AS Products
FROM dbo.Categories c
WHERE c.ParentCategoryID = 4
ORDER BY c.CategoryName;
