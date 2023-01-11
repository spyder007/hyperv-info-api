using spydersoft.hyperv.info.Models;
using System.Text.Json;

namespace spydersoft.hyperv.info.Services
{
    public class VmCommandService : IVmCommandService
    {
        private const string GetVmList = "Get-VM | Select Name, State, AutomaticStartDelay, @{n='StartGroup';e= {(ConvertFrom-Json $_.Notes).startGroup}}, @{n='DelayOffset';e= {(ConvertFrom-Json $_.Notes).delayOffset}} | Sort-Object startGroup, delayOffset";

        private const string SetVmNotesTemplate = "Set-Vm -Name {0} -Notes \"{1}\"";

        private const string RefreshAutomaticStartDelayTemplate = "Get-VM | Select Name, State, AutomaticStartDelay, @{{n='startGroup';e= {{(ConvertFrom-Json $_.Notes).startGroup}}}}, @{{n='delayOffset';e= {{(ConvertFrom-Json $_.Notes).delayOffset}}}} |? {{$_.startGroup -gt 0}} | % {{ Set-VM -name $_.name -AutomaticStartDelay ((($_.startGroup - 1) * {0}) + $_.delayOffset) }}";

        private readonly ILogger<VmCommandService> _logger;

        public VmCommandService(ILogger<VmCommandService> logger)
        {
            _logger = logger;
        }

        public string VmList()
        {
            return GetVmList;
        }

        public string SetVmNotes(string vmName, VirtualMachineDetails details)
        {
            var jsonSerializerOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            string notes = JsonSerializer.Serialize(details, jsonSerializerOptions);

            var command = string.Format(SetVmNotesTemplate, vmName, notes.Replace("\"", "`\""));
            _logger.LogDebug("Generated {command}", command);
            return command;
        }

        public string Refresh(int refresh)
        {
            var command = string.Format(RefreshAutomaticStartDelayTemplate, refresh);
            _logger.LogDebug("Generated {command}", command);
            return command;
        }
    }
}