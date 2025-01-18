public class Course
{
    public int CourseID { get; set; }
    public string CourseCode { get; set; }
    public string CourseName { get; set; }
    public string CourseType { get; set; }
    public decimal Credit { get; set; }
    public string ScheduleTime { get; set; }
    public string Classroom { get; set; }
    public string TeacherName { get; set; }
    public string Capacity { get; set; }
    public string Description { get; set; }
    public bool IsSelected { get; set; }
    public string Status { get; set; }
    public int SemesterId { get; set; }
}

public class CourseSelectionResult : Course
{
    public string ApprovalStatus { get; set; }
    public string RejectReason { get; set; }
}

public enum CourseSelectionStatus
{
    PendingApproval,
    Approved,
    Rejected
}

public class Semester
{
    public int SemesterId { get; set; }
    public string SemesterName { get; set; }
    public int AcademicYearId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string AcademicYearName { get; set; }  // 添加这个属性

    // 重写 ToString 方法以便在 ComboBox 中显示
    public override string ToString()
    {
        return $"{SemesterName}";
    }
}

