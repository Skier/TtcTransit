using Microsoft.AspNetCore.Mvc;
using TtcTransit.Web.Services;

namespace TtcTransit.Web.Controllers;

public sealed class RouteController : Controller
{
    private readonly ApiClient _api;

    public RouteController(ApiClient api)
    {
        _api = api;
    }

    // /Route/Stops/{id}
    public async Task<IActionResult> Stops(string id, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var stops = await _api.GetStopsByRouteAsync(id, ct);
        ViewBag.RouteId = id;

        return View(stops);
    }
}