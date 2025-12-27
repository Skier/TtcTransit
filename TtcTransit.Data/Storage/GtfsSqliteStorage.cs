using Microsoft.Data.Sqlite;
using TtcTransit.Domain.Entities;

namespace TtcTransit.Data.Storage;

public sealed class GtfsSqliteStorage : IGtfsStorage
{
    private readonly string _connectionString;

    public GtfsSqliteStorage(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async IAsyncEnumerable<Trip> StreamTrips(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        const string sql = """
            SELECT trip_id, route_id, trip_headsign, COALESCE(direction_id, 0)
            FROM trips
            ORDER BY route_id, trip_id
            """;

        await using var cmd = new SqliteCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            if (ct.IsCancellationRequested)
                yield break;

            var trip = new Trip(
                id: reader.GetString(0),
                routeId: reader.GetString(1),
                headSign: reader.IsDBNull(2) ? "" : reader.GetString(2),
                directionId: reader.IsDBNull(3) ? 0 : reader.GetInt32(3));

            yield return trip;
        }
    }

    public async ValueTask<Trip?> FindTripByIdAsync(
        string tripId,
        CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        const string sql = """
            SELECT trip_id, route_id, trip_headsign, COALESCE(direction_id, 0)
            FROM trips
            WHERE trip_id = $trip_id
            """;

        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$trip_id", tripId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
            return null;

        return new Trip(
            id: reader.GetString(0),
            routeId: reader.GetString(1),
            headSign: reader.IsDBNull(2) ? "" : reader.GetString(2),
            directionId: reader.IsDBNull(3) ? 0 : reader.GetInt32(3));
    }
}
