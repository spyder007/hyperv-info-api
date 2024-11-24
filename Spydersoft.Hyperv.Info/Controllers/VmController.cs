using Microsoft.AspNetCore.Mvc;
using Spydersoft.Hyperv.Info.Models;
using Spydersoft.Hyperv.Info.Services;

namespace Spydersoft.Hyperv.Info.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class VmController : ControllerBase
    {
        private readonly ILogger<VmController> _log;
        private readonly IHyperVService _commandService;
        public VmController(ILogger<VmController> log, IHyperVService commandService)
        {
            _log = log;
            _commandService = commandService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<VirtualMachine>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IResult> Get()
        {
            _log.LogInformation("Fielding VirtualMachine Request (/vm)");

            IEnumerable<VirtualMachine>? vms = await _commandService.VmList();
            return vms != null ? Results.Ok(vms) : Results.BadRequest();
        }

        [HttpPost("refreshdelay")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IResult> RefreshDelay(int? groupDelay)
        {
            var refresh = groupDelay ?? 480;

            _log.LogInformation("Refreshing AutomaticRestartDelay with a group delay of {GroupDelay}", refresh);
            var success = await _commandService.Refresh(refresh);

            return !success ? Results.BadRequest() : Results.Accepted();
        }

        [HttpPut("{name}")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IResult> Put([FromRoute] string name, [FromBody] VirtualMachineDetails details)
        {
            _log.LogInformation("Updating {VmName} with provided details.", name);
            var success = await _commandService.SetVmNotes(name, details);
            return !success ? Results.BadRequest() : Results.Accepted();
        }

    }
}
