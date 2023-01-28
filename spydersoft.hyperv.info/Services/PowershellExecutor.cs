using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace spydersoft.hyperv.info.Services
{
    public class PowershellExecutor : IPowershellExecutor
    {
        private readonly ILogger<PowershellExecutor> _log;

        public PowershellExecutor(ILogger<PowershellExecutor> log)
        {
            _log = log;
        }

        public async Task<PSDataCollection<PSObject>> ExecuteCommandAndGetPipeline(string command)
        {
            _log.LogInformation($"Executing Powershell Command: {command}");
            PSDataCollection<PSObject> pipeline;
            PowerShell ps = PrepareShell();
            try
            {
                ps.AddScript(command);
                pipeline = await ps.InvokeAsync().ConfigureAwait(false);
                if (ps.HadErrors)
                {
                    _log.LogError(ps.Streams.Error.First().Exception, "Error Executing Command {command}:{error}",
                        command, ps.Streams.Error.First().ErrorDetails);
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

        private PowerShell PrepareShell()
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
                _log.LogWarning(ps.Streams.Error.Count.ToString());
                _log.LogError(ps.Streams.Error[0].Exception, "Powershell Error");
            }

            ps.Dispose();
        }
    }
}