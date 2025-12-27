namespace TtcTransit.Web.Models;

public sealed class RealtimeDelayViewModel
{
    public string RouteId { get; init; } = "";
    public string RouteShortName { get; init; } = "";
    public string StopId { get; init; } = "";
    public string StopName { get; init; } = "";
    public DateTimeOffset ScheduledTime { get; init; }
    public DateTimeOffset ActualTime { get; init; }
    public int DelaySeconds { get; init; }
}