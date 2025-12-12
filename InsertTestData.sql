-- Test Data Script for Snap & Shape Backend
-- Run this script to insert test data for all tables

USE [mobileSystem];
GO

-- Clear existing data (optional - comment out if you want to keep existing data)
-- DELETE FROM [UserHistories];
-- DELETE FROM [TripsHistories];
-- DELETE FROM [Charges];
-- DELETE FROM [Orders];
-- DELETE FROM [CarDatas];
-- DELETE FROM [Drivers];
-- DELETE FROM [AspNetUsers];

-- ============================================
-- 1. Insert Test Users (AspNetUsers)
-- ============================================
-- Note: PasswordHash is for password "Test@123" - you can change it
DECLARE @User1Id NVARCHAR(450) = NEWID();
DECLARE @User2Id NVARCHAR(450) = NEWID();
DECLARE @User3Id NVARCHAR(450) = NEWID();
DECLARE @Driver1Id NVARCHAR(450) = NEWID();
DECLARE @Driver2Id NVARCHAR(450) = NEWID();

-- User 1 - Passenger
INSERT INTO [AspNetUsers] ([Id], [FullName], [UserName], [NormalizedUserName], [Email], [NormalizedEmail], 
    [EmailConfirmed], [PasswordHash], [SecurityStamp], [ConcurrencyStamp], [PhoneNumber], 
    [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEnabled], [AccessFailedCount], [UserType], [Gender], [Image])
VALUES 
(@User1Id, N'Ahmed Mohamed', N'ahmed.mohamed', N'AHMED.MOHAMED', N'ahmed@test.com', N'AHMED@TEST.COM',
    1, N'AQAAAAIAAYagAAAAE...', NEWID(), NEWID(), N'01012345678', 1, 0, 1, 0, N'Passenger', N'male', N'');

-- User 2 - Passenger
INSERT INTO [AspNetUsers] ([Id], [FullName], [UserName], [NormalizedUserName], [Email], [NormalizedEmail], 
    [EmailConfirmed], [PasswordHash], [SecurityStamp], [ConcurrencyStamp], [PhoneNumber], 
    [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEnabled], [AccessFailedCount], [UserType], [Gender], [Image])
VALUES 
(@User2Id, N'Sara Ali', N'sara.ali', N'SARA.ALI', N'sara@test.com', N'SARA@TEST.COM',
    1, N'AQAAAAIAAYagAAAAE...', NEWID(), NEWID(), N'01087654321', 1, 0, 1, 0, N'Passenger', N'female', N'');

-- User 3 - Passenger
INSERT INTO [AspNetUsers] ([Id], [FullName], [UserName], [NormalizedUserName], [Email], [NormalizedEmail], 
    [EmailConfirmed], [PasswordHash], [SecurityStamp], [ConcurrencyStamp], [PhoneNumber], 
    [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEnabled], [AccessFailedCount], [UserType], [Gender], [Image])
VALUES 
(@User3Id, N'Mohamed Hassan', N'mohamed.hassan', N'MOHAMED.HASSAN', N'mohamed@test.com', N'MOHAMED@TEST.COM',
    1, N'AQAAAAIAAYagAAAAE...', NEWID(), NEWID(), N'01123456789', 1, 0, 1, 0, N'Passenger', N'male', N'');

-- Driver 1 User
INSERT INTO [AspNetUsers] ([Id], [FullName], [UserName], [NormalizedUserName], [Email], [NormalizedEmail], 
    [EmailConfirmed], [PasswordHash], [SecurityStamp], [ConcurrencyStamp], [PhoneNumber], 
    [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEnabled], [AccessFailedCount], [UserType], [Gender], [Image])
VALUES 
(@Driver1Id, N'Khaled Ibrahim', N'khaled.ibrahim', N'KHALED.IBRAHIM', N'khaled@driver.com', N'KHALED@DRIVER.COM',
    1, N'AQAAAAIAAYagAAAAE...', NEWID(), NEWID(), N'01234567890', 1, 0, 1, 0, N'Driver', N'male', N'');

-- Driver 2 User
INSERT INTO [AspNetUsers] ([Id], [FullName], [UserName], [NormalizedUserName], [Email], [NormalizedEmail], 
    [EmailConfirmed], [PasswordHash], [SecurityStamp], [ConcurrencyStamp], [PhoneNumber], 
    [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEnabled], [AccessFailedCount], [UserType], [Gender], [Image])
VALUES 
(@Driver2Id, N'Fatma Ahmed', N'fatma.ahmed', N'FATMA.AHMED', N'fatma@driver.com', N'FATMA@DRIVER.COM',
    1, N'AQAAAAIAAYagAAAAE...', NEWID(), NEWID(), N'01234567891', 1, 0, 1, 0, N'Driver', N'female', N'');

