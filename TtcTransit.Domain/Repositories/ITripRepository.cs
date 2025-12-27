namespace TtcTransit.Domain.Repositories;

using TtcTransit.Domain.Entities;

public interface ITripRepository
{
    IAsyncEnumerable<Trip> GetTripsByRouteAsync(
        string routeId,
        CancellationToken ct = default);

    ValueTask<Trip?> GetTripByIdAsync(
        string tripId,
        CancellationToken ct = default);
}