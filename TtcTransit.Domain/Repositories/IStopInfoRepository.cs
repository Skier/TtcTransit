namespace TtcTransit.Domain.Repositories;

public interface IStopInfoRepository
{
    Task<Dictionary<string, string>> GetStopNamesAsync(CancellationToken ct = default);
}