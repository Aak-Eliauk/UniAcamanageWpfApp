namespace UniAcamanageWpfApp.Models
{
    public class ProgressInfo
    {
        public decimal CompletedCredits { get; set; }
        public decimal TotalCredits { get; set; }
        public decimal Percentage { get; set; }
    }

    public class ProgramProgressInfo
    {
        public decimal CompletedCredits { get; set; }
        public decimal TotalCredits { get; set; }
        public decimal CompletionPercentage { get; set; }

        public ProgressInfo BaseRequiredProgress { get; set; }
        public ProgressInfo MajorRequiredProgress { get; set; }
        public ProgressInfo ElectiveProgress { get; set; }

        public int CompletedCourses { get; set; }
        public int OngoingCourses { get; set; }
        public int FailedCourses { get; set; }
        public int RemainingCourses { get; set; }
    }
}