using Microsoft.AspNetCore.Mvc;
using TtcTransit.Web.Models;
using TtcTransit.Web.Services;

namespace TtcTransit.Web.Controllers;

public sealed class RealtimeController : Controller
{
    private readonly ApiClient _api;

    public RealtimeController(ApiClient api)
    {
        _api = api;
    }

    // /Realtime
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var delays = await _api.GetRealtimeDelaysAsync(499, ct);
        return View(delays);
    }
}