// Services/AcademicStatusService.cs
using System;
using System.Data;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Configuration;
using UniAcamanageWpfApp.Models;
using Dapper;

namespace UniAcamanageWpfApp.Services
{
    public class AcademicStatusService : IAcademicStatusService
    {
        private readonly string _connectionString;

        // 课程权重常量
        private const decimal BASE_REQUIRED_WEIGHT = 1.2m;
        private const decimal MAJOR_REQUIRED_WEIGHT = 1.1m;
        private const decimal ELECTIVE_WEIGHT = 1.0m;

        public AcademicStatusService()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
        }

        private class GradeResult
        {
            public string Level { get; set; }
            public decimal Point { get; set; }
        }

        #region 辅助方法

        private GradeResult GetGradeInfo(decimal score, bool isRetake)  // 移除可空类型
        {
            if (score == 0) return new GradeResult { Level = "未完成", Point = 0 };  // 假设0分表示未完成

            if (isRetake && score >= 60) return new GradeResult { Level = "D-", Point = 1.0m };

            return new GradeResult
            {
                Level = score switch
                {
                    >= 90 => "A",
                    >= 85 => "A-",
                    >= 82 => "B+",
                    >= 78 => "B",
                    >= 75 => "B-",
                    >= 71 => "C+",
                    >= 66 => "C",
                    >= 62 => "C-",
                    >= 60 => "D",
                    _ => "F"
                },
                Point = score switch
                {
                    >= 90 => 4.0m,
                    >= 85 => 3.7m,
                    >= 82 => 3.3m,
                    >= 78 => 3.0m,
                    >= 75 => 2.7m,
                    >= 71 => 2.3m,
                    >= 66 => 2.0m,
                    >= 62 => 1.7m,
                    >= 60 => 1.3m,
                    _ => 0
                }
            };
        }

        /// <summary>
        /// 获取课程权重系数
        /// </summary>
        private decimal GetCourseWeight(string courseType)
        {
            return courseType switch
            {
                "基础必修" => BASE_REQUIRED_WEIGHT,
                "专业必修" => MAJOR_REQUIRED_WEIGHT,
                "选修" => ELECTIVE_WEIGHT,
                _ => 1.0m
            };
        }

