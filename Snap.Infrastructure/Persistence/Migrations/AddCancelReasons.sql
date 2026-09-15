-- Manual apply script for migration 20260808173459_AddCancelReasons.
-- Purely additive: one nullable column on the existing Orders table, plus one
-- new table, plus its default seed rows. Nothing existing is altered, dropped,
-- or renamed. Generated to match the previous migrations' "manually applied"
-- workflow, since Program.cs has auto-migrate disabled and migration history
-- is out of sync with the live schema.

BEGIN TRAN;

ALTER TABLE [Orders] ADD [CancelReasonId] int NULL;

CREATE TABLE [CancelReasons] (
    [Id] int NOT NULL IDENTITY(1,1),
    [TextEn] nvarchar(max) NOT NULL,
    [TextAr] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_CancelReasons] PRIMARY KEY ([Id])
);

-- Default reasons (English + Arabic). Safe to re-run: only inserts if the
-- table is still empty, so editing/adding reasons later via
-- POST /api/v2/cancel-reasons won't be touched by re-running this script.
IF NOT EXISTS (SELECT 1 FROM [CancelReasons])
BEGIN
    INSERT INTO [CancelReasons] ([TextEn], [TextAr]) VALUES
        (N'Driver is taking too long',   N'السائق يستغرق وقتاً طويلاً'),
        (N'Changed my mind',             N'غيرت رأيي'),
        (N'Found another ride',          N'وجدت رحلة أخرى'),
        (N'Price is too high',           N'السعر مرتفع جداً'),
        (N'Wrong pickup location',       N'موقع الاستلام غير صحيح'),
        (N'Driver asked me to cancel',   N'طلب مني السائق الإلغاء'),
        (N'Booked by mistake',           N'تم الحجز عن طريق الخطأ'),
        (N'Other',                       N'أخرى');
END

-- Optional: only run this if your __EFMigrationsHistory table already has rows for
-- the previous migrations (i.e. it's actually being kept in sync). Since
-- Program.cs's MigrateAsync() is disabled, this is not required for the app to work.
-- INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
-- VALUES (N'20260808173459_AddCancelReasons', N'8.0.12');

COMMIT;
