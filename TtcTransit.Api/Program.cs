using System.Globalization;
using TtcTransit.Api.Realtime;
using TtcTransit.Data;
using TtcTransit.Domain.Repositories;

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

// --- ESP32: plain text список ближайших прибытии? ---
// GET /api/esp/next.txt?stops=STOP1,STOP2&max=10
app.MapGet("/api/esp/next.txt",
    async (string stops,
           int? max,
           GtfsRealtimeClient rtClient,
           IRouteRepository routeRepo,
           CancellationToken ct) =>
    {
        // Парсим список остановок из строки "STOP1,STOP2,STOP3"
        var stopIds = stops
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (stopIds.Count == 0)
            return Results.Text("", "text/plain");

        var stopSet = new HashSet<string>(stopIds, StringComparer.OrdinalIgnoreCase);

        int maxResults = max is > 0 and <= 50 ? max.Value : 10;

        // 1. Получаем realtime-прибытия
        var allArrivals = await rtClient.GetAllArrivalsAsync(ct);

        if (allArrivals.Count == 0)
            return Results.Text("", "text/plain");

        // 2. Фильтруем по stop_id
        var now = DateTimeOffset.Now;

        var forStops = allArrivals
            .Where(a => stopSet.Contains(a.StopId))
            .Select(a => new
            {
                Arrival = a,
                Minutes = (int)Math.Round((a.ArrivalTime - now).TotalMinutes)
            })
            .Where(x => x.Minutes >= 0) // убираем уже прошедшие
            .OrderBy(x => x.Minutes)    // сортировка по ближайшему времени
            .ToList();

        if (forStops.Count == 0)
            return Results.Text("", "text/plain");

        // 3. Короткие имена маршрутов
        var routeNames = new Dictionary<string, string>();
        await foreach (var r in routeRepo.GetAllAsync(ct))
        {
            var key = r.Id;
            var value = string.IsNullOrWhiteSpace(r.ShortName) ? r.Id : r.ShortName;
            routeNames[key] = value;
        }

        var lines = new List<string>();

        foreach (var x in forStops)
        {
            var a = x.Arrival;
            var minutes = x.Minutes;
            if (minutes < 0)
                continue;

            // Имя маршрута
            routeNames.TryGetValue(a.RouteId, out var routeShortRaw);
            var routeShort = string.IsNullOrWhiteSpace(routeShortRaw)
                ? a.RouteId
                : routeShortRaw;

            // Буква направления
            string dirLetter = a.DirectionId switch
            {
                0 => "E",
                1 => "W",
                _ => ""
            };

            var label = string.IsNullOrEmpty(dirLetter)
                ? routeShort
                : $"{routeShort}{dirLetter}";

            // Базовая строка
            var text = $"{label} - {minutes} min";

            // Ограничение длины строки
            if (text.Length > 20)
            {
                // упрощенный вариант
                text = $"{label} {minutes}m";

                if (text.Length > 20)
                    text = text.Substring(0, 20);
            }

            lines.Add(text);

            if (lines.Count >= maxResults)
                break;
        }

        // Итоговый plain-text ответ
        var resultText = string.Join("\n", lines);

        return Results.Text(resultText, "text/plain");
    });

app.Run();
