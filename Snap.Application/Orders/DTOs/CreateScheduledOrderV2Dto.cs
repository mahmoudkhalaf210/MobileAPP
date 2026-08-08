using System;
using System.ComponentModel.DataAnnotations;

namespace Snap.Application.Orders.DTOs
{
    public class CreateScheduledOrderV2Dto : CreateNormalOrderV2Dto
    {
        /// <summary>Must be far enough in the future to clear OrderSettings.ScheduledDispatchLeadTimeMinutes.</summary>
        [Required]
        public DateTime Date { get; set; }
    }
}
