using TtcTransit.Domain.Entities;
using TtcTransit.Domain.Repositories;
using TtcTransit.Data.Storage;

namespace TtcTransit.Data.Repositories;

public sealed class TripRepository : ITripRepository
{
    private readonly IGtfsStorage _storage;

    public TripRepository(IGtfsStorage storage)
    {
        _storage = storage;
    }

    public async IAsyncEnumerable<Trip> GetTripsByRouteAsync(
        string routeId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var trip in _storage.StreamTrips(ct))
        {
            if (ct.IsCancellationRequested)
                yield break;

            if (trip.RouteId == routeId)
                yield return trip;
        }
    }

    public ValueTask<Trip?> GetTripByIdAsync(
        string tripId,
        CancellationToken ct = default)
        => _storage.FindTripByIdAsync(tripId, ct);
}