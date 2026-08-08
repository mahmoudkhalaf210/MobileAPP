using System.Text.Json.Serialization;

namespace Snap.Application.Domain.Enums
{
    // Serializes as its string name ("Car", "Scooter", ...) in v2 JSON payloads rather
    // than a raw int. Scoped to this enum only via the attribute — does not touch the
    // app's global JSON options, so no existing (v1) enum serialization is affected.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CarType
    {
        Car = 0,
        Scooter = 1,
        SuperMalaky = 2,
        Taxi = 3
    }
}
