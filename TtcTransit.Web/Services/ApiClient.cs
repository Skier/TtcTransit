using System.Net;
using System.Net.Http.Json;
using TtcTransit.Web.Models;

namespace TtcTransit.Web.Services;

public sealed class ApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<ApiClient> _logger;

    public ApiClient(HttpClient http, ILogger<ApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RouteViewModel>> GetRoutesAsync(CancellationToken ct = default)
    {
        HttpResponseMessage response;

        try
        {
            response = await _http.GetAsync("/api/routes", ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while calling API /api/routes at {BaseAddress}", _http.BaseAddress);
            return new List<RouteViewModel>();
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("API /api/routes returned {StatusCode} from {BaseAddress}",
                (int)response.StatusCode, _http.BaseAddress);

            return new List<RouteViewModel>();
        }

        var data = await response.Content.ReadFromJsonAsync<List<RouteViewModel>>(cancellationToken: ct);
        return data ?? new List<RouteViewModel>();
    }

    public async Task<IReadOnlyList<StopViewModel>> GetStopsByRouteAsync(string routeId, CancellationToken ct = default)
    {
        HttpResponseMessage response;

        try
        {
            response = await _http.GetAsync($"/api/routes/{routeId}/stops", ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while calling API /api/routes/{RouteId}/stops at {BaseAddress}",
                routeId, _http.BaseAddress);
            return new List<StopViewModel>();
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("API /api/routes/{RouteId}/stops returned {StatusCode} from {BaseAddress}",
                routeId, (int)response.StatusCode, _http.BaseAddress);

            return new List<StopViewModel>();
        }

        var data = await response.Content.ReadFromJsonAsync<List<StopViewModel>>(cancellationToken: ct);
        return data ?? new List<StopViewModel>();
    }

    public async Task<IReadOnlyList<StopScheduleItemViewModel>> GetStopScheduleAsync(
        string stopId,
        DateOnly? date = null,
        CancellationToken ct = default)
    {
        string url;

        if (date is null)
        {
            url = $"/api/stops/{stopId}/schedule";
        }
        else
        {
            url = $"/api/stops/{stopId}/schedule?date={date.Value:yyyy-MM-dd}";
        }

        HttpResponseMessage response;

        try
        {
            response = await _http.GetAsync(url, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while calling {Url} at {BaseAddress}", url, _http.BaseAddress);
            return new List<StopScheduleItemViewModel>();
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("API {Url} returned {StatusCode} from {BaseAddress}",
                url, (int)response.StatusCode, _http.BaseAddress);

            return new List<StopScheduleItemViewModel>();
        }

        var data = await response.Content.ReadFromJsonAsync<List<StopScheduleItemViewModel>>(cancellationToken: ct);
        return data ?? new List<StopScheduleItemViewModel>();
    }

    public async Task<IReadOnlyList<RealtimeStopArrivalViewModel>> GetRealtimeArrivalsAsync(
        string stopId,
        int maxResults = 5,
        CancellationToken ct = default)
    {
        var url = $"/api/stops/{stopId}/next?max={maxResults}";

        HttpResponseMessage response;

        try
        {
            response = await _http.GetAsync(url, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while calling {Url} at {BaseAddress}", url, _http.BaseAddress);
            return new List<RealtimeStopArrivalViewModel>();
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("API {Url} returned {StatusCode} from {BaseAddress}",
                url, (int)response.StatusCode, _http.BaseAddress);

            return new List<RealtimeStopArrivalViewModel>();
        }

        var data = await response.Content.ReadFromJsonAsync<List<RealtimeStopArrivalViewModel>>(cancellationToken: ct);
        return data ?? new List<RealtimeStopArrivalViewModel>();
    }

    public async Task<IReadOnlyList<RealtimeDelayViewModel>> GetRealtimeDelaysAsync(
        int maxResults = 100,
        CancellationToken ct = default)
    {
        var url = $"/api/realtime/delays?max={maxResults}";

        HttpResponseMessage response;

        try
        {
            response = await _http.GetAsync(url, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while calling {Url} at {BaseAddress}", url, _http.BaseAddress);
            return new List<RealtimeDelayViewModel>();
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("API {Url} returned {StatusCode} from {BaseAddress}",
                url, (int)response.StatusCode, _http.BaseAddress);

            return new List<RealtimeDelayViewModel>();
        }

        var data = await response.Content.ReadFromJsonAsync<List<RealtimeDelayViewModel>>(cancellationToken: ct);
        return data ?? new List<RealtimeDelayViewModel>();
    }

}
