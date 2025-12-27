using TtcTransit.Domain.Entities;

namespace TtcTransit.Domain.Repositories;

public interface IStopScheduleRepository
{
    IAsyncEnumerable<StopScheduleEntry> GetScheduleAsync(
        string stopId,
        DateOnly date,
        CancellationToken ct = default);
}