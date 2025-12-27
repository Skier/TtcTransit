namespace TtcTransit.Domain.Entities;

public sealed class StopScheduleEntry
{
    public string StopId { get; }
    public string TripId { get; }
    public string RouteId { get; }
    public string RouteShortName { get; }
    public string RouteLongName { get; }
    public string? HeadSign { get; }
    public int? DirectionId { get; }
    public string ArrivalTime { get; }
    public string DepartureTime { get; }

    public StopScheduleEntry(
        string stopId,
        string tripId,
        string routeId,
        string routeShortName,
        string routeLongName,
        string? headSign,
        int? directionId,
        string arrivalTime,
        string departureTime)
    {
        StopId = stopId;
        TripId = tripId;
        RouteId = routeId;
        RouteShortName = routeShortName;
        RouteLongName = routeLongName;
        HeadSign = headSign;
        DirectionId = directionId;
        ArrivalTime = arrivalTime;
        DepartureTime = departureTime;
    }
}