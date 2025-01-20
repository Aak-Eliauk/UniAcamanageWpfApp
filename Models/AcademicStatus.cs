using System;

namespace UniAcamanageWpfApp.Models
{
    public class GradeInfo
    {
        public string Semester { get; set; }
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public string CourseType { get; set; }
        public decimal Credit { get; set; }
        public decimal Score { get; set; }
        public string GradeLevel { get; set; }
        public decimal BaseGradePoint { get; set; }
        public decimal WeightedGradePoint { get; set; }
        public string CourseStatus { get; set; }
        public string Remarks { get; set; }
        public bool IsRetake { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public string ModifiedBy { get; set; }
        public int AttemptNumber { get; internal set; }
    }

    public class AcademicStats
    {
        public decimal OverallGPA { get; set; }
        public decimal BaseRequiredGPA { get; set; }
        public decimal MajorRequiredGPA { get; set; }
        public decimal ElectiveGPA { get; set; }
        public int ClassRanking { get; set; }
        public decimal TotalCredits { get; set; }
        public decimal CompletedCredits { get; set; }
        public decimal RemainingCredits { get; set; }
        public int CompletedCourses { get; set; }
        public int OngoingCourses { get; set; }
        public int FailedCourses { get; set; }
    }

    public class CourseCompletionInfo : Course
    {
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public string CourseType { get; set; }
        public decimal Credit { get; set; }
        public string Status { get; set; }
        public string Semester { get; set; }
        public decimal Score { get; set; }
        public string Remarks { get; set; }
        public int CourseID { get; set; }
        public bool IsRetake { get; set; }
    }


}