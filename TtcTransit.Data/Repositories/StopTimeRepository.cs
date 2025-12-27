using System.Globalization;
using Microsoft.Data.Sqlite;
using TtcTransit.Domain.Repositories;

namespace TtcTransit.Data.Repositories;

public sealed class StopTimeRepository : IStopTimeRepository
{
    private readonly string _connectionString;

    public StopTimeRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<TimeSpan?> GetScheduledDepartureAsync(
        string tripId,
        uint? stopSequence,
        string stopId,
        CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        // Сначала пробуем по trip_id + stop_sequence
        if (stopSequence.HasValue)
        {
            const string sqlSeq = """
                SELECT departure_time
                FROM stop_times
                WHERE trip_id = $tripId AND stop_sequence = $seq
                LIMIT 1
                """;

            await using var cmd = new SqliteCommand(sqlSeq, connection);
            cmd.Parameters.AddWithValue("$tripId", tripId);
            cmd.Parameters.AddWithValue("$seq", stopSequence.Value);

            var val = await cmd.ExecuteScalarAsync(ct);
            if (val is string s && !string.IsNullOrWhiteSpace(s))
            {
                if (TryParseGtfsTime(s, out var ts))
                    return ts;
            }
        }

        // Если seq нет или не нашли — fallback по trip_id + stop_id
        const string sqlStop = """
            SELECT departure_time
            FROM stop_times
            WHERE trip_id = $tripId AND stop_id = $stopId
            ORDER BY stop_sequence
            LIMIT 1
            """;

        await using (var cmd = new SqliteCommand(sqlStop, connection))
        {
            cmd.Parameters.AddWithValue("$tripId", tripId);
            cmd.Parameters.AddWithValue("$stopId", stopId);

            var val = await cmd.ExecuteScalarAsync(ct);
            if (val is string s && !string.IsNullOrWhiteSpace(s))
            {
                if (TryParseGtfsTime(s, out var ts))
                    return ts;
            }
        }

        return null;
    }

    private static bool TryParseGtfsTime(string value, out TimeSpan time)
    {
        // GTFS допускает часы > 24 (например "25:30:00")
        // TimeSpan.Parse с InvariantCulture такое переварит.
        return TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out time);
    }
}
