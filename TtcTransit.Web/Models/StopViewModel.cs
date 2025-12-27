namespace TtcTransit.Web.Models;

public sealed class StopViewModel
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }

    public int? DirectionId { get; init; }
    public int Sequence { get; init; }
}