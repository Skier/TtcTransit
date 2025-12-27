using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TtcTransit.Data;
using TtcTransit.Import.Services;

await Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddTransitData();
        services.AddSingleton<GtfsImporter>();
        services.AddHostedService<GtfsImportService>();
    })
    .RunConsoleAsync();