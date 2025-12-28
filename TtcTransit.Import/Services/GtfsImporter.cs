using System.Globalization;
using System.IO.Compression;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Data.Sqlite;

namespace TtcTransit.Import.Services;

public sealed class GtfsImporter
{
    private readonly string _sqlitePath;
    private readonly string _zipPath;

    public GtfsImporter()
    {
        var baseDir = AppContext.BaseDirectory;
        var solutionRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
        var dataDir = Path.Combine(solutionRoot, "Data");

        Directory.CreateDirectory(dataDir);

        _sqlitePath = Path.Combine(dataDir, "gtfs.sqlite");
        _zipPath = Path.Combine(dataDir, "..", "gtfs.zip");
    }

    public async Task ImportAsync(CancellationToken ct)
    {
        if (!File.Exists(_zipPath))
            throw new FileNotFoundException("GTFS zip not found", _zipPath);

        // 1. Удаляем старую базу
        if (File.Exists(_sqlitePath))
            File.Delete(_sqlitePath);

        // 2. Создаём новую базу и схему
        await CreateSchemaAsync(ct);

        // 3. Распаковываем zip во временную папку
        var tempDir = Path.Combine(Path.GetTempPath(), "gtfs_import_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            ZipFile.ExtractToDirectory(_zipPath, tempDir, true);

            await using var connection = new SqliteConnection($"Data Source={_sqlitePath}");
            await connection.OpenAsync(ct);

            await ImportAgencyAsync(connection, Path.Combine(tempDir, "agency.txt"), ct);
            await ImportStopsAsync(connection, Path.Combine(tempDir, "stops.txt"), ct);
            await ImportRoutesAsync(connection, Path.Combine(tempDir, "routes.txt"), ct);
            await ImportTripsAsync(connection, Path.Combine(tempDir, "trips.txt"), ct);
            await ImportStopTimesAsync(connection, Path.Combine(tempDir, "stop_times.txt"), ct);
            await ImportCalendarAsync(connection, Path.Combine(tempDir, "calendar.txt"), ct);
            await ImportCalendarDatesAsync(connection, Path.Combine(tempDir, "calendar_dates.txt"), ct);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { /* ignore */ }
        }
    }

    private async Task CreateSchemaAsync(CancellationToken ct)
    {
        await using var connection = new SqliteConnection($"Data Source={_sqlitePath}");
        await connection.OpenAsync(ct);

        var sql = """
            PRAGMA journal_mode = WAL;

            CREATE TABLE agency (
                agency_id       TEXT PRIMARY KEY,
                agency_name     TEXT NOT NULL,
                agency_url      TEXT,
                agency_timezone TEXT NOT NULL,
                agency_lang     TEXT,
                agency_phone    TEXT,
                agency_fare_url TEXT,
                agency_email    TEXT
            );

            CREATE TABLE stops (
                stop_id        TEXT PRIMARY KEY,
                stop_name      TEXT NOT NULL,
                stop_lat       REAL,
                stop_lon       REAL,
                location_type  INTEGER,
                parent_station TEXT
            );

            CREATE TABLE routes (
                route_id         TEXT PRIMARY KEY,
                agency_id        TEXT,
                route_short_name TEXT,
                route_long_name  TEXT,
                route_type       INTEGER,
                route_color      TEXT,
                route_text_color TEXT
            );

            CREATE TABLE trips (
                trip_id       TEXT PRIMARY KEY,
                route_id      TEXT NOT NULL,
                service_id    TEXT,
                trip_headsign TEXT,
                direction_id  INTEGER
            );

            CREATE TABLE stop_times (
                trip_id         TEXT NOT NULL,
                arrival_time    TEXT,
                departure_time  TEXT,
                stop_id         TEXT NOT NULL,
                stop_sequence   INTEGER NOT NULL,
                pickup_type     INTEGER,
                drop_off_type   INTEGER
            );

            CREATE TABLE calendar (
                service_id TEXT PRIMARY KEY,
                monday     INTEGER,
                tuesday    INTEGER,
                wednesday  INTEGER,
                thursday   INTEGER,
                friday     INTEGER,
                saturday   INTEGER,
                sunday     INTEGER,
                start_date TEXT,
                end_date   TEXT
            );

            CREATE TABLE calendar_dates (
                service_id     TEXT NOT NULL,
                date           TEXT NOT NULL,
                exception_type INTEGER NOT NULL
            );

            CREATE INDEX idx_trips_route_id ON trips(route_id);
            CREATE INDEX idx_stop_times_trip_id ON stop_times(trip_id);
            CREATE INDEX idx_stop_times_stop_id ON stop_times(stop_id);
            CREATE INDEX idx_calendar_dates_service_id ON calendar_dates(service_id);
            """;

        await using var cmd = new SqliteCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ----------------- CSV helpers -----------------

    private static CsvReader CreateCsv(string path)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            MissingFieldFound = null,
            BadDataFound = null,
            HeaderValidated = null,
            DetectDelimiter = true
        };

