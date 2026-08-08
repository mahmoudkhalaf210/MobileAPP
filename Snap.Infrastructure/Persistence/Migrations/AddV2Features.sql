-- Manual apply script for migration 20260806124922_AddV2Features.
-- Purely additive: one nullable column on the existing Orders table, plus three
-- new tables. Nothing existing is altered, dropped, or renamed.
-- Generated to match the previous two migrations' "manually applied" workflow,
-- since Program.cs has auto-migrate disabled and migration history is out of sync
-- with the live schema.

BEGIN TRAN;

ALTER TABLE [Orders] ADD [CarTypeEnum] nvarchar(32) NULL;

CREATE TABLE [ExplorePlaces] (
    [Id] int NOT NULL IDENTITY(1,1),
    [Name] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NULL,
    [Latitude] float NOT NULL,
    [Longitude] float NOT NULL,
    [Photo] nvarchar(max) NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NULL,
    CONSTRAINT [PK_ExplorePlaces] PRIMARY KEY ([Id])
);

CREATE TABLE [UserPoints] (
    [Id] int NOT NULL IDENTITY(1,1),
    [UserId] nvarchar(450) NOT NULL,
    [Balance] int NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_UserPoints] PRIMARY KEY ([Id])
);

CREATE TABLE [UserPointsTransactions] (
    [Id] int NOT NULL IDENTITY(1,1),
    [UserId] nvarchar(max) NOT NULL,
    [OrderId] int NOT NULL,
    [PointsAwarded] int NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_UserPointsTransactions] PRIMARY KEY ([Id])
);

CREATE UNIQUE INDEX [IX_UserPoints_UserId] ON [UserPoints] ([UserId]);

CREATE UNIQUE INDEX [IX_UserPointsTransactions_OrderId] ON [UserPointsTransactions] ([OrderId]);

-- Optional: only run this if your __EFMigrationsHistory table already has rows for
-- the previous 2 migrations (i.e. it's actually being kept in sync). Since
-- Program.cs's MigrateAsync() is disabled, this is not required for the app to work.
-- INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
-- VALUES (N'20260806124922_AddV2Features', N'8.0.12');

COMMIT;
