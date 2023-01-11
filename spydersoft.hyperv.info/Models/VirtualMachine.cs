namespace spydersoft.hyperv.info.Models
{
    internal record VirtualMachine(string Name, string State, int AutomaticStartDelay, int? StartGroup, int? DelayOffset)
    {
    }
}
