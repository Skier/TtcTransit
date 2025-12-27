using TtcTransit.Domain.Entities;

namespace TtcTransit.Data.Storage;

public interface IGtfsStorage
{
    IAsyncEnumerable<Trip> StreamTrips(
        CancellationToken ct = default);

    ValueTask<Trip?> FindTripByIdAsync(
        string tripId,
        CancellationToken ct = default);
}