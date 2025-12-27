using TtcTransit.Domain.Entities;

namespace TtcTransit.Domain.Repositories;

public interface IStopRepository
{
    IAsyncEnumerable<RouteStop> GetByRouteAsync(
        string routeId,
        CancellationToken ct = default);
}