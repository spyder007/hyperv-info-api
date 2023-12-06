namespace Spydersoft.Hyperv.Info.Options
{
    public class HostSettings
    {
        public const string SectionName = "HostSettings";
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 5000;
    }
}