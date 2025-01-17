// Models/SemesterItem.cs
namespace UniAcamanageWpfApp.Models
{
    public class SemesterItem
    {
        public int SemesterID { get; set; }
        public string SemesterName { get; set; }
        public string YearName { get; set; }
        public string DisplayName => $"{YearName} {SemesterName}";
    }
}