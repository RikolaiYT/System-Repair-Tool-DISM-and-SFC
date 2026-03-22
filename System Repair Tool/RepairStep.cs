using System.Text;

class RepairStep
{
    public string Name { get; set; }
    public string Command { get; set; } // если null → DISM
    public Encoding Encoding { get; set; }
}