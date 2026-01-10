namespace TtcTransit.Web.Models;

public sealed class RouteStopsViewModel
{
    public string RouteId { get; init; } = "";
    public string RouteShortName { get; init; } = "";

    public List<RouteStopViewModel> Stops { get; init; } = new();
}

public sealed class RouteStopViewModel
{
    public int Sequence { get; init; }

    public string StopId { get; init; } = "";
    public string StopName { get; init; } = "";

    public double Latitude { get; init; }
    public double Longitude { get; init; }

    public int? DirectionId { get; init; }
}