        var reader = new StreamReader(path);
        var csv = new CsvReader(reader, config);
        return csv;
    }

    private static bool FileExists(string path) => File.Exists(path);

    // ----------------- Import таблиц -----------------

    private static async Task ImportAgencyAsync(SqliteConnection connection, string path, CancellationToken ct)
    {
        if (!FileExists(path)) return;

        await using var tx = await connection.BeginTransactionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO agency (
                agency_id, agency_name, agency_url, agency_timezone,
                agency_lang, agency_phone, agency_fare_url, agency_email
            ) VALUES (
                $id, $name, $url, $tz, $lang, $phone, $fare, $email
            )
            """;

        var pId = cmd.Parameters.Add("$id", SqliteType.Text);
        var pName = cmd.Parameters.Add("$name", SqliteType.Text);
        var pUrl = cmd.Parameters.Add("$url", SqliteType.Text);
        var pTz = cmd.Parameters.Add("$tz", SqliteType.Text);
        var pLang = cmd.Parameters.Add("$lang", SqliteType.Text);
        var pPhone = cmd.Parameters.Add("$phone", SqliteType.Text);
        var pFare = cmd.Parameters.Add("$fare", SqliteType.Text);
        var pEmail = cmd.Parameters.Add("$email", SqliteType.Text);

        using var csv = CreateCsv(path);
        await csv.ReadAsync();
        csv.ReadHeader();

        while (await csv.ReadAsync())
        {
            if (ct.IsCancellationRequested) break;

            pId.Value = csv.TryGetField("agency_id", out string? id) ? id : "default";
            pName.Value = csv.GetField("agency_name");
            pUrl.Value = csv.TryGetField("agency_url", out string? url) ? url : "";
            pTz.Value = csv.GetField("agency_timezone");
            pLang.Value = csv.TryGetField("agency_lang", out string? lang) ? lang : "";
            pPhone.Value = csv.TryGetField("agency_phone", out string? phone) ? phone : "";
            pFare.Value = csv.TryGetField("agency_fare_url", out string? fare) ? fare : "";
            pEmail.Value = csv.TryGetField("agency_email", out string? email) ? email : "";

            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    private static async Task ImportStopsAsync(SqliteConnection connection, string path, CancellationToken ct)
    {
        if (!FileExists(path)) return;

        await using var tx = await connection.BeginTransactionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO stops (
                stop_id, stop_name, stop_lat, stop_lon,
                location_type, parent_station
            ) VALUES (
                $id, $name, $lat, $lon, $locType, $parent
            )
            """;

        var pId = cmd.Parameters.Add("$id", SqliteType.Text);
        var pName = cmd.Parameters.Add("$name", SqliteType.Text);
        var pLat = cmd.Parameters.Add("$lat", SqliteType.Real);
        var pLon = cmd.Parameters.Add("$lon", SqliteType.Real);
        var pLoc = cmd.Parameters.Add("$locType", SqliteType.Integer);
        var pParent = cmd.Parameters.Add("$parent", SqliteType.Text);

        using var csv = CreateCsv(path);
        await csv.ReadAsync();
        csv.ReadHeader();

        while (await csv.ReadAsync())
        {
            if (ct.IsCancellationRequested) break;

            pId.Value = csv.GetField("stop_id");
            pName.Value = csv.GetField("stop_name");

            if (csv.TryGetField("stop_lat", out string? latStr) &&
                double.TryParse(latStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat))
                pLat.Value = lat;
            else
                pLat.Value = DBNull.Value;

            if (csv.TryGetField("stop_lon", out string? lonStr) &&
                double.TryParse(lonStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
                pLon.Value = lon;
            else
                pLon.Value = DBNull.Value;

            if (csv.TryGetField("location_type", out string? locStr) &&
                int.TryParse(locStr, out var locType))
                pLoc.Value = locType;
            else
                pLoc.Value = DBNull.Value;

            pParent.Value = csv.TryGetField("parent_station", out string? parent) ? parent : "";

            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    private static async Task ImportRoutesAsync(SqliteConnection connection, string path, CancellationToken ct)
    {
        if (!FileExists(path)) return;

        await using var tx = await connection.BeginTransactionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO routes (
                route_id, agency_id, route_short_name, route_long_name,
                route_type, route_color, route_text_color
            ) VALUES (
                $id, $agency, $short, $long, $type, $color, $textColor
            )
            """;

        var pId = cmd.Parameters.Add("$id", SqliteType.Text);
        var pAgency = cmd.Parameters.Add("$agency", SqliteType.Text);
        var pShort = cmd.Parameters.Add("$short", SqliteType.Text);
        var pLong = cmd.Parameters.Add("$long", SqliteType.Text);
        var pType = cmd.Parameters.Add("$type", SqliteType.Integer);
        var pColor = cmd.Parameters.Add("$color", SqliteType.Text);
        var pTextColor = cmd.Parameters.Add("$textColor", SqliteType.Text);

        using var csv = CreateCsv(path);
        await csv.ReadAsync();
        csv.ReadHeader();

        while (await csv.ReadAsync())
        {
            if (ct.IsCancellationRequested) break;

            pId.Value = csv.GetField("route_id");
            pAgency.Value = csv.TryGetField("agency_id", out string? ag) ? ag : "default";
            pShort.Value = csv.TryGetField("route_short_name", out string? rs) ? rs : "";
            pLong.Value = csv.TryGetField("route_long_name", out string? rl) ? rl : "";

            if (csv.TryGetField("route_type", out string? typeStr) &&
                int.TryParse(typeStr, out var type))
                pType.Value = type;
            else
                pType.Value = DBNull.Value;

            pColor.Value = csv.TryGetField("route_color", out string? color) ? color : "";
            pTextColor.Value = csv.TryGetField("route_text_color", out string? tc) ? tc : "";

            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    private static async Task ImportTripsAsync(SqliteConnection connection, string path, CancellationToken ct)
    {
        if (!FileExists(path)) return;

        await using var tx = await connection.BeginTransactionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO trips (
                trip_id, route_id, service_id, trip_headsign, direction_id
            ) VALUES (
                $id, $route, $service, $headsign, $dir
            )
            """;

        var pId = cmd.Parameters.Add("$id", SqliteType.Text);
        var pRoute = cmd.Parameters.Add("$route", SqliteType.Text);
        var pService = cmd.Parameters.Add("$service", SqliteType.Text);
        var pHeadsign = cmd.Parameters.Add("$headsign", SqliteType.Text);
        var pDir = cmd.Parameters.Add("$dir", SqliteType.Integer);

        using var csv = CreateCsv(path);
        await csv.ReadAsync();
        csv.ReadHeader();

        while (await csv.ReadAsync())
        {
            if (ct.IsCancellationRequested) break;

            pId.Value = csv.GetField("trip_id");
            pRoute.Value = csv.GetField("route_id");
            pService.Value = csv.TryGetField("service_id", out string? svc) ? svc : "";
            pHeadsign.Value = csv.TryGetField("trip_headsign", out string? hs) ? hs : "";

            if (csv.TryGetField("direction_id", out string? dirStr) &&
                int.TryParse(dirStr, out var dir))
                pDir.Value = dir;
            else
                pDir.Value = DBNull.Value;

            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    private static async Task ImportStopTimesAsync(SqliteConnection connection, string path, CancellationToken ct)
    {
        if (!FileExists(path)) return;

        await using var tx = await connection.BeginTransactionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO stop_times (
                trip_id, arrival_time, departure_time, stop_id,
                stop_sequence, pickup_type, drop_off_type
            ) VALUES (
                $trip, $arr, $dep, $stop, $seq, $pickup, $drop
            )
            """;

        var pTrip = cmd.Parameters.Add("$trip", SqliteType.Text);
        var pArr = cmd.Parameters.Add("$arr", SqliteType.Text);
        var pDep = cmd.Parameters.Add("$dep", SqliteType.Text);
        var pStop = cmd.Parameters.Add("$stop", SqliteType.Text);
        var pSeq = cmd.Parameters.Add("$seq", SqliteType.Integer);
        var pPickup = cmd.Parameters.Add("$pickup", SqliteType.Integer);
        var pDrop = cmd.Parameters.Add("$drop", SqliteType.Integer);

        using var csv = CreateCsv(path);
        await csv.ReadAsync();
        csv.ReadHeader();

        while (await csv.ReadAsync())
        {
            if (ct.IsCancellationRequested) break;

            pTrip.Value = csv.GetField("trip_id");
            pArr.Value = csv.TryGetField("arrival_time", out string? arr) ? arr : "";
            pDep.Value = csv.TryGetField("departure_time", out string? dep) ? dep : "";
            pStop.Value = csv.GetField("stop_id");

            if (csv.TryGetField("stop_sequence", out string? seqStr) &&
                int.TryParse(seqStr, out var seq))
                pSeq.Value = seq;
            else
                pSeq.Value = 0;

            if (csv.TryGetField("pickup_type", out string? puStr) &&
                int.TryParse(puStr, out var pickup))
                pPickup.Value = pickup;
            else
                pPickup.Value = DBNull.Value;

            if (csv.TryGetField("drop_off_type", out string? drStr) &&
                int.TryParse(drStr, out var drop))
                pDrop.Value = drop;
            else
                pDrop.Value = DBNull.Value;

            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    private static async Task ImportCalendarAsync(SqliteConnection connection, string path, CancellationToken ct)
    {
        if (!FileExists(path)) return;

        await using var tx = await connection.BeginTransactionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO calendar (
                service_id, monday, tuesday, wednesday, thursday,
                friday, saturday, sunday, start_date, end_date
            ) VALUES (
                $service, $mon, $tue, $wed, $thu,
                $fri, $sat, $sun, $start, $end
            )
            """;

        var pService = cmd.Parameters.Add("$service", SqliteType.Text);
        var pMon = cmd.Parameters.Add("$mon", SqliteType.Integer);
        var pTue = cmd.Parameters.Add("$tue", SqliteType.Integer);
        var pWed = cmd.Parameters.Add("$wed", SqliteType.Integer);
        var pThu = cmd.Parameters.Add("$thu", SqliteType.Integer);
        var pFri = cmd.Parameters.Add("$fri", SqliteType.Integer);
        var pSat = cmd.Parameters.Add("$sat", SqliteType.Integer);
        var pSun = cmd.Parameters.Add("$sun", SqliteType.Integer);
        var pStart = cmd.Parameters.Add("$start", SqliteType.Text);
        var pEnd = cmd.Parameters.Add("$end", SqliteType.Text);

        using var csv = CreateCsv(path);
        await csv.ReadAsync();
        csv.ReadHeader();

        while (await csv.ReadAsync())
        {
            if (ct.IsCancellationRequested) break;

            pService.Value = csv.GetField("service_id");

            pMon.Value = ParseIntOrZero(csv, "monday");
            pTue.Value = ParseIntOrZero(csv, "tuesday");
            pWed.Value = ParseIntOrZero(csv, "wednesday");
            pThu.Value = ParseIntOrZero(csv, "thursday");
            pFri.Value = ParseIntOrZero(csv, "friday");
            pSat.Value = ParseIntOrZero(csv, "saturday");
            pSun.Value = ParseIntOrZero(csv, "sunday");

            pStart.Value = csv.TryGetField("start_date", out string? sd) ? sd : "";
            pEnd.Value = csv.TryGetField("end_date", out string? ed) ? ed : "";

            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    private static async Task ImportCalendarDatesAsync(SqliteConnection connection, string path, CancellationToken ct)
    {
        if (!FileExists(path)) return;

        await using var tx = await connection.BeginTransactionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO calendar_dates (
                service_id, date, exception_type
            ) VALUES (
                $service, $date, $type
            )
            """;

        var pService = cmd.Parameters.Add("$service", SqliteType.Text);
        var pDate = cmd.Parameters.Add("$date", SqliteType.Text);
        var pType = cmd.Parameters.Add("$type", SqliteType.Integer);

        using var csv = CreateCsv(path);
        await csv.ReadAsync();
        csv.ReadHeader();

        while (await csv.ReadAsync())
        {
            if (ct.IsCancellationRequested) break;

            pService.Value = csv.GetField("service_id");
            pDate.Value = csv.GetField("date");

            if (csv.TryGetField("exception_type", out string? typeStr) &&
                int.TryParse(typeStr, out var type))
                pType.Value = type;
            else
                pType.Value = 0;

            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    private static int ParseIntOrZero(CsvReader csv, string field)
    {
        if (csv.TryGetField(field, out string? str) &&
            int.TryParse(str, out var value))
        {
            return value;
        }

        return 0;
    }
}
