namespace spydersoft.hyperv.info.Models
{
    public record VirtualMachine(string Name, string State, int AutomaticStartDelay, int? StartGroup, int? DelayOffset)
    {
    }
}