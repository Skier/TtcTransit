using Microsoft.AspNetCore.Mvc;
using TtcTransit.Web.Services;

namespace TtcTransit.Web.Controllers;

public sealed class HomeController : Controller
{
    private readonly ApiClient _api;

    public HomeController(ApiClient api)
    {
        _api = api;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var routes = await _api.GetRoutesAsync(ct);
        return View(routes);
    }

    // Страница Privacy из меню навигации
    public IActionResult Privacy()
    {
        return View();
    }
}