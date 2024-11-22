using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting.WindowsServices;
using Serilog;
using Spydersoft.Hyperv.Info.Models;
using Spydersoft.Hyperv.Info.Options;
using Spydersoft.Hyperv.Info.Services;
using Spydersoft.Platform.Hosting.Options;
using Spydersoft.Platform.Hosting.StartupExtensions;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

var options = new WebApplicationOptions
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : default
};

var builder = WebApplication.CreateBuilder(options);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration);
});

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

app.MapGet("/testpsmodule", async (ILogger<Program> log) =>
{
    var defaultSessionState = InitialSessionState.CreateDefault();
    defaultSessionState.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Unrestricted;
    defaultSessionState.ImportPSModule("Hyper-V");
    using PowerShell ps = PowerShell.Create(defaultSessionState);

    ps.AddScript("Import-Module Hyper-V -SkipEditionCheck; Get-VM");
    await ps.InvokeAsync().ConfigureAwait(false);

    log.LogWarning("Executed with {Warnings} warnings.", ps.Streams.Warning.Count);
    if (ps.HadErrors)
    {
        log.LogWarning("Executed with {Errors} errors.", ps.Streams.Error.Count);
        log.LogError(ps.Streams.Error[0].Exception, "Powershell Error");

        return Results.BadRequest($"Error: {ps.Streams.Error[0].Exception}");
    }

    return Results.Ok();
}).WithName("TestPsHyperVModule").WithDisplayName("Test Powershell Hyper-V Module").WithTags("Info");

app.MapGet("/vm",
        async (ILogger<Program> log, IHyperVService commandService) =>
        {
            log.LogInformation("Fielding VirtualMachine Request (/vm)");

            IEnumerable<VirtualMachine>? vms = await commandService.VmList();
            return vms != null ? Results.Ok(vms) : Results.BadRequest();
        })
    .WithName("GetVirtualMachines").WithDisplayName("Retrieve the list of Virtual Machines").WithTags("VM");

app.MapPut("/vm/{name}", async (ILogger<Program> log, IHyperVService commandService, [FromRoute] string name, [FromBody] VirtualMachineDetails details) =>
    {
        log.LogInformation("Updating {VmName} with provided details.", name);
        var success = await commandService.SetVmNotes(name, details);
        return !success ? Results.BadRequest() : Results.Accepted();
    })
    .WithName("UpdateVirtualMachine").WithDisplayName("Update VM Details").WithTags("VM");

app.MapPost("/vm/refreshdelay", async (ILogger<Program> log, IHyperVService commandService, IConfiguration config, int? groupDelay) =>
    {
        var refresh = groupDelay ?? 480;

        log.LogInformation("Refreshing AutomaticRestartDelay with a group delay of {GroupDelay}", refresh);
        var success = await commandService.Refresh(refresh);

        return !success ? Results.BadRequest() : Results.Accepted();
    })
    .WithName("RefreshAutomaticStartDelay").WithDisplayName("Refresh AutomaticStartDelay").WithTags("VM");

await app.RunAsync();
