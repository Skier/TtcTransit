using TtcTransit.Domain.Entities;

namespace TtcTransit.Data.Storage;

public sealed class InMemoryGtfsStorage : IGtfsStorage
{
    private readonly List<Trip> _trips = new()
    {
        new Trip("T1", "1", "Downtown", 0),
        new Trip("T2", "1", "Uptown", 1),
        new Trip("T3", "2", "Airport", 0)
    };

    public async IAsyncEnumerable<Trip> StreamTrips(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var t in _trips)
        {
            if (ct.IsCancellationRequested) yield break;
            await Task.Yield();
            yield return t;
        }
    }

    public ValueTask<Trip?> FindTripByIdAsync(
        string tripId,
        CancellationToken ct = default)
    {
        var result = _trips.FirstOrDefault(t => t.Id == tripId);
        return ValueTask.FromResult(result);
    }
}