        /// <summary>
        /// 计算加权后的绩点
        /// </summary>
        private decimal CalculateWeightedGradePoint(decimal basePoint, string courseType)
        {
            decimal weightCoefficient = courseType?.ToUpper() switch
            {
                "基础必修" => 1.2m,
                "专业必修" => 1.1m,
                "选修" => 1.0m,
                _ => 1.0m
            };

            return Math.Round(basePoint * weightCoefficient, 2);
        }
        public async Task<AcademicStats> GetAcademicStatsAsync(string studentId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            const string query = @"
        WITH CourseGrades AS (
            SELECT 
                c.CourseType,
                c.Credit,
                sc.Score,
                CASE WHEN EXISTS (
                    SELECT 1 FROM StudentCourse sc2 
                    WHERE sc2.StudentID = sc.StudentID 
                    AND sc2.CourseID = sc.CourseID 
                    AND sc2.Id < sc.Id
                ) THEN 1 ELSE 0 END as IsRetake
            FROM StudentCourse sc
            JOIN Course c ON sc.CourseID = c.CourseID
            WHERE sc.StudentID = @StudentID AND sc.Score IS NOT NULL
        ),
        TypeGPA AS (
            SELECT 
                CourseType,
                CASE 
                    WHEN SUM(Credit) = 0 THEN 0
                    ELSE SUM(Credit * CASE
                        WHEN IsRetake = 1 AND Score >= 60 THEN 1.0
                        WHEN Score >= 90 THEN 4.0
                        WHEN Score >= 85 THEN 3.7
                        WHEN Score >= 82 THEN 3.3
                        WHEN Score >= 78 THEN 3.0
                        WHEN Score >= 75 THEN 2.7
                        WHEN Score >= 71 THEN 2.3
                        WHEN Score >= 66 THEN 2.0
                        WHEN Score >= 62 THEN 1.7
                        WHEN Score >= 60 THEN 1.3
                        ELSE 0
                    END * CASE 
                        WHEN CourseType = '基础必修' THEN 1.2
                        WHEN CourseType = '专业必修' THEN 1.1
                        ELSE 1.0
                    END) / SUM(Credit)
                END as TypeGPA
            FROM CourseGrades
            GROUP BY CourseType
        ),
        OverallGPA AS (
            SELECT 
                CASE 
                    WHEN SUM(Credit) = 0 THEN 0
                    ELSE SUM(Credit * CASE
                        WHEN IsRetake = 1 AND Score >= 60 THEN 1.0
                        WHEN Score >= 90 THEN 4.0
                        WHEN Score >= 85 THEN 3.7
                        WHEN Score >= 82 THEN 3.3
                        WHEN Score >= 78 THEN 3.0
                        WHEN Score >= 75 THEN 2.7
                        WHEN Score >= 71 THEN 2.3
                        WHEN Score >= 66 THEN 2.0
                        WHEN Score >= 62 THEN 1.7
                        WHEN Score >= 60 THEN 1.3
                        ELSE 0
                    END * CASE 
                        WHEN CourseType = '基础必修' THEN 1.2
                        WHEN CourseType = '专业必修' THEN 1.1
                        ELSE 1.0
                    END) / SUM(Credit)
                END as OverallGPA
            FROM CourseGrades
        ),
        ClassRank AS (
            SELECT 
                s.ClassID,
                sc.StudentID,
                AVG(CAST(sc.Score AS DECIMAL(5,2))) as AvgScore
            FROM StudentCourse sc
            JOIN Student s ON sc.StudentID = s.StudentID
            WHERE s.ClassID = (SELECT ClassID FROM Student WHERE StudentID = @StudentID)
            GROUP BY s.ClassID, sc.StudentID
        )
        SELECT 
            (SELECT OverallGPA FROM OverallGPA) as OverallGPA,
            MAX(CASE WHEN g.CourseType = '基础必修' THEN g.TypeGPA ELSE 0 END) as BaseRequiredGPA,
            MAX(CASE WHEN g.CourseType = '专业必修' THEN g.TypeGPA ELSE 0 END) as MajorRequiredGPA,
            MAX(CASE WHEN g.CourseType = '选修' THEN g.TypeGPA ELSE 0 END) as ElectiveGPA,
            (SELECT COUNT(*) + 1 
             FROM ClassRank r2 
             WHERE r2.AvgScore > (SELECT AvgScore FROM ClassRank WHERE StudentID = @StudentID)
            ) as ClassRanking
        FROM TypeGPA g";

            try
            {
                var result = await connection.QueryFirstOrDefaultAsync<dynamic>(query, new { StudentID = studentId });

                return new AcademicStats
                {
                    OverallGPA = Convert.ToDecimal(result?.OverallGPA ?? 0),
                    BaseRequiredGPA = Convert.ToDecimal(result?.BaseRequiredGPA ?? 0),
                    MajorRequiredGPA = Convert.ToDecimal(result?.MajorRequiredGPA ?? 0),
                    ElectiveGPA = Convert.ToDecimal(result?.ElectiveGPA ?? 0),
                    ClassRanking = Convert.ToInt32(result?.ClassRanking ?? 0),
                    TotalCredits = 0, // 这些值会在后续更新
                    CompletedCredits = 0,
                    RemainingCredits = 0,
                    CompletedCourses = 0,
                    OngoingCourses = 0,
                    FailedCourses = 0
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"获取学业统计信息时出错: {ex.Message}", ex);
            }
        }

        public async Task<List<GradeInfo>> GetGradesAsync(string studentId, string semester = null)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"
        SELECT 
            s.SemesterName as Semester,
            c.CourseCode,
            c.CourseName,
            c.CourseType,
            c.Credit,
            COALESCE(sc.Score, 0) as Score,  -- 使用 COALESCE 处理 NULL
            CASE WHEN EXISTS (
                SELECT 1 FROM StudentCourse sc2 
                WHERE sc2.StudentID = sc.StudentID 
                AND sc2.CourseID = sc.CourseID 
                AND sc2.Id < sc.Id
            ) THEN 1 ELSE 0 END as IsRetake,
            sc.Remarks
        FROM StudentCourse sc
        JOIN Course c ON sc.CourseID = c.CourseID
        JOIN Semester s ON c.SemesterID = s.SemesterID
        WHERE sc.StudentID = @StudentID";

            if (!string.IsNullOrEmpty(semester))
            {
                query += " AND s.SemesterName = @Semester";
            }

            query += " ORDER BY s.SemesterID DESC, c.CourseCode";

            var grades = await connection.QueryAsync<dynamic>(query,
                new { StudentID = studentId, Semester = semester });

