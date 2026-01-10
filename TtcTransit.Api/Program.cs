using Microsoft.Data.Sqlite;
using System.Globalization;
using TtcTransit.Api.Realtime;
using TtcTransit.Data;
using TtcTransit.Data.Repositories;
using TtcTransit.Domain.Repositories;
using static System.Net.Mime.MediaTypeNames;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddTransitData();
builder.Services.AddHttpClient<GtfsRealtimeClient>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => "TTC Transit API is running.");

// --- Список маршрутов с сортировкой по числовому shortName ---
app.MapGet("/api/routes", async (IRouteRepository repo, CancellationToken ct) =>
{
    var list = new List<TtcTransit.Domain.Entities.Route>();

    await foreach (var route in repo.GetAllAsync(ct))
        list.Add(route);

    var sorted = list
        .OrderBy(r =>
        {
            if (int.TryParse(r.ShortName, out int num))
                return (0, num, "");
            else
                return (1, 0, r.ShortName);
        })
        .ThenBy(r => r.LongName)
        .Select(r => new
        {
            id = r.Id,
            shortName = r.ShortName,
            longName = r.LongName,
            routeType = r.RouteType,
            color = r.Color,
            textColor = r.TextColor
        });

    return Results.Ok(sorted);
});

// --- Список остановок для маршрута (с направлением и порядком) ---
app.MapGet("/api/routes/{routeId}/stops",
    async (string routeId, IStopRepository repo, CancellationToken ct) =>
    {
        var stops = new List<object>();

        await foreach (var s in repo.GetByRouteAsync(routeId, ct))
        {
            stops.Add(new
            {
                id = s.StopId,
                name = s.StopName,
                latitude = s.Latitude,
                longitude = s.Longitude,
                directionId = s.DirectionId,
                sequence = s.Sequence
            });
        }

        return Results.Ok(stops);
    });

// --- Список трипов маршрута (как было раньше) ---
app.MapGet("/api/routes/{routeId}/trips",
    async (string routeId, ITripRepository repo, CancellationToken ct) =>
    {
        var result = new List<object>();

        await foreach (var trip in repo.GetTripsByRouteAsync(routeId, ct))
        {
            result.Add(new
            {
                trip.Id,
                trip.RouteId,
                trip.HeadSign,
                trip.DirectionId
            });
        }

        return Results.Ok(result);
    });

app.MapGet("/api/stops/{stopId}/schedule",
    async (string stopId, string? date, IStopScheduleRepository repo, CancellationToken ct) =>
    {
        DateOnly targetDate;

        if (!string.IsNullOrWhiteSpace(date) &&
            DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            targetDate = parsed;
        }
        else
        {
            targetDate = DateOnly.FromDateTime(DateTime.Today);
        }

        var list = new List<object>();

        await foreach (var e in repo.GetScheduleAsync(stopId, targetDate, ct))
        {
            list.Add(new
            {
                stopId = e.StopId,
                tripId = e.TripId,
                routeId = e.RouteId,
                routeShortName = e.RouteShortName,
                routeLongName = e.RouteLongName,
                headSign = e.HeadSign,
                directionId = e.DirectionId,
                arrivalTime = e.ArrivalTime,
                departureTime = e.DepartureTime
            });
        }

        return Results.Ok(list);
    });

app.MapGet("/api/stops/{stopId}/next",
    async (string stopId, int? max, GtfsRealtimeClient rtClient, CancellationToken ct) =>
    {
        var maxResults = max is > 0 and < 50 ? max.Value : 5;

        var arrivals = await rtClient.GetStopArrivalsAsync(stopId, maxResults, ct);

        var response = arrivals.Select(a => new
        {
            stopId = a.StopId,
            tripId = a.TripId,
            routeId = a.RouteId,
            headSign = a.HeadSign,
            directionId = a.DirectionId,
            arrivalTime = a.ArrivalTime,
            delaySeconds = a.DelaySeconds
        });

        return Results.Ok(response);
    });

