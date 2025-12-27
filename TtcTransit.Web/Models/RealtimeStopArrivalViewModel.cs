namespace TtcTransit.Web.Models;

public sealed class RealtimeStopArrivalViewModel
{
    public string StopId { get; init; } = "";
    public string TripId { get; init; } = "";
    public string RouteId { get; init; } = "";
    public string? HeadSign { get; init; }
    public int? DirectionId { get; init; }
    public DateTimeOffset ArrivalTime { get; init; }
    public int? DelaySeconds { get; init; }
}