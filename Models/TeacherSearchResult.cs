public class TeacherSearchResult
{
    public string TeacherID { get; set; }
    public string Name { get; set; }
    public string Title { get; set; }
    public string DisplayText => $"{TeacherID} - {Name} ({Title})";
}
