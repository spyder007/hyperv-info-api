namespace Spydersoft.Hyperv.Info.Models
{
    public record VirtualMachine(string Name, string State, int AutomaticStartDelay, long ProcessorCount, long MemoryAssigned, int? StartGroup, int? DelayOffset)
    {
    }
}