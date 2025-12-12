-- SQL Script to create Drivers and CarDatas tables
-- This script should be run before running update-database

USE [mobileSystem];
GO

-- Create Drivers table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Drivers]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Drivers] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [DriverPhoto] nvarchar(max) NOT NULL,
        [DriverIdCard] nvarchar(max) NOT NULL,
        [DriverLicenseFront] nvarchar(max) NOT NULL,
        [DriverLicenseBack] nvarchar(max) NOT NULL,
        [IdCardFront] nvarchar(max) NOT NULL,
        [IdCardBack] nvarchar(max) NOT NULL,
        [DriverFullname] nvarchar(max) NOT NULL,
        [NationalId] nvarchar(max) NOT NULL,
        [Age] int NOT NULL,
        [LicenseNumber] nvarchar(max) NOT NULL,
        [Email] nvarchar(max) NOT NULL,
        [Password] nvarchar(max) NOT NULL,
        [LicenseExpiryDate] datetime2 NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_Drivers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Drivers_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
    
    CREATE INDEX [IX_Drivers_UserId] ON [dbo].[Drivers] ([UserId]);
    
    PRINT 'Drivers table created successfully';
END
ELSE
BEGIN
    PRINT 'Drivers table already exists';
END
GO

-- Create CarDatas table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CarDatas]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CarDatas] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [CarPhoto] nvarchar(max) NOT NULL,
        [LicenseFront] nvarchar(max) NOT NULL,
        [LicenseBack] nvarchar(max) NOT NULL,
        [CarBrand] nvarchar(max) NOT NULL,
        [CarModel] nvarchar(max) NOT NULL,
        [CarColor] nvarchar(max) NOT NULL,
        [PlateNumber] nvarchar(max) NOT NULL,
        [DriverId] int NOT NULL,
        CONSTRAINT [PK_CarDatas] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CarDatas_Drivers_DriverId] FOREIGN KEY ([DriverId]) REFERENCES [dbo].[Drivers] ([Id]) ON DELETE CASCADE
    );
    
    CREATE INDEX [IX_CarDatas_DriverId] ON [dbo].[CarDatas] ([DriverId]);
    
    PRINT 'CarDatas table created successfully';
END
ELSE
BEGIN
    PRINT 'CarDatas table already exists';
END
GO

PRINT 'Script completed successfully!';
GO

