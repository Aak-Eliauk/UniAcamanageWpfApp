public class CourseSearchResult
{
    public int CourseID { get; set; }
    public string CourseCode { get; set; }
    public string CourseName { get; set; }
    public string DisplayText => $"{CourseCode} - {CourseName}";
}