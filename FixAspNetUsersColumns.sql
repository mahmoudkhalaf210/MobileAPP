-- SQL Script to fix AspNetUsers table columns
-- Add missing FullName and UserType columns

USE [mobileSystem];
GO

-- Check if FullName column exists, if not add it
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AspNetUsers]') AND name = 'FullName')
BEGIN
    -- If DispalyName exists, rename it to FullName
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AspNetUsers]') AND name = 'DispalyName')
    BEGIN
        EXEC sp_rename 'AspNetUsers.DispalyName', 'FullName', 'COLUMN';
        PRINT 'Renamed DispalyName to FullName';
    END
    ELSE
    BEGIN
        ALTER TABLE [AspNetUsers] ADD [FullName] nvarchar(max) NOT NULL DEFAULT '';
        PRINT 'Added FullName column';
    END
END
ELSE
BEGIN
    PRINT 'FullName column already exists';
END
GO

-- Check if UserType column exists, if not add it
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AspNetUsers]') AND name = 'UserType')
BEGIN
    ALTER TABLE [AspNetUsers] ADD [UserType] nvarchar(max) NOT NULL DEFAULT '';
    PRINT 'Added UserType column';
END
ELSE
BEGIN
    PRINT 'UserType column already exists';
END
GO

PRINT 'Script completed successfully!';
GO

