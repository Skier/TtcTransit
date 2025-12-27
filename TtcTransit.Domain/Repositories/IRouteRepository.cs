using TtcTransit.Domain.Entities;

namespace TtcTransit.Domain.Repositories;

public interface IRouteRepository
{
    IAsyncEnumerable<Route> GetAllAsync(
        CancellationToken ct = default);

    ValueTask<Route?> GetByIdAsync(
        string routeId,
        CancellationToken ct = default);
}