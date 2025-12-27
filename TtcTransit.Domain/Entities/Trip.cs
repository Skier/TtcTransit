namespace TtcTransit.Domain.Entities;

public sealed class Trip
{
    public string Id { get; }
    public string RouteId { get; }
    public string HeadSign { get; }
    public int DirectionId { get; }

    public Trip(string id, string routeId, string headSign, int directionId)
    {
        Id = id;
        RouteId = routeId;
        HeadSign = headSign;
        DirectionId = directionId;
    }
}