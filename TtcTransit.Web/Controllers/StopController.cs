using Microsoft.AspNetCore.Mvc;
using TtcTransit.Web.Models;
using TtcTransit.Web.Services;

namespace TtcTransit.Web.Controllers;

public sealed class StopController : Controller
{
    private readonly ApiClient _api;

    public StopController(ApiClient api)
    {
        _api = api;
    }

    // /Stop/Details/{id}?date=2025-12-25
    public async Task<IActionResult> Details(string id, string? date, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        DateOnly targetDate;

        if (!string.IsNullOrWhiteSpace(date) &&
            DateOnly.TryParse(date, out var parsed))
        {
            targetDate = parsed;
        }
        else
        {
            targetDate = DateOnly.FromDateTime(DateTime.Today);
        }

        var scheduleTask = _api.GetStopScheduleAsync(id, targetDate, ct);
        var realtimeTask = _api.GetRealtimeArrivalsAsync(id, 5, ct);

        await Task.WhenAll(scheduleTask, realtimeTask);

        var vm = new StopDetailsViewModel
        {
            StopId = id,
            Date = targetDate,
            Schedule = scheduleTask.Result,
            Realtime = realtimeTask.Result
        };

        return View(vm);
    }
}