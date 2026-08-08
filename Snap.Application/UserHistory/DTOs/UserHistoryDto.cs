using System;

namespace Snap.Application.UserHistory.DTOs
{
    public class UserHistoryDto
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public decimal Price { get; set; }
        public DateTime Date { get; set; }
        public string PaymentMethod { get; set; }
        public string RideType { get; set; }
    }
}
