using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace Spydersoft.Hyperv.Info.Services
{
    public class PowershellExecutor(ILogger<PowershellExecutor> log) : IPowershellExecutor
    {
        private readonly ILogger<PowershellExecutor> _log = log;

        public async Task<PSDataCollection<PSObject>> ExecuteCommandAndGetPipeline(string command)
        {
            _log.LogInformation("Executing Powershell Command: {Command}", command);
            PSDataCollection<PSObject> pipeline;
            PowerShell ps = PrepareShell();
            try
            {
                ps.AddScript(command);
                pipeline = await ps.InvokeAsync().ConfigureAwait(false);
                if (ps.HadErrors)
                {
                    _log.LogError(ps.Streams.Error[0].Exception, "Error Executing Command {Command}:{Error}",
                        command, ps.Streams.Error[0].ErrorDetails);
                }
            }
            finally
            {
                CleanupShell(ps);
            }
            return pipeline;
        }

        public async Task<bool> ExecuteCommand(string command)
        {
            var success = false;
            PowerShell ps = PrepareShell();
            try
            {
                ps.AddScript(command);
                await ps.InvokeAsync().ConfigureAwait(false);
                success = !ps.HadErrors;
            }
            finally
            {
                CleanupShell(ps);
            }

            return success;
        }

        private static PowerShell PrepareShell()
        {
            var defaultSessionState = InitialSessionState.CreateDefault();
            defaultSessionState.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Unrestricted;
            defaultSessionState.ImportPSModule("Hyper-V");
            PowerShell ps = PowerShell.Create(defaultSessionState);
            ps.AddScript("Import-Module Hyper-V -SkipEditionCheck");
            return ps;
        }

        private void CleanupShell(PowerShell ps)
        {
            if (ps.HadErrors)
            {
                _log.LogWarning("Executed with {Errors} errors.", ps.Streams.Error.Count);
                _log.LogError(ps.Streams.Error[0].Exception, "Powershell Error");
            }

            ps.Dispose();
        }
    }
}
