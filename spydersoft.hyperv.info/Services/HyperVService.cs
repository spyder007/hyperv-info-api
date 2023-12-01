using spydersoft.hyperv.info.Models;
using System.Text.Json;

namespace spydersoft.hyperv.info.Services
{
    public class HyperVService : IHyperVService
    {
        private const string GetVmList = "Get-VM | Select Name, State, AutomaticStartDelay, ProcessorCount, MemoryAssigned, @{n='StartGroup';e= {(ConvertFrom-Json $_.Notes).startGroup}}, @{n='DelayOffset';e= {(ConvertFrom-Json $_.Notes).delayOffset}} | Sort-Object startGroup, delayOffset";

        private const string SetVmNotesTemplate = "Set-Vm -Name {0} -Notes \"{1}\"";

        private const string RefreshAutomaticStartDelayTemplate = "Get-VM | Select Name, State, AutomaticStartDelay, @{{n='startGroup';e= {{(ConvertFrom-Json $_.Notes).startGroup}}}}, @{{n='delayOffset';e= {{(ConvertFrom-Json $_.Notes).delayOffset}}}} |? {{$_.startGroup -gt 0}} | % {{ Set-VM -name $_.name -AutomaticStartDelay ((($_.startGroup - 1) * {0}) + $_.delayOffset) }}";

        private readonly ILogger<HyperVService> _logger;

        private readonly IPowershellExecutor _executor;

        public HyperVService(ILogger<HyperVService> logger, IPowershellExecutor executor)
        {
            _logger = logger;
            _executor = executor;
        }

        public async Task<IEnumerable<VirtualMachine>?> VmList()
        {
            try
            {
                var pipelineObjects = await _executor.ExecuteCommandAndGetPipeline(GetVmList);
                _logger.LogDebug("Found {objects} objects", pipelineObjects.Count);
                var vms = pipelineObjects.Select(vm => new VirtualMachine(
                    vm.Properties["Name"].Value?.ToString() ?? string.Empty,
                    vm.Properties["State"].Value?.ToString() ?? string.Empty,
                    (int)vm.Properties["AutomaticStartDelay"].Value,
                    (long)vm.Properties["ProcessorCount"].Value,
                    (long)vm.Properties["MemoryAssigned"].Value,
                    int.Parse(vm.Properties["StartGroup"].Value?.ToString() ?? "0"),
                    int.Parse(vm.Properties["DelayOffset"].Value?.ToString() ?? "0")
                ));

                return vms;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error retrieving Virtual Machines");
            }

            return null;
        }

        public async Task<bool> SetVmNotes(string vmName, VirtualMachineDetails details)
        {
            var jsonSerializerOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            string notes = JsonSerializer.Serialize(details, jsonSerializerOptions);

            var command = string.Format(SetVmNotesTemplate, vmName, notes.Replace("\"", "`\""));
            _logger.LogDebug("Generated {command}", command);
            return await _executor.ExecuteCommand(command);
        }

        public async Task<bool> Refresh(int refresh)
        {
            var command = string.Format(RefreshAutomaticStartDelayTemplate, refresh);
            _logger.LogDebug("Generated {command}", command);
            return await _executor.ExecuteCommand(command);
        }
    }
}