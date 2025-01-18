using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UniAcamanageWpfApp.Models;

namespace UniAcamanageWpfApp.Services
{
    public interface IAcademicStatusService
    {
        Task<AcademicStats> GetAcademicStatsAsync(string studentId);
        Task<List<GradeInfo>> GetGradesAsync(string studentId, string semester = null);
        Task<List<CourseCompletionInfo>> GetCourseCompletionAsync(string studentId);
        Task<Dictionary<string, double>> GetGradeDistributionAsync(string studentId);
        Task<List<double>> GetSemesterGPAsAsync(string studentId);
        Task<List<string>> GetSemestersAsync(string studentId);
        Task<(string Major, string Grade)> GetStudentInfoAsync(string studentId);
    }
}