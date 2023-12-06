using Spydersoft.Hyperv.Info.Models;

namespace Spydersoft.Hyperv.Info.Services
{
    public interface IHyperVService
    {
        Task<IEnumerable<VirtualMachine>?> VmList();

        Task<bool> SetVmNotes(string vmName, VirtualMachineDetails details);

        Task<bool> Refresh(int refresh);
    }
}