namespace TtcTransit.Domain.Entities;

public sealed class Route
{
    public string Id { get; }
    public string ShortName { get; }
    public string LongName { get; }
    public int? RouteType { get; }
    public string? Color { get; }
    public string? TextColor { get; }

    public Route(
        string id,
        string shortName,
        string longName,
        int? routeType = null,
        string? color = null,
        string? textColor = null)
    {
        Id = id;
        ShortName = shortName;
        LongName = longName;
        RouteType = routeType;
        Color = color;
        TextColor = textColor;
    }

    public override string ToString()
        => $"{ShortName} - {LongName}";
}