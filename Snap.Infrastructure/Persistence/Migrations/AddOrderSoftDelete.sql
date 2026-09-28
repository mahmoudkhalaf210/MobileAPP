-- Manual apply script for migration 20260926122334_AddOrderSoftDelete.
-- Purely additive: two new columns on Orders. Cancelled orders are now flagged
-- IsDeleted = 1 (hidden from the apps by the EF global query filter) instead of
-- being removed, so they remain queryable here for the admin.

BEGIN TRAN;

ALTER TABLE [Orders] ADD [IsDeleted] bit NOT NULL CONSTRAINT [DF_Orders_IsDeleted] DEFAULT CAST(0 AS bit);

ALTER TABLE [Orders] ADD [DeletedAtUtc] datetime2 NULL;

-- Optional: only run this if your __EFMigrationsHistory table is actually being
-- kept in sync. Since Program.cs's MigrateAsync() is disabled, this is not
-- required for the app to work.
-- INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
-- VALUES (N'20260926122334_AddOrderSoftDelete', N'8.0.12');

COMMIT;

-- Admin: all cancelled/soft-deleted orders with their cancel reason
-- SELECT o.*, r.* FROM Orders o LEFT JOIN CancelReasons r ON r.Id = o.CancelReasonId WHERE o.IsDeleted = 1 ORDER BY o.DeletedAtUtc DESC;
