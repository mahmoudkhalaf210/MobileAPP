using Snap.Application.Common.DTOs;

namespace Snap.Application.TripsHistory.DTOs
{
    public class DriverTripHistoryDetailsDto
    {
        public PublicUserInfoDto User { get; set; }
        public PublicDriverInfoDto Driver { get; set; }
        public TripDetailsDto Trip { get; set; }
    }
}
