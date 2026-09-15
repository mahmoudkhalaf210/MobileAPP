using Microsoft.EntityFrameworkCore;
using Snap.Application.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Snap.Infrastructure.Persistence.Seeders
{
    public class CancelReasonSeed
    {
        public static async Task SeedAsync(SnapDbContext context)
        {
            if (await context.CancelReasons.AnyAsync())
                return;

            var reasons = new List<CancelReason>
            {
                new() { TextEn = "Driver is taking too long", TextAr = "السائق يستغرق وقتاً طويلاً" },
                new() { TextEn = "Changed my mind",           TextAr = "غيرت رأيي" },
                new() { TextEn = "Found another ride",        TextAr = "وجدت رحلة أخرى" },
                new() { TextEn = "Price is too high",         TextAr = "السعر مرتفع جداً" },
                new() { TextEn = "Wrong pickup location",     TextAr = "موقع الاستلام غير صحيح" },
                new() { TextEn = "Driver asked me to cancel", TextAr = "طلب مني السائق الإلغاء" },
                new() { TextEn = "Booked by mistake",         TextAr = "تم الحجز عن طريق الخطأ" },
                new() { TextEn = "Other",                     TextAr = "أخرى" },
            };

            context.CancelReasons.AddRange(reasons);
            await context.SaveChangesAsync();
        }
    }
}
