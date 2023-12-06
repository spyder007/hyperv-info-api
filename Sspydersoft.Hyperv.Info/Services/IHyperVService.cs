using spydersoft.hyperv.info.Models;

namespace spydersoft.hyperv.info.Services
{
    public interface IHyperVService
    {
        Task<IEnumerable<VirtualMachine>?> VmList();

        Task<bool> SetVmNotes(string vmName, VirtualMachineDetails details);

        Task<bool> Refresh(int refresh);
    }
}