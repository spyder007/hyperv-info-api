using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting.WindowsServices;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using Serilog;
using spydersoft.hyperv.info.Models;
using spydersoft.hyperv.info.Services;
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

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
bool configureAuthentication = !string.IsNullOrEmpty(builder.Configuration.GetValue<string>("Identity:AuthorityUrl"));

if (configureAuthentication)
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(jwtBearerOptions =>
            {
                jwtBearerOptions.Authority = builder.Configuration.GetValue<string>("Identity:AuthorityUrl");
                jwtBearerOptions.Audience = builder.Configuration.GetValue<string>("Identity:ApiName");

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
builder.Services.AddSingleton<IVmCommandService, VmCommandService>();

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

    log.LogWarning(ps.Streams.Warning.Count.ToString());
    if (ps.HadErrors)
    {
        log.LogWarning(ps.Streams.Error.Count.ToString());
        log.LogError(ps.Streams.Error[0].Exception, "Powershell Error");

        return Results.BadRequest($"Error: {ps.Streams.Error[0].Exception.ToString()}");
    }

    return Results.Ok();
}).WithName("TestPsHyperVModule").WithDisplayName("Test Powershell Hyper-V Module").WithTags("Info"); 

app.MapGet("/vm",
        async (ILogger<Program> log, IPowershellExecutor powershellHelper, IVmCommandService commandService) =>
        {
            log.LogInformation("Fielding VirtualMachine Request (/vm)");

            try
            {
                var pipelineObjects = await powershellHelper.ExecuteCommandAndGetPipeline(commandService.VmList());

                var vms = pipelineObjects.Select(vm => new VirtualMachine(
                    vm.Properties["Name"].Value?.ToString() ?? string.Empty,
                    vm.Properties["State"].Value?.ToString() ?? string.Empty,
                    (int)vm.Properties["AutomaticStartDelay"].Value,
                    int.Parse(vm.Properties["StartGroup"].Value?.ToString() ?? "0"),
                    int.Parse(vm.Properties["DelayOffset"].Value?.ToString() ?? "0")
                ));

                return vms;
            }
            catch (Exception e)
            {
                log.LogError(e, "Error retrieving Virtual Machines");
                return null;
            }
        })
    .WithName("GetVirtualMachines").WithDisplayName("Retrieve the list of Virtual Machines").WithTags("VM");

app.MapPut("/vm/{name}", async (ILogger<Program> log, IPowershellExecutor powershellHelper,
        IVmCommandService commandService, [FromRoute] string name, [FromBody] VirtualMachineDetails details) =>
    {
        log.LogInformation("Updating {vmName} with provided details.", name);
        var success = await powershellHelper.ExecuteCommand(commandService.SetVmNotes(name, details));

        return !success ? Results.BadRequest() : Results.Accepted();
    })
    .WithName("UpdateVirtualMachine").WithDisplayName("Update VM Details").WithTags("VM");

app.MapPost("/vm/refreshdelay", async (ILogger<Program> log, IPowershellExecutor powershellHelper,
        IVmCommandService commandService, IConfiguration config, int? groupDelay) =>
    {
        var refresh = groupDelay ?? 480;

        log.LogInformation("Refreshing AutomaticRestartDelay with a group delay of {groupDelay}", refresh);
        var success = await powershellHelper.ExecuteCommand(commandService.Refresh(refresh));

        return !success ? Results.BadRequest() : Results.Accepted();
    })
    .WithName("RefreshAutomaticStartDelay").WithDisplayName("Refresh AutomaticStartDelay").WithTags("VM");

app.Run();