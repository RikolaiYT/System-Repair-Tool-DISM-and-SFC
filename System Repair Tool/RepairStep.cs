using System.Text;

namespace SystemRepairTool
{
    internal class RepairStep
    {
        public string Name { get; set; }

        public string Command { get; set; }

        public string DismVerb { get; set; }

        public Encoding Encoding { get; set; }

        public bool AllowNonZeroExitCode { get; set; }

        public bool MarkErrorOutputAsFailure { get; set; } = true;
    }
}
