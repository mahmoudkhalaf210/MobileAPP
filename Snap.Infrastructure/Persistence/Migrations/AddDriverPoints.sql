-- Manual apply script for migration 20260919105602_AddDriverPoints.
-- Purely additive: two new tables, mirroring UserPoints/UserPointsTransactions
-- (the rider points system) but keyed by DriverId (int, Drivers.Id) instead of
-- UserId. Nothing existing is altered, dropped, or renamed. Generated to match
-- the previous migrations' "manually applied" workflow, since Program.cs has
-- auto-migrate disabled and migration history is out of sync with the live schema.

BEGIN TRAN;

CREATE TABLE [DriverPoints] (
    [Id] int NOT NULL IDENTITY(1,1),
    [DriverId] int NOT NULL,
    [Balance] int NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_DriverPoints] PRIMARY KEY ([Id])
);

CREATE TABLE [DriverPointsTransactions] (
    [Id] int NOT NULL IDENTITY(1,1),
    [DriverId] int NOT NULL,
    [OrderId] int NOT NULL,
    [PointsAwarded] int NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_DriverPointsTransactions] PRIMARY KEY ([Id])
);

CREATE UNIQUE INDEX [IX_DriverPoints_DriverId] ON [DriverPoints] ([DriverId]);

CREATE UNIQUE INDEX [IX_DriverPointsTransactions_OrderId] ON [DriverPointsTransactions] ([OrderId]);

-- Optional: only run this if your __EFMigrationsHistory table is actually being
-- kept in sync. Since Program.cs's MigrateAsync() is disabled, this is not
-- required for the app to work.
-- INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
-- VALUES (N'20260919105602_AddDriverPoints', N'8.0.12');

COMMIT;
