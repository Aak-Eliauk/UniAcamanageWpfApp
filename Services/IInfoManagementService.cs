using UniAcamanageWpfApp.Models;

namespace UniAcamanageWpfApp.Services
{
    public interface IInfoManagementService
    {
        Task<List<Department>> GetDepartmentsAsync();
        Task<List<Class>> GetClassesByDepartmentAsync(string departmentId);
        Task<List<Student>> SearchStudentsAsync(string searchText, string major = null, string classId = null);
        Task<List<Student>> GetStudentsByClassAsync(string departmentId, int admissionYear, string classId = null);
        Task<List<Semester>> GetSemestersAsync();
        Task<int> AddSemesterAsync(Semester semester);
        Task<bool> UpdateSemesterAsync(Semester semester);
        Task<bool> DeleteSemesterAsync(int semesterId);
        Task<List<Teacher>> GetTeachersAsync();
        Task<List<Teacher>> SearchTeachersAsync(string searchText, string departmentId);
        Task<Teacher> GetTeacherByIdAsync(string teacherId);
        Task<bool> UpdateTeacherAsync(Teacher teacher);
        Task<bool> AddTeacherAsync(Teacher teacher);
        Task<bool> DeleteTeacherAsync(string teacherId);
        Task<bool> UpdateStudentAsync(Student student);
        Task<List<Student>> GetClassStudentsAsync(string classId);
        Task<bool> CheckSemesterOverlapAsync(DateTime startDate, DateTime endDate);
        Task<bool> CheckSemesterOverlapExceptCurrentAsync(int currentSemesterId, DateTime startDate, DateTime endDate);
        Task<bool> CheckSemesterHasRelatedDataAsync(int semesterId);
        Task<IEnumerable<Semester>> GetSemestersBySearchTermAsync(string searchTerm);
        Task<IEnumerable<Course>> GetCoursesBySemesterAsync(int semesterID);
        Task<IEnumerable<TeacherCourse>> GetTeacherCoursesBySemesterAsync(int semesterID);
    }
}