app.MapGet("/api/realtime/delays",
    async (int? max,
           GtfsRealtimeClient rtClient,
           IRouteRepository routeRepo,
           IStopInfoRepository stopInfoRepo,
           IStopTimeRepository stopTimeRepo,
           CancellationToken ct) =>
    {
        var maxResults = max is > 0 and <= 500 ? max.Value : 100;

        // 1. Все realtime-прибытия (с фактическим departure и stop_sequence)
        var arrivals = await rtClient.GetAllArrivalsAsync(ct);

        if (arrivals.Count == 0)
            return Results.Ok(Array.Empty<object>());

        // 2. Подтягиваем названия маршрутов
        var routeNames = new Dictionary<string, string>();
        await foreach (var r in routeRepo.GetAllAsync(ct))
        {
            var key = r.Id;
            var value = string.IsNullOrWhiteSpace(r.ShortName) ? r.Id : r.ShortName;
            routeNames[key] = value;
        }

        // 3. Подтягиваем названия остановок
        var stopNames = await stopInfoRepo.GetStopNamesAsync(ct);

        var items = new List<object>();

        // 4. Для каждого realtime-события считаем delay на основе статики
        foreach (var a in arrivals)
        {
            // Если нет tripId — пропускаем, не с чем сопоставлять
            if (string.IsNullOrWhiteSpace(a.TripId))
                continue;

            var scheduledTs = await stopTimeRepo.GetScheduledDepartureAsync(
                a.TripId,
                a.StopSequence,
                a.StopId,
                ct);

            if (scheduledTs is null)
                continue;

            var actual = a.ArrivalTime;

            // Преобразуем TimeSpan (время суток) в DateTimeOffset того же дня, что и фактическое время
            var scheduledLocal = new DateTimeOffset(
                actual.Date.Add(scheduledTs.Value),
                actual.Offset);

            var delaySeconds = (int)(actual - scheduledLocal).TotalSeconds;

            // Здесь можно отфильтровать слишком странные значения (например, > 4 часов)
            if (Math.Abs(delaySeconds) > 4 * 3600)
                continue;

            routeNames.TryGetValue(a.RouteId, out var routeShort);
            stopNames.TryGetValue(a.StopId, out var stopName);

            items.Add(new
            {
                routeId = a.RouteId,
                routeShortName = routeShort ?? a.RouteId,
                stopId = a.StopId,
                stopName = stopName ?? a.StopId,
                scheduledTime = scheduledLocal,
                actualTime = actual,
                delaySeconds = delaySeconds
            });
        }

        // 5. Сортируем по задержке по убыванию и берём top N
        var ordered = items
            .OrderByDescending(x => ((dynamic)x).delaySeconds)
            .Take(maxResults)
            .ToList();

        return Results.Ok(ordered);
    });

