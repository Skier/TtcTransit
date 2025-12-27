namespace TtcTransit.Domain.Repositories;

public interface IStopTimeRepository
{
    /// <summary>
    /// Возвращает запланированное время отправления (HH:mm:ss) как TimeSpan
    /// для указанного рейса и остановки.
    /// </summary>
    Task<TimeSpan?> GetScheduledDepartureAsync(
        string tripId,
        uint? stopSequence,
        string stopId,
        CancellationToken ct = default);
}