namespace UniAcamanageWpfApp.Models
{
    public class TeacherCourse
    {
        public string TeacherID { get; set; }
        public int CourseID { get; set; }
        public string CourseName { get; set; }  // 从Course表关联获取
        public string CourseCode { get; set; }  // 从Course表关联获取
        public string SemesterName { get; set; } // 从Semester表关联获取
    }
}
