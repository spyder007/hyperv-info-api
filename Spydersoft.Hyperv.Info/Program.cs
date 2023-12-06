using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting.WindowsServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Serilog;
using Spydersoft.Hyperv.Info.Models;
using Spydersoft.Hyperv.Info.Options;
using Spydersoft.Hyperv.Info.Services;
using System.Diagnostics;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;

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

var identitySettings = new IdentitySettings();
builder.Configuration.GetSection(IdentitySettings.SectionName).Bind(identitySettings);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
bool configureAuthentication = !string.IsNullOrEmpty(identitySettings.AuthorityUrl);

if (configureAuthentication)
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(jwtBearerOptions =>
            {
                jwtBearerOptions.Authority = identitySettings.AuthorityUrl;
                jwtBearerOptions.Audience = identitySettings.ApiName;

                jwtBearerOptions.TokenValidationParameters.ValidTypes = new[] { "at+jwt" };
            }
        );
    builder.Services.AddAuthorization(cfg =>
    {
        cfg.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    });
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton<IPowershellExecutor, PowershellExecutor>();
builder.Services.AddSingleton<IHyperVService, HyperVService>();

builder.Services.AddOpenApiDocument(doc =>
{
    doc.DocumentName = "hyperv.info";
    doc.Title = "HyperV Info API";
    doc.Description = "API for interacting with HyperV";
    doc.SerializerSettings = new JsonSerializerSettings
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver()
    };
});

builder.Host.UseWindowsService();

var app = builder.Build();

app.UseOpenApi();
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUi3();
}

if (configureAuthentication)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapGet("/info", async (ILogger<Program> log) =>
{
    var version = "0.0.0.0";
    try
    {
        await Task.Run(() =>
        {
            var fileInfo = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);

            version = fileInfo.ProductVersion;
        });
    }
    catch (Exception e)
    {
        log.LogError(e, "Error retrieving file version");
        version = "0.0.0.0";
    }

    return Results.Ok(version);
}).WithName("info").WithDisplayName("Retrieve Application Info").WithTags("Info");

app.MapGet("/testpsmodule", async (ILogger<Program> log) =>
{
    var defaultSessionState = InitialSessionState.CreateDefault();
    defaultSessionState.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Unrestricted;
    defaultSessionState.ImportPSModule("Hyper-V");
    using PowerShell ps = PowerShell.Create(defaultSessionState);

    ps.AddScript("Import-Module Hyper-V -SkipEditionCheck; Get-VM");
    await ps.InvokeAsync().ConfigureAwait(false);

    log.LogWarning("Executed with {warnings} warnings.", ps.Streams.Warning.Count);
    if (ps.HadErrors)
    {
        log.LogWarning("Executed with {errors} errors.", ps.Streams.Error.Count);
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
        log.LogInformation("Updating {vmName} with provided details.", name);
        var success = await commandService.SetVmNotes(name, details);
        return !success ? Results.BadRequest() : Results.Accepted();
    })
    .WithName("UpdateVirtualMachine").WithDisplayName("Update VM Details").WithTags("VM");

app.MapPost("/vm/refreshdelay", async (ILogger<Program> log, IHyperVService commandService, IConfiguration config, int? groupDelay) =>
    {
        var refresh = groupDelay ?? 480;

        log.LogInformation("Refreshing AutomaticRestartDelay with a group delay of {groupDelay}", refresh);
        var success = await commandService.Refresh(refresh);

        return !success ? Results.BadRequest() : Results.Accepted();
    })
    .WithName("RefreshAutomaticStartDelay").WithDisplayName("Refresh AutomaticStartDelay").WithTags("VM");

app.Run();