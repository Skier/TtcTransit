namespace TtcTransit.Api.Realtime;

public sealed class RealtimeStopArrival
{
    public string StopId { get; init; } = "";
    public string TripId { get; init; } = "";
    public string RouteId { get; init; } = "";
    public string? HeadSign { get; init; }
    public int? DirectionId { get; init; }

    // stop_sequence из GTFS-RT StopTimeUpdate, нужен для сопоставления со stop_times
    public uint? StopSequence { get; init; }

    // Это фактическое время отправления (берём departure.time)
    public DateTimeOffset ArrivalTime { get; init; }

    // Больше не используем realtime delay, будем считать сами
    public int? DelaySeconds { get; init; }
}