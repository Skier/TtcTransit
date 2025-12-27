using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProtoBuf;
using TransitRealtime;

namespace TtcTransit.Api.Realtime;

public sealed class GtfsRealtimeClient
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<GtfsRealtimeClient> _logger;

    public GtfsRealtimeClient(
        HttpClient http,
        IConfiguration config,
        ILogger<GtfsRealtimeClient> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    private async Task<FeedMessage?> FetchFeedAsync(CancellationToken ct)
    {
        var url = _config["Realtime:TripUpdatesUrl"];
        if (string.IsNullOrWhiteSpace(url))
        {
            _logger.LogWarning("Realtime:TripUpdatesUrl is not configured");
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            var apiKeyHeader = _config["Realtime:ApiKeyHeader"];
            var apiKeyValue = _config["Realtime:ApiKeyValue"];

            if (!string.IsNullOrWhiteSpace(apiKeyHeader) &&
                !string.IsNullOrWhiteSpace(apiKeyValue))
            {
                request.Headers.TryAddWithoutValidation(apiKeyHeader, apiKeyValue);
            }

            using var response = await _http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                ct);

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(ct);

            var feed = Serializer.Deserialize<FeedMessage>(stream);
            return feed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch GTFS-RT feed");
            return null;
        }
    }

    // Уже существующий метод для конкретной остановки (можно оставить как есть, если используешь)
    public async Task<IReadOnlyList<RealtimeStopArrival>> GetStopArrivalsAsync(
        string stopId,
        int maxResults,
        CancellationToken ct = default)
    {
        var feed = await FetchFeedAsync(ct);
        if (feed == null)
            return Array.Empty<RealtimeStopArrival>();

        var result = new List<RealtimeStopArrival>();
        var now = DateTimeOffset.UtcNow;

        foreach (var entity in feed.Entities)
        {
            var tu = entity.TripUpdate;
            if (tu == null)
                continue;

            var trip = tu.Trip;
            var routeId = trip?.RouteId ?? string.Empty;
            var tripId = trip?.TripId ?? string.Empty;
            int? directionId = trip != null ? (int?)trip.DirectionId : null;

            foreach (var stu in tu.StopTimeUpdates)
            {
                if (!string.Equals(stu.StopId, stopId, StringComparison.OrdinalIgnoreCase))
                    continue;

                var arrival = stu.Arrival;
                var departure = stu.Departure;

                long? ts = null;
                if (arrival != null && arrival.Time != 0)
                    ts = arrival.Time;
                else if (departure != null && departure.Time != 0)
                    ts = departure.Time;

                if (ts == null)
                    continue;

                var arrTime = DateTimeOffset.FromUnixTimeSeconds(ts.Value);

                if (arrTime < now.AddMinutes(-30))
                    continue;

                int? delaySeconds = null;
                if (arrival != null && arrival.Delay != 0)
                    delaySeconds = arrival.Delay;
                else if (departure != null && departure.Delay != 0)
                    delaySeconds = departure.Delay;

                result.Add(new RealtimeStopArrival
                {
                    StopId = stu.StopId ?? string.Empty,
                    TripId = tripId,
                    RouteId = routeId,
                    DirectionId = directionId,
                    HeadSign = null,
                    ArrivalTime = arrTime.ToLocalTime(),
                    DelaySeconds = delaySeconds
                });
            }
        }

        return result
            .OrderBy(r => r.ArrivalTime)
            .Take(Math.Max(1, maxResults))
            .ToList();
    }

    // НОВЫЙ метод: все прибытия по всем остановкам (для диагностической страницы)
    public async Task<IReadOnlyList<RealtimeStopArrival>> GetAllArrivalsAsync(
        CancellationToken ct = default)
    {
        var feed = await FetchFeedAsync(ct);
        if (feed == null)
            return Array.Empty<RealtimeStopArrival>();

        var result = new List<RealtimeStopArrival>();
        var now = DateTimeOffset.UtcNow;

        foreach (var entity in feed.Entities)
        {
            var tu = entity.TripUpdate;
            if (tu == null)
                continue;

            var trip = tu.Trip;
            var routeId = trip?.RouteId ?? string.Empty;
            var tripId = trip?.TripId ?? string.Empty;
            int? directionId = trip != null ? (int?)trip.DirectionId : null;

            foreach (var stu in tu.StopTimeUpdates)
            {
                var stopId = stu.StopId;
                if (string.IsNullOrWhiteSpace(stopId))
                    continue;

                // Берём В ПЕРВУЮ ОЧЕРЕДЬ departure.time
                var departure = stu.Departure;
                var arrival = stu.Arrival;

                long? ts = null;
                if (departure != null && departure.Time != 0)
                    ts = departure.Time;
                else if (arrival != null && arrival.Time != 0)
                    ts = arrival.Time;

                if (ts == null)
                    continue;

                var depTime = DateTimeOffset.FromUnixTimeSeconds(ts.Value);

                // Отсекаем сильно устаревшие
                if (depTime < now.AddMinutes(-20))
                    continue;

                // delay из фида игнорируем, будем считать сами
                uint? stopSeq = stu.StopSequence == 0 ? null : stu.StopSequence;

                result.Add(new RealtimeStopArrival
                {
                    StopId = stopId,
                    TripId = tripId,
                    RouteId = routeId,
                    DirectionId = directionId,
                    HeadSign = null,
                    StopSequence = stopSeq,
                    ArrivalTime = depTime.ToLocalTime(),
                    DelaySeconds = null
                });
            }
        }

        return result;
    }
}
