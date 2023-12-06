using System.Management.Automation;

namespace spydersoft.hyperv.info.Services
{
    public interface IPowershellExecutor
    {
        Task<PSDataCollection<PSObject>> ExecuteCommandAndGetPipeline(string command);

        Task<bool> ExecuteCommand(string command);
    }
}