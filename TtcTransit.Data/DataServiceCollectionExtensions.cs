using Microsoft.Extensions.DependencyInjection;
using TtcTransit.Data.Repositories;
using TtcTransit.Data.Storage;
using TtcTransit.Domain.Repositories;

namespace TtcTransit.Data;

public static class DataServiceCollectionExtensions
{
    private static string GetSolutionDataPath()
    {
        var baseDir = AppContext.BaseDirectory;
        var solutionRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
        var dataDir = Path.Combine(solutionRoot, "Data");
        Directory.CreateDirectory(dataDir);
        return dataDir;
    }

    public static IServiceCollection AddTransitData(this IServiceCollection services)
    {
        var dataDir = GetSolutionDataPath();
        var dbPath = Path.Combine(dataDir, "gtfs.sqlite");

        var connectionString = $"Data Source={dbPath}";

        services.AddSingleton<IGtfsStorage>(_ => new GtfsSqliteStorage(connectionString));

        services.AddScoped<ITripRepository, TripRepository>();
        services.AddScoped<IRouteRepository>(_ => new RouteRepository(connectionString));
        services.AddScoped<IStopRepository>(_ => new StopRepository(connectionString));
        services.AddScoped<IStopScheduleRepository>(_ => new StopScheduleRepository(connectionString));
        services.AddScoped<IStopInfoRepository>(_ => new StopInfoRepository(connectionString));
        services.AddScoped<IStopTimeRepository>(_ => new StopTimeRepository(connectionString));


        return services;
    }
}