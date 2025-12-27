namespace TtcTransit.Web.Models;

public sealed class RouteViewModel
{
    public string Id { get; init; } = "";
    public string ShortName { get; init; } = "";
    public string LongName { get; init; } = "";
    public int? RouteType { get; init; }
    public string? Color { get; init; }
    public string? TextColor { get; init; }

    public string DisplayName => string.IsNullOrWhiteSpace(ShortName)
        ? LongName
        : $"{ShortName} — {LongName}";
}