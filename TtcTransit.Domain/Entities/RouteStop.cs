namespace TtcTransit.Domain.Entities;

public sealed class RouteStop
{
    public string StopId { get; }
    public string StopName { get; }
    public double Latitude { get; }
    public double Longitude { get; }
    public string RouteId { get; }
    public int? DirectionId { get; }
    public int Sequence { get; }

    public RouteStop(
        string stopId,
        string stopName,
        double latitude,
        double longitude,
        string routeId,
        int? directionId,
        int sequence)
    {
        StopId = stopId;
        StopName = stopName;
        Latitude = latitude;
        Longitude = longitude;
        RouteId = routeId;
        DirectionId = directionId;
        Sequence = sequence;
    }
}