using Microsoft.Data.Sqlite;
using TtcTransit.Domain.Repositories;

namespace TtcTransit.Data.Repositories;

public sealed class StopInfoRepository : IStopInfoRepository
{
    private readonly string _connectionString;

    public StopInfoRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Dictionary<string, string>> GetStopNamesAsync(CancellationToken ct = default)
    {
        var result = new Dictionary<string, string>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        const string sql = "SELECT stop_id, stop_name FROM stops";

        await using var cmd = new SqliteCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            if (ct.IsCancellationRequested)
                break;

            var id = reader.GetString(0);
            var name = reader.GetString(1);
            result[id] = name;
        }

        return result;
    }
}