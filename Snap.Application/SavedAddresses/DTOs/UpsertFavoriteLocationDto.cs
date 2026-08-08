namespace Snap.Application.SavedAddresses.DTOs
{
    public class UpsertFavoriteLocationDto
    {
        public string AddressLine { get; set; } = null!;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
