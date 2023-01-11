using spydersoft.hyperv.info.Models;

namespace spydersoft.hyperv.info.Services
{
    public interface IVmCommandService
    {
        string VmList();

        string SetVmNotes(string vmName, VirtualMachineDetails details);

        string Refresh(int refresh);
    }
}