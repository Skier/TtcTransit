using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;
using TtcTransit.Domain.Entities;
using TtcTransit.Domain.Repositories;

namespace TtcTransit.Data.Repositories;

public sealed class StopScheduleRepository : IStopScheduleRepository
{
    private readonly string _connectionString;

    public StopScheduleRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async IAsyncEnumerable<StopScheduleEntry> GetScheduleAsync(
        string stopId,
        DateOnly date,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var dateInt = date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var weekday = (int)date.DayOfWeek; // Sunday=0 ... Saturday=6

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        const string sql = """
            SELECT
                st.stop_id,
                st.trip_id,
                t.route_id,
                COALESCE(r.route_short_name, ''),
                COALESCE(r.route_long_name, ''),
                t.trip_headsign,
                t.direction_id,
                st.arrival_time,
                st.departure_time
            FROM stop_times st
            INNER JOIN trips t ON t.trip_id = st.trip_id
            INNER JOIN routes r ON r.route_id = t.route_id
            LEFT JOIN calendar c ON c.service_id = t.service_id
            LEFT JOIN calendar_dates cd
                ON cd.service_id = t.service_id
               AND cd.date = $dateInt
            WHERE st.stop_id = $stopId
              AND (
                    -- Обычная служба по calendar
                    (
                       c.service_id IS NOT NULL
                       AND c.start_date <= $dateInt
                       AND c.end_date   >= $dateInt
                       AND CASE $weekday
                            WHEN 1 THEN c.monday
                            WHEN 2 THEN c.tuesday
                            WHEN 3 THEN c.wednesday
                            WHEN 4 THEN c.thursday
                            WHEN 5 THEN c.friday
                            WHEN 6 THEN c.saturday
                            WHEN 0 THEN c.sunday
                           END = 1
                       AND (cd.exception_type IS NULL OR cd.exception_type != 2)
                    )
                    OR
                    -- Исключения: добавленные рейсы (exception_type = 1)
                    (cd.exception_type = 1)
                  )
            ORDER BY
                t.route_id,
                t.direction_id,
                st.arrival_time
            """;

        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$stopId", stopId);
        cmd.Parameters.AddWithValue("$dateInt", dateInt);
        cmd.Parameters.AddWithValue("$weekday", weekday);

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            if (ct.IsCancellationRequested)
                yield break;

            var sId = reader.GetString(0);
            var tripId = reader.GetString(1);
            var routeId = reader.GetString(2);
            var routeShort = reader.GetString(3);
            var routeLong = reader.GetString(4);
            string? headSign = reader.IsDBNull(5) ? null : reader.GetString(5);
            int? directionId = reader.IsDBNull(6) ? null : reader.GetInt32(6);
            var arr = reader.IsDBNull(7) ? "" : reader.GetString(7);
            var dep = reader.IsDBNull(8) ? "" : reader.GetString(8);

            yield return new StopScheduleEntry(
                sId,
                tripId,
                routeId,
                routeShort,
                routeLong,
                headSign,
                directionId,
                arr,
                dep);
        }
    }
}
