using TtcTransit.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Читаем URL API из конфигурации
var apiBaseUrl = builder.Configuration["Api:BaseUrl"];

if (string.IsNullOrWhiteSpace(apiBaseUrl))
{
    throw new InvalidOperationException(
        "API base URL is not configured. Set Api:BaseUrl in appsettings.json.");
}

builder.Services.AddHttpClient<ApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

var app = builder.Build();

app.UseStaticFiles();
app.MapDefaultControllerRoute();

app.Run();