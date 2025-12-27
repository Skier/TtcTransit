using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;
using TtcTransit.Domain.Entities;
using TtcTransit.Domain.Repositories;

namespace TtcTransit.Data.Repositories;

public sealed class StopRepository : IStopRepository
{
    private readonly string _connectionString;

    public StopRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async IAsyncEnumerable<RouteStop> GetByRouteAsync(
        string routeId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        // 1. Находим для данного route_id все trip'ы и считаем количество остановок в каждом.
        const string tripsSql = """
            SELECT
                t.trip_id,
                t.direction_id,
                COUNT(st.stop_id) AS stop_count
            FROM trips t
            INNER JOIN stop_times st ON st.trip_id = t.trip_id
            WHERE t.route_id = $routeId
            GROUP BY t.trip_id, t.direction_id
            """;

        await using var tripsCmd = new SqliteCommand(tripsSql, connection);
        tripsCmd.Parameters.AddWithValue("$routeId", routeId);

        var trips = new List<(string TripId, int? DirectionId, int StopCount)>();

        await using (var reader = await tripsCmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                if (ct.IsCancellationRequested)
                    yield break;

                var tripId = reader.GetString(0);
                int? directionId = reader.IsDBNull(1) ? null : reader.GetInt32(1);
                var stopCount = reader.GetInt32(2);

                trips.Add((tripId, directionId, stopCount));
            }
        }

        if (trips.Count == 0)
            yield break;

        // 2. Для каждого направления выбираем один "эталонный" trip —
        //    тот, у которого максимальное количество остановок.
        var bestTrips = trips
            .GroupBy(t => t.DirectionId)
            .Select(g => g
                .OrderByDescending(x => x.StopCount)
                .First())
            .OrderBy(x => x.DirectionId ?? -1)   // null (без направления) первыми
            .ToList();

        // 3. Для каждого выбранного trip'а вытаскиваем список остановок
        //    в порядке stop_sequence.
        const string stopsSql = """
            SELECT
                s.stop_id,
                s.stop_name,
                s.stop_lat,
                s.stop_lon,
                t.route_id,
                t.direction_id,
                st.stop_sequence
            FROM stop_times st
            INNER JOIN stops s ON s.stop_id = st.stop_id
            INNER JOIN trips t ON t.trip_id = st.trip_id
            WHERE st.trip_id = $tripId
            ORDER BY st.stop_sequence
            """;

        foreach (var (tripId, directionId, _) in bestTrips)
        {
            await using var stopsCmd = new SqliteCommand(stopsSql, connection);
            stopsCmd.Parameters.AddWithValue("$tripId", tripId);

            await using var reader = await stopsCmd.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                if (ct.IsCancellationRequested)
                    yield break;

                var stopId = reader.GetString(0);
                var stopName = reader.GetString(1);

                double lat = reader.IsDBNull(2) ? 0 : reader.GetDouble(2);
                double lon = reader.IsDBNull(3) ? 0 : reader.GetDouble(3);
                var route = reader.GetString(4);
                int? dirId = reader.IsDBNull(5) ? null : reader.GetInt32(5);
                var seq = reader.IsDBNull(6) ? 0 : reader.GetInt32(6);

                yield return new RouteStop(
                    stopId,
                    stopName,
                    lat,
                    lon,
                    route,
                    dirId,
                    seq);
            }
        }
    }
}