// GET /api/esp/next.txt?stops=STOP1,STOP2&max=10
// Plain-text endpoint for ESP32: nearest arrivals for given stops
// GET /api/esp/next.txt?stops=STOP1,STOP2&max=10
app.MapGet("/api/esp/next.txt",
    async (string stops,
           int? max,
           GtfsRealtimeClient rtClient,
           IRouteRepository routeRepo,
           CancellationToken ct) =>
    {
        // 1. Парсим список stop_id из query: "STOP1,STOP2,STOP3"
        var stopIds = stops
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (stopIds.Count == 0)
            return Results.Text(string.Empty, "text/plain");

        var stopSet = new HashSet<string>(stopIds, StringComparer.OrdinalIgnoreCase);

        int maxResults = max is > 0 and <= 50 ? max.Value : 10;

        // 2. Берём все realtime-прибытия
        var allArrivals = await rtClient.GetAllArrivalsAsync(ct);
        if (allArrivals.Count == 0)
            return Results.Text(string.Empty, "text/plain");

        var now = DateTimeOffset.Now;

        // 3. Фильтруем по нужным остановкам и считаем минуты до прибытия
        var forStops = allArrivals
            .Where(a => stopSet.Contains(a.StopId))
            .Select(a => new
            {
                Arrival = a,
                Minutes = (int)Math.Round((a.ArrivalTime - now).TotalMinutes)
            })
            .Where(x => x.Minutes > 0) // только будущие рейсы
            .OrderBy(x => x.Minutes)    // сортировка по ближайшему времени
            .ToList();

        if (forStops.Count == 0)
            return Results.Text(string.Empty, "text/plain");

        // 4. Словарь route_id -> short_name (или route_id, если short_name пустой)
        var routeNames = new Dictionary<string, string>();
        await foreach (var r in routeRepo.GetAllAsync(ct))
        {
            var key = r.Id;
            var value = string.IsNullOrWhiteSpace(r.ShortName) ? r.Id : r.ShortName;
            routeNames[key] = value;
        }

        // 5. Собираем нужные trip_id
        var tripIds = forStops
            .Select(x => x.Arrival.TripId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // 6. Грузим trip_headsign для нужных trip_id из SQLite
        var tripHeadsigns = await LoadTripHeadsignsAsync(tripIds, ct);

        var lines = new List<string>();

        foreach (var x in forStops)
        {
            var a = x.Arrival;
            var minutes = x.Minutes;

            // route short name (из routes.txt)
            routeNames.TryGetValue(a.RouteId, out var routeShortRaw);
            var routeShort = string.IsNullOrWhiteSpace(routeShortRaw)
                ? a.RouteId
                : routeShortRaw;

            string? labelRoute = routeShort;
            string? labelDir = null;

            if (!string.IsNullOrWhiteSpace(a.TripId)
                && tripHeadsigns.TryGetValue(a.TripId, out var headsign)
                && !string.IsNullOrWhiteSpace(headsign))
            {
                var parsed = ParseTripHeadsign(headsign);

                if (!string.IsNullOrWhiteSpace(parsed.RouteVariant))
                    labelRoute = parsed.RouteVariant;

                if (!string.IsNullOrWhiteSpace(parsed.Direction))
                    labelDir = parsed.Direction;
            }

            // Fallback по DirectionId, если направление не удалось взять из headsign
            if (string.IsNullOrWhiteSpace(labelDir))
            {
                labelDir = DirectionFromId(a.DirectionId);
            }

            // Формируем текстовую метку маршрута:
            // "13A South" или "512 West" или просто "512"
            string label = labelRoute ?? routeShort;
            if (!string.IsNullOrWhiteSpace(labelDir))
                label = $"{label} {labelDir}";

            // Базовая строка: "13A South - 5m"
            var text = $"{label} - {minutes}m";

            // Ограничиваем длину 20 символами
            if (text.Length > 20)
            {
                // Укороченный вариант: без " - "
                text = $"{labelRoute ?? routeShort} {minutes}m";

            }

            lines.Add(text);

            if (lines.Count >= maxResults)
                break;
        }

        var finalLines = GroupArrivalLines(lines);

        var resultText = string.Join("\n", finalLines);
        return Results.Text(resultText, "text/plain");
    });

static List<string> GroupArrivalLines(IEnumerable<string> lines)
{
    // Берём только строки, которые содержат разделитель '-'
    var parsed = lines
        .Select(line => line.Split('-', 2, StringSplitOptions.TrimEntries))
        .Where(parts => parts.Length == 2
                        && !string.IsNullOrWhiteSpace(parts[0])
                        && !string.IsNullOrWhiteSpace(parts[1]))
        .Select(parts => (label: parts[0], minutes: parts[1]))
        .ToList();

    if (parsed.Count == 0)
        return new List<string>();

    // группируем по label
    var grouped = parsed
        .GroupBy(x => x.label)
        .Select(g =>
        {
            // собираем список минут в одну строку: "5m, 7m, 12m"
            var joinedMinutes = string.Join(" ", g.Select(x => x.minutes));
            var text = $"{g.Key} - {joinedMinutes}";
            if (text.Length > 20)
                text = text.Substring(0, 20);
            return text;

        })
        .ToList();

    return grouped;
}

static async Task<Dictionary<string, string>> LoadTripHeadsignsAsync(
    IEnumerable<string> tripIds,
    CancellationToken ct)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    var ids = tripIds
        .Where(id => !string.IsNullOrWhiteSpace(id))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (ids.Count == 0)
        return result;

    // Путь к БД: сначала пробуем GTFS_DB_PATH (Cloud Run),
    // иначе локальный вариант (для разработки)
    var dbPath = Environment.GetEnvironmentVariable("GTFS_DB_PATH");
    if (string.IsNullOrWhiteSpace(dbPath))
    {
        var baseDir = AppContext.BaseDirectory;
        var solutionRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
        var dataDir = Path.Combine(solutionRoot, "Data");
        dbPath = Path.Combine(dataDir, "gtfs.sqlite");
    }

    var connectionString = $"Data Source={dbPath}";

    await using var conn = new SqliteConnection(connectionString);
    await conn.OpenAsync(ct);

    await using var cmd = conn.CreateCommand();

    var paramNames = new List<string>();
    for (int i = 0; i < ids.Count; i++)
    {
        var pName = $"@p{i}";
        paramNames.Add(pName);
        cmd.Parameters.AddWithValue(pName, ids[i]);
    }

    cmd.CommandText = $@"
        SELECT trip_id, trip_headsign
        FROM trips
        WHERE trip_id IN ({string.Join(",", paramNames)})
    ";

    await using var reader = await cmd.ExecuteReaderAsync(ct);
    while (await reader.ReadAsync(ct))
    {
        var tripId = reader.GetString(0);
        var headsign = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
        result[tripId] = headsign;
    }

    return result;
}

// Разбор строки вида
// "South - 13A Avenue Rd towards Queen's Park"
static (string? RouteVariant, string? Direction) ParseTripHeadsign(string? headsign)
{
    if (string.IsNullOrWhiteSpace(headsign))
        return (null, null);

    var parts = headsign.Split(" - ", 2, StringSplitOptions.TrimEntries);
    if (parts.Length != 2)
        return (null, null);

    var directionRaw = parts[0];           // "South"
    var rest = parts[1];                   // "13A Avenue Rd towards ..."

    string? direction = null;
    if (directionRaw.Equals("North", StringComparison.OrdinalIgnoreCase) ||
        directionRaw.Equals("South", StringComparison.OrdinalIgnoreCase) ||
        directionRaw.Equals("East", StringComparison.OrdinalIgnoreCase) ||
        directionRaw.Equals("West", StringComparison.OrdinalIgnoreCase))
    {
        direction = directionRaw.Substring(0,1);
    }

    string? routeVariant = null;
    var restParts = rest.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
    if (restParts.Length >= 1)
    {
        // "13A", "130B", "131", ...
        routeVariant = restParts[0];
    }

    return (routeVariant, direction);
}

// Fallback-направление по DirectionId, если не удалось достать из headsign
static string? DirectionFromId(int? dirId) => dirId switch
{
    0 => "E?",
    1 => "W?",
    2 => "S?",
    3 => "N?",
    _ => null
};


app.Run();
