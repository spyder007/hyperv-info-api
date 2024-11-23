using Microsoft.Extensions.Hosting.WindowsServices;
using Spydersoft.Hyperv.Info.Options;
using Spydersoft.Hyperv.Info.Services;
using Spydersoft.Platform.Hosting.Options;
using Spydersoft.Platform.Hosting.StartupExtensions;

var options = new WebApplicationOptions
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : default
};

var builder = WebApplication.CreateBuilder(options);

var hostSettings = new HostSettings();
builder.Configuration.GetSection(HostSettings.SectionName).Bind(hostSettings);
builder.WebHost.UseUrls($"{hostSettings.Host}:{hostSettings.Port}");

AppHealthCheckOptions healthCheckOptions = builder.AddSpydersoftHealthChecks();

builder.AddSpydersoftTelemetry(typeof(Program).Assembly)
    .AddSpydersoftSerilog();

bool authInstalled = builder.AddSpydersoftIdentity();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton<IPowershellExecutor, PowershellExecutor>();
builder.Services.AddSingleton<IHyperVService, HyperVService>();
builder.Services.AddControllers();

builder.Services.AddOpenApiDocument(doc =>
{
    doc.DocumentName = "hyperv.info";
    doc.Title = "HyperV Info API";
    doc.Description = "API for interacting with HyperV";
});

builder.Host.UseWindowsService();

var app = builder.Build();

app.UseSpydersoftHealthChecks(healthCheckOptions);
app.UseOpenApi();
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUi();
}

app.UseAuthentication(authInstalled).UseAuthorization(authInstalled);
app.MapControllers();

await app.RunAsync();
