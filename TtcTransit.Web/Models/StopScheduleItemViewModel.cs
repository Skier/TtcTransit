namespace TtcTransit.Web.Models;

public sealed class StopScheduleItemViewModel
{
    public string StopId { get; init; } = "";
    public string TripId { get; init; } = "";
    public string RouteId { get; init; } = "";
    public string RouteShortName { get; init; } = "";
    public string RouteLongName { get; init; } = "";
    public string? HeadSign { get; init; }
    public int? DirectionId { get; init; }
    public string ArrivalTime { get; init; } = "";
    public string DepartureTime { get; init; } = "";
}