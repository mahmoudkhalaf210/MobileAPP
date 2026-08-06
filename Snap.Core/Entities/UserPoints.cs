using System;

namespace Snap.Core.Entities
{
    public class UserPoints
    {
        public int Id { get; set; }
        public string UserId { get; set; } = null!;
        public int Balance { get; set; }
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
