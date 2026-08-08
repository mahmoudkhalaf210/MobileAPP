using Snap.Application.Common.DTOs;

namespace Snap.Application.UserHistory.DTOs
{
    public class UserHistoryDetailsDto
    {
        public PublicUserInfoDto User { get; set; }
        public PublicDriverInfoDto? Driver { get; set; }
        public TripDetailsDto Trip { get; set; }
    }
}
