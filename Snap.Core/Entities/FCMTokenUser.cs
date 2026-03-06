using System.ComponentModel.DataAnnotations;

namespace Snap.Core.Entities
{
    public class FCMTokenUser
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string Token { get; set; }
    }
}