using Microsoft.Extensions.Hosting;

namespace TtcTransit.Import.Services;

public sealed class GtfsImportService : BackgroundService
{
    private readonly GtfsImporter _importer;

    public GtfsImportService(GtfsImporter importer)
    {
        _importer = importer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("GTFS import started...");
        Console.WriteLine("Looking for data/gtfs.zip in solution root...");

        try
        {
            await _importer.ImportAsync(stoppingToken);
            Console.WriteLine("GTFS import finished successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("GTFS import failed:");
            Console.WriteLine(ex);
        }

        // Завершаем процесс после окончания импорта
        Environment.Exit(0);
    }
}