PRINT 'Users inserted successfully';
GO

-- ============================================
-- 2. Insert Test Drivers
-- ============================================
DECLARE @Driver1UserId NVARCHAR(450);
DECLARE @Driver2UserId NVARCHAR(450);
SELECT @Driver1UserId = [Id] FROM [AspNetUsers] WHERE [Email] = N'khaled@driver.com';
SELECT @Driver2UserId = [Id] FROM [AspNetUsers] WHERE [Email] = N'fatma@driver.com';

INSERT INTO [Drivers] ([DriverPhoto], [DriverIdCard], [DriverLicenseFront], [DriverLicenseBack], 
    [IdCardFront], [IdCardBack], [DriverFullname], [NationalId], [Age], [LicenseNumber], 
    [Email], [Password], [LicenseExpiryDate], [UserId], [Status], [TotalReview], [NoReviews], [Wallet])
VALUES 
(N'https://example.com/driver1.jpg', N'https://example.com/id1.jpg', N'https://example.com/license1f.jpg', 
    N'https://example.com/license1b.jpg', N'https://example.com/idcard1f.jpg', N'https://example.com/idcard1b.jpg',
    N'Khaled Ibrahim', N'12345678901234', 35, N'DL123456', N'khaled@driver.com', N'Driver@123',
    DATEADD(YEAR, 2, GETDATE()), @Driver1UserId, N'approved', 45, 10, 5000.0),
(N'https://example.com/driver2.jpg', N'https://example.com/id2.jpg', N'https://example.com/license2f.jpg', 
    N'https://example.com/license2b.jpg', N'https://example.com/idcard2f.jpg', N'https://example.com/idcard2b.jpg',
    N'Fatma Ahmed', N'98765432109876', 28, N'DL789012', N'fatma@driver.com', N'Driver@123',
    DATEADD(YEAR, 3, GETDATE()), @Driver2UserId, N'approved', 38, 8, 3500.0);

PRINT 'Drivers inserted successfully';
GO

-- ============================================
-- 3. Insert Test Car Data
-- ============================================
DECLARE @Driver1Id INT;
DECLARE @Driver2Id INT;
SELECT @Driver1Id = [Id] FROM [Drivers] WHERE [Email] = N'khaled@driver.com';
SELECT @Driver2Id = [Id] FROM [Drivers] WHERE [Email] = N'fatma@driver.com';

INSERT INTO [CarDatas] ([CarPhoto], [LicenseFront], [LicenseBack], [CarBrand], [CarModel], [CarColor], [PlateNumber], [DriverId])
VALUES 
(N'https://example.com/car1.jpg', N'https://example.com/carlicense1f.jpg', N'https://example.com/carlicense1b.jpg',
    N'Toyota', N'Camry', N'White', N'ABC-1234', @Driver1Id),
(N'https://example.com/car2.jpg', N'https://example.com/carlicense2f.jpg', N'https://example.com/carlicense2b.jpg',
    N'Honda', N'Accord', N'Black', N'XYZ-5678', @Driver2Id);

PRINT 'Car data inserted successfully';
GO

-- ============================================
-- 4. Insert Test Orders
-- ============================================
DECLARE @User1Id NVARCHAR(450);
DECLARE @User2Id NVARCHAR(450);
DECLARE @User3Id NVARCHAR(450);
DECLARE @Driver1Id INT;
SELECT @User1Id = [Id] FROM [AspNetUsers] WHERE [Email] = N'ahmed@test.com';
SELECT @User2Id = [Id] FROM [AspNetUsers] WHERE [Email] = N'sara@test.com';
SELECT @User3Id = [Id] FROM [AspNetUsers] WHERE [Email] = N'mohamed@test.com';
SELECT @Driver1Id = [Id] FROM [Drivers] WHERE [Email] = N'khaled@driver.com';

INSERT INTO [Orders] ([UserId], [Date], [From], [To], [FromLatLng_Lat], [FromLatLng_Lng], 
    [ToLatLng_Lat], [ToLatLng_Lng], [ExpectedPrice], [Type], [Distance], [Notes], 
    [NoPassengers], [UserImage], [UserName], [UserPhone], [Status], [Driverid], [Review], 
    [PaymentWay], [CarType], [PinkMode])
VALUES 
(@User1Id, GETDATE(), N'Tahrir Square, Cairo', N'Nasr City, Cairo', 30.0444, 31.2357, 30.0626, 31.3199,
    150.0, N'ride', 12.5, N'Please arrive on time', 2, N'', N'Ahmed Mohamed', N'01012345678',
    N'pending', NULL, 0, N'cash', N'sedan', 0),
