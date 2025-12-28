using Microsoft.Extensions.DependencyInjection;
using TtcTransit.Data.Repositories;
using TtcTransit.Data.Storage;
using TtcTransit.Domain.Repositories;

namespace TtcTransit.Data;

public static class DataServiceCollectionExtensions
{
    private static string GetDbPath()
    {
        // 1. Пробуем взять путь из переменной среды
        var envPath = Environment.GetEnvironmentVariable("GTFS_DB_PATH");
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            return envPath;
        }

        // 2. Локальная разработка: ищем Data/gtfs.sqlite рядом с солюшеном
        var baseDir = AppContext.BaseDirectory;
        var solutionRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
        var dataDir = Path.Combine(solutionRoot, "Data");
        Directory.CreateDirectory(dataDir);
        return Path.Combine(dataDir, "gtfs.sqlite");
    }

    public static IServiceCollection AddTransitData(this IServiceCollection services)
    {
        var dbPath = GetDbPath();
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