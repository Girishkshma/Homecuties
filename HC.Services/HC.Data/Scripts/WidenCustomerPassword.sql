-- ============================================================
-- Script: WidenCustomerPassword.sql
-- Purpose: Customers.Password is varchar(50) and used to hold plain-text
--          passwords. Passwords are now stored as PBKDF2-SHA256 hashes
--          (format: pbkdf2$sha256$<iterations>$<saltBase64>$<hashBase64>,
--          ~90 characters), so the column must be widened.
-- Tables affected:
--   - Customers (alter column)
-- Safe to run more than once.
-- ============================================================

SET NOCOUNT ON;

ALTER TABLE dbo.Customers ALTER COLUMN Password VARCHAR(200) NULL;

PRINT 'Customers.Password widened to varchar(200).';

SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Customers' AND COLUMN_NAME = 'Password';