            return grades.Select(g =>
            {
                decimal score = g.Score == null ? 0 : Convert.ToDecimal(g.Score);
                bool isRetake = Convert.ToBoolean(g.IsRetake);
                var gradeInfo = GetGradeInfo(score, isRetake);
                var weightedPoint = CalculateWeightedGradePoint(gradeInfo.Point, g.CourseType);

                return new GradeInfo
                {
                    Semester = g.Semester,
                    CourseCode = g.CourseCode,
                    CourseName = g.CourseName,
                    CourseType = g.CourseType,
                    Credit = Convert.ToDecimal(g.Credit),
                    Score = score,
                    GradeLevel = gradeInfo.Level,
                    BaseGradePoint = gradeInfo.Point,
                    WeightedGradePoint = weightedPoint,
                    CourseStatus = score > 0 ?
                        (score >= 60 ? "已修完成" : "未通过") :
                        "正在修读",
                    Remarks = g.Remarks,
                    IsRetake = isRetake
                };
            }).ToList();
        }

        public async Task<List<CourseCompletionInfo>> GetCourseCompletionAsync(string studentId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"
        WITH LatestScores AS (
            SELECT 
                CourseID,
                Score,
                ROW_NUMBER() OVER (PARTITION BY CourseID ORDER BY SelectionDate DESC) as rn
            FROM StudentCourse
            WHERE StudentID = @StudentId
        )
        SELECT 
            c.CourseID,
            c.CourseCode,
            c.CourseName,
            c.CourseType,
            c.Credit,
            s.SemesterName as Semester,
            COALESCE(ls.Score, 0) as Score,
            CASE 
                WHEN ls.Score >= 60 THEN '已修完成'
                WHEN ls.Score IS NULL OR ls.Score = 0 THEN '未修'
                ELSE '未通过'
            END as Status
        FROM Course c
        LEFT JOIN LatestScores ls ON c.CourseID = ls.CourseID AND ls.rn = 1
        LEFT JOIN Semester s ON c.SemesterID = s.SemesterID
        ORDER BY c.CourseType, c.CourseCode";

            var courses = await connection.QueryAsync<CourseCompletionInfo>(query, new { StudentId = studentId });
            return courses.ToList();
        }

        public async Task<Dictionary<string, double>> GetGradeDistributionAsync(string studentId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT 
                    CASE 
                        WHEN Score >= 90 THEN 'A (90-100)'
                        WHEN Score >= 85 THEN 'A- (85-89)'
                        WHEN Score >= 82 THEN 'B+ (82-84)'
                        WHEN Score >= 78 THEN 'B (78-81)'
                        WHEN Score >= 75 THEN 'B- (75-77)'
                        WHEN Score >= 71 THEN 'C+ (71-74)'
                        WHEN Score >= 66 THEN 'C (66-70)'
                        WHEN Score >= 62 THEN 'C- (62-65)'
                        WHEN Score >= 60 THEN 'D (60-61)'
                        ELSE 'F (<60)'
                    END as Grade,
                    COUNT(*) * 100.0 / (
                        SELECT COUNT(*) 
                        FROM StudentCourse 
                        WHERE StudentID = @StudentID AND Score IS NOT NULL
                    ) as Percentage
                FROM StudentCourse
                WHERE StudentID = @StudentID AND Score IS NOT NULL
                GROUP BY 
                    CASE 
                        WHEN Score >= 90 THEN 'A (90-100)'
                        WHEN Score >= 85 THEN 'A- (85-89)'
                        WHEN Score >= 82 THEN 'B+ (82-84)'
                        WHEN Score >= 78 THEN 'B (78-81)'
                        WHEN Score >= 75 THEN 'B- (75-77)'
                        WHEN Score >= 71 THEN 'C+ (71-74)'
                        WHEN Score >= 66 THEN 'C (66-70)'
                        WHEN Score >= 62 THEN 'C- (62-65)'
                        WHEN Score >= 60 THEN 'D (60-61)'
                        ELSE 'F (<60)'
                    END
                ORDER BY 
                    MIN(Score) DESC";

            var distribution = await connection.QueryAsync<dynamic>(query,
                new { StudentId = studentId });
            return distribution.ToDictionary(
                x => (string)x.Grade,
                x => (double)x.Percentage
            );
        }

        public async Task<List<double>> GetSemesterGPAsAsync(string studentId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"
        WITH SemesterGrades AS (
            SELECT 
                s.SemesterID,
                s.SemesterName,
                c.CourseType,
                c.Credit,
                CAST(sc.Score as decimal(5,2)) as Score,
                CASE WHEN EXISTS (
                    SELECT 1 FROM StudentCourse sc2 
                    WHERE sc2.StudentID = sc.StudentID 
                    AND sc2.CourseID = sc.CourseID 
                    AND sc2.Id < sc.Id
                ) THEN 1 ELSE 0 END as IsRetake
            FROM StudentCourse sc
            JOIN Course c ON sc.CourseID = c.CourseID
            JOIN Semester s ON c.SemesterID = s.SemesterID
            WHERE sc.StudentID = @StudentID AND sc.Score IS NOT NULL
        )
        SELECT 
            SemesterID,
            SemesterName,
            CAST(
                CASE 
                    WHEN SUM(Credit) = 0 THEN 0
                    ELSE SUM(Credit * CASE
                        WHEN IsRetake = 1 AND Score >= 60 THEN 1.0
                        WHEN Score >= 90 THEN 4.0
                        WHEN Score >= 85 THEN 3.7
                        WHEN Score >= 82 THEN 3.3
                        WHEN Score >= 78 THEN 3.0
                        WHEN Score >= 75 THEN 2.7
                        WHEN Score >= 71 THEN 2.3
                        WHEN Score >= 66 THEN 2.0
                        WHEN Score >= 62 THEN 1.7
                        WHEN Score >= 60 THEN 1.3
                        ELSE 0
                    END * CASE 
                        WHEN CourseType = '基础必修' THEN 1.2
                        WHEN CourseType = '专业必修' THEN 1.1
                        ELSE 1.0
                    END) / SUM(Credit)
                END AS float
            ) as SemesterGPA
        FROM SemesterGrades
        GROUP BY SemesterID, SemesterName
        ORDER BY SemesterID";

            var results = await connection.QueryAsync<dynamic>(query, new { StudentID = studentId });

            // 创建新的List<double>并添加结果
            var gpaList = new List<double>();
            if (results != null)
            {
                foreach (var result in results)
                {
                    double gpa = Convert.ToDouble(result.SemesterGPA);
                    gpaList.Add(gpa);
                }
            }

            return gpaList;
        }

        public async Task<List<string>> GetSemestersAsync(string studentId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT DISTINCT s.SemesterName
                FROM StudentCourse sc
                JOIN Course c ON sc.CourseID = c.CourseID
                JOIN Semester s ON c.SemesterID = s.SemesterID
                WHERE sc.StudentID = @StudentID
                ORDER BY s.SemesterName DESC";

            var semesters = await connection.QueryAsync<string>(query, new { StudentId = studentId });
            return semesters.ToList();
        }

        public async Task<(string Major, string Grade)> GetStudentInfoAsync(string studentId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT 
                    Major,
                    CAST(YEAR(GETDATE()) - YearOfAdmission + 1 AS VARCHAR) + '年级' as Grade
                FROM Student
                WHERE StudentID = @StudentID";

            var result = await connection.QueryFirstAsync<dynamic>(query, new { StudentId = studentId });
            return ((string)result.Major, (string)result.Grade);
        }

        #endregion

        #region 私有辅助方法

        private decimal CalculateGPA(IEnumerable<dynamic> grades, string courseType)
        {
            if (grades == null || !grades.Any()) return 0;

            decimal totalWeightedPoints = 0;
            decimal totalCredits = 0;

            foreach (var grade in grades)
            {
                try
                {
                    decimal credit = Convert.ToDecimal(grade.Credit);
                    decimal score = grade.Score == null ? 0 : Convert.ToDecimal(grade.Score);
                    bool isRetake = Convert.ToBoolean(grade.IsRetake);

                    if (score > 0)  // 只计算有成绩的课程
                    {
                        var gradeInfo = GetGradeInfo(score, isRetake);
                        var weightedPoint = CalculateWeightedGradePoint(gradeInfo.Point, courseType);

                        totalWeightedPoints += weightedPoint * credit;
                        totalCredits += credit;
                    }
                }
                catch
                {
                    continue;
                }
            }

            return totalCredits > 0 ? Math.Round(totalWeightedPoints / totalCredits, 2) : 0;
        }


        private decimal CalculateOverallGPA(IEnumerable<dynamic> allGrades)
        {
            decimal totalWeightedPoints = 0;
            decimal totalCredits = 0;

            foreach (var grade in allGrades)
            {
                try
                {
                    var credit = Convert.ToDecimal(grade.Credit);
                    var score = grade.Score == null ? (double?)null : Convert.ToDouble(grade.Score);
                    var isRetake = Convert.ToBoolean(grade.IsRetake);
                    var courseType = (string)grade.CourseType;

                    var gradeInfo = GetGradeInfo(score, isRetake);
                    var weightedPoint = CalculateWeightedGradePoint(gradeInfo.Point, courseType);

                    totalWeightedPoints += weightedPoint * credit;
                    totalCredits += credit;
                }
                catch
                {
                    continue; // 跳过处理出错的成绩
                }
            }

            return totalCredits > 0 ? Math.Round(totalWeightedPoints / totalCredits, 2) : 0;
        }

        #endregion
    }
}