using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;
using TtcTransit.Domain.Entities;
using TtcTransit.Domain.Repositories;

namespace TtcTransit.Data.Repositories;

public sealed class RouteRepository : IRouteRepository
{
    private readonly string _connectionString;

    public RouteRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async IAsyncEnumerable<Route> GetAllAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        const string sql = """
            SELECT route_id,
                   COALESCE(route_short_name, ''),
                   COALESCE(route_long_name, ''),
                   route_type,
                   route_color,
                   route_text_color
            FROM routes
            ORDER BY route_short_name, route_long_name, route_id
            """;

        await using var cmd = new SqliteCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            if (ct.IsCancellationRequested)
                yield break;

            var id = reader.GetString(0);
            var shortName = reader.GetString(1);
            var longName = reader.GetString(2);

            int? routeType = reader.IsDBNull(3) ? null : reader.GetInt32(3);
            string? color = reader.IsDBNull(4) ? null : reader.GetString(4);
            string? textColor = reader.IsDBNull(5) ? null : reader.GetString(5);

            yield return new Route(id, shortName, longName, routeType, color, textColor);
        }
    }

    public async ValueTask<Route?> GetByIdAsync(string routeId, CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        const string sql = """
            SELECT route_id,
                   COALESCE(route_short_name, ''),
                   COALESCE(route_long_name, ''),
                   route_type,
                   route_color,
                   route_text_color
            FROM routes
            WHERE route_id = $id
            """;

        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", routeId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
            return null;

        var id = reader.GetString(0);
        var shortName = reader.GetString(1);
        var longName = reader.GetString(2);

        int? routeType = reader.IsDBNull(3) ? null : reader.GetInt32(3);
        string? color = reader.IsDBNull(4) ? null : reader.GetString(4);
        string? textColor = reader.IsDBNull(5) ? null : reader.GetString(5);

        return new Route(id, shortName, longName, routeType, color, textColor);
    }
}
