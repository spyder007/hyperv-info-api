using System.Management.Automation;

namespace Spydersoft.Hyperv.Info.Services
{
    public interface IPowershellExecutor
    {
        Task<PSDataCollection<PSObject>> ExecuteCommandAndGetPipeline(string command);

        Task<bool> ExecuteCommand(string command);
    }
}