(@User2Id, DATEADD(HOUR, -2, GETDATE()), N'Maadi, Cairo', N'Zamalek, Cairo', 29.9602, 31.2569, 30.0626, 31.2197,
    80.0, N'ride', 8.3, N'Need pink mode', 1, N'', N'Sara Ali', N'01087654321',
    N'accepted', @Driver1Id, 0, N'card', N'sedan', 1),
(@User3Id, DATEADD(DAY, -1, GETDATE()), N'Heliopolis, Cairo', N'Downtown, Cairo', 30.0869, 31.3275, 30.0444, 31.2357,
    120.0, N'delivery', 15.0, N'Fragile package', 0, N'', N'Mohamed Hassan', N'01123456789',
    N'completed', @Driver1Id, 4.5, N'cash', N'suv', 0);

PRINT 'Orders inserted successfully';
GO

-- ============================================
-- 5. Insert Test Trips History
-- ============================================
DECLARE @Driver1Id INT;
DECLARE @Driver2Id INT;
SELECT @Driver1Id = [Id] FROM [Drivers] WHERE [Email] = N'khaled@driver.com';
SELECT @Driver2Id = [Id] FROM [Drivers] WHERE [Email] = N'fatma@driver.com';

INSERT INTO [TripsHistories] ([Review], [PaymentWay], [From], [To], [Date], [TotalTip], [DriverId])
VALUES 
(5, N'cash', N'Tahrir Square', N'Nasr City', DATEADD(DAY, -5, GETDATE()), 20.0, @Driver1Id),
(4, N'card', N'Maadi', N'Zamalek', DATEADD(DAY, -3, GETDATE()), 15.0, @Driver1Id),
(5, N'cash', N'Heliopolis', N'Downtown', DATEADD(DAY, -2, GETDATE()), 25.0, @Driver2Id),
(4, N'card', N'New Cairo', N'6th October', DATEADD(DAY, -1, GETDATE()), 10.0, @Driver2Id);

PRINT 'Trips history inserted successfully';
GO

-- ============================================
-- 6. Insert Test User History
-- ============================================
DECLARE @User1Id NVARCHAR(450);
DECLARE @User2Id NVARCHAR(450);
SELECT @User1Id = [Id] FROM [AspNetUsers] WHERE [Email] = N'ahmed@test.com';
SELECT @User2Id = [Id] FROM [AspNetUsers] WHERE [Email] = N'sara@test.com';

INSERT INTO [UserHistories] ([UserId], [From], [To], [Price], [Date], [PaymentMethod], [RideType])
VALUES 
(@User1Id, N'Tahrir Square', N'Nasr City', 150.00, DATEADD(DAY, -5, GETDATE()), N'cash', N'ride'),
(@User1Id, N'Maadi', N'Zamalek', 80.00, DATEADD(DAY, -3, GETDATE()), N'card', N'ride'),
(@User2Id, N'Heliopolis', N'Downtown', 120.00, DATEADD(DAY, -2, GETDATE()), N'cash', N'delivery'),
(@User2Id, N'New Cairo', N'6th October', 200.00, DATEADD(DAY, -1, GETDATE()), N'card', N'ride');

PRINT 'User history inserted successfully';
GO

-- ============================================
-- 7. Insert Test Charges
-- ============================================
DECLARE @Driver1Id INT;
DECLARE @Driver2Id INT;
SELECT @Driver1Id = [Id] FROM [Drivers] WHERE [Email] = N'khaled@driver.com';
SELECT @Driver2Id = [Id] FROM [Drivers] WHERE [Email] = N'fatma@driver.com';

INSERT INTO [Charges] ([DriverId], [Name], [Image])
VALUES 
(@Driver1Id, N'Vodafone Cash Receipt', N'https://example.com/receipt1.jpg'),
(@Driver2Id, N'Bank Transfer Receipt', N'https://example.com/receipt2.jpg'),
(@Driver1Id, N'Fawry Receipt', N'https://example.com/receipt3.jpg');

PRINT 'Charges inserted successfully';
GO

PRINT '========================================';
PRINT 'All test data inserted successfully!';
PRINT '========================================';
PRINT '';
PRINT 'Test Users:';
PRINT '  - ahmed@test.com / Test@123';
PRINT '  - sara@test.com / Test@123';
PRINT '  - mohamed@test.com / Test@123';
PRINT '';
PRINT 'Test Drivers:';
PRINT '  - khaled@driver.com / Driver@123';
PRINT '  - fatma@driver.com / Driver@123';
PRINT '';
PRINT 'Note: You may need to update PasswordHash using the API Register endpoint';
PRINT '      or use the actual password hash from Identity framework';
GO

