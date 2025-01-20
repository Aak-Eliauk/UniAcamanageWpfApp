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
using System.Diagnostics;
using Microsoft.Extensions.Configuration;

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
            // 直接从配置文件读取连接字符串
            _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
        }

        private class GradeResult
        {
            public string Level { get; set; }
            public decimal Point { get; set; }
        }

        #region 辅助方法
        private GradeResult GetGradeInfo(decimal score, bool isRetest)
        {
            if (score == 0) return new GradeResult { Level = "未完成", Point = 0 };

            // 补考及格统一按 D- 1.0 计算
            if (isRetest && score >= 60)
                return new GradeResult { Level = "D-", Point = 1.0m };

            // 不及格统一为 F 0分
            if (score < 60)
                return new GradeResult { Level = "F", Point = 0 };

            // 正常考试的成绩等级判定
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
                "基础必修" => 1.2m,
                "专业必修" => 1.1m,
                "选修" => 1.0m,
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
        public async Task<AcademicStats> GetAcademicStatsAsync(string studentId, string semester = null)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            const string query = @"
        WITH LatestGrades AS (
            -- 获取每门课程的最新成绩
            SELECT 
                c.CourseType,
                c.Credit,
                g.Score,
                g.IsRetest,
                s.SemesterName,
                CASE 
                    WHEN g.IsRetest = 1 AND g.Score >= 60 THEN 1.0  -- 重修及格固定为1.0
                    WHEN g.Score >= 90 THEN 4.0
                    WHEN g.Score >= 85 THEN 3.7
                    WHEN g.Score >= 82 THEN 3.3
                    WHEN g.Score >= 78 THEN 3.0
                    WHEN g.Score >= 75 THEN 2.7
                    WHEN g.Score >= 71 THEN 2.3
                    WHEN g.Score >= 66 THEN 2.0
                    WHEN g.Score >= 62 THEN 1.7
                    WHEN g.Score >= 60 THEN 1.3
                    ELSE 0
                END * 
                CASE 
                    WHEN c.CourseType = '基础必修' THEN 1.2
                    WHEN c.CourseType = '专业必修' THEN 1.1
                    ELSE 1.0
                END as WeightedGradePoint
            FROM Grade g
            JOIN Course c ON g.CourseID = c.CourseID
            JOIN Semester s ON g.SemesterID = s.SemesterID
            INNER JOIN (
                SELECT CourseID, MAX(AttemptNumber) as LastAttempt
                FROM Grade
                WHERE StudentID = @StudentID
                GROUP BY CourseID
            ) latest ON g.CourseID = latest.CourseID 
            AND g.AttemptNumber = latest.LastAttempt
            WHERE g.StudentID = @StudentID
            AND (@Semester IS NULL OR s.SemesterName = @Semester)
        ),
        TypeGPA AS (
            SELECT 
                CourseType,
                SUM(Credit * WeightedGradePoint) / NULLIF(SUM(Credit), 0) as TypeGPA
            FROM LatestGrades
            GROUP BY CourseType
        )
        SELECT 
            (SELECT SUM(Credit * WeightedGradePoint) / NULLIF(SUM(Credit), 0) 
             FROM LatestGrades) as OverallGPA,
            MAX(CASE WHEN CourseType = '基础必修' THEN TypeGPA ELSE 0 END) as BaseRequiredGPA,
            MAX(CASE WHEN CourseType = '专业必修' THEN TypeGPA ELSE 0 END) as MajorRequiredGPA,
            MAX(CASE WHEN CourseType = '选修' THEN TypeGPA ELSE 0 END) as ElectiveGPA
        FROM TypeGPA";

            try
            {
                var result = await connection.QueryFirstOrDefaultAsync<dynamic>(query,
                    new { StudentID = studentId, Semester = semester });

                return new AcademicStats
                {
                    OverallGPA = Math.Round(Convert.ToDecimal(result?.OverallGPA ?? 0), 2),
                    BaseRequiredGPA = Math.Round(Convert.ToDecimal(result?.BaseRequiredGPA ?? 0), 2),
                    MajorRequiredGPA = Math.Round(Convert.ToDecimal(result?.MajorRequiredGPA ?? 0), 2),
                    ElectiveGPA = Math.Round(Convert.ToDecimal(result?.ElectiveGPA ?? 0), 2)
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
            g.Score,
            g.GradeLevel,
            g.BaseGradePoint,
            g.WeightedGradePoint,
            g.IsRetest as IsRetake,
            sc.Remarks,
            g.ModifiedAt,
            g.ModifiedBy,
            g.AttemptNumber
        FROM Grade g
        JOIN Course c ON g.CourseID = c.CourseID
        JOIN Semester s ON g.SemesterID = s.SemesterID
        LEFT JOIN StudentCourse sc ON g.StudentID = sc.StudentID AND g.CourseID = sc.CourseID
        WHERE g.StudentID = @StudentID
        AND (@Semester IS NULL OR s.SemesterName = @Semester)
        -- 只获取每门课程的最新考试成绩
        AND g.AttemptNumber = (
            SELECT MAX(AttemptNumber)
            FROM Grade g2
            WHERE g2.StudentID = g.StudentID
            AND g2.CourseID = g.CourseID
        )
        ORDER BY s.SemesterID DESC, c.CourseCode";

            var grades = await connection.QueryAsync<dynamic>(query,
                new { StudentID = studentId, Semester = semester });

            return grades.Select(g => new GradeInfo
            {
                Semester = g.Semester,
                CourseCode = g.CourseCode,
                CourseName = g.CourseName,
                CourseType = g.CourseType,
                Credit = Convert.ToDecimal(g.Credit),
                Score = g.Score,
                GradeLevel = g.GradeLevel,
                BaseGradePoint = g.BaseGradePoint,
                WeightedGradePoint = g.WeightedGradePoint,
                CourseStatus = GetCourseStatus(g.Score),
                Remarks = g.Remarks,
                IsRetake = g.IsRetake,
                ModifiedAt = g.ModifiedAt,
                ModifiedBy = g.ModifiedBy,
                AttemptNumber = g.AttemptNumber // 添加考试次数字段
            }).ToList();
        }

        private string GetCourseStatus(decimal score)
        {
            if (score >= 60) return "已修完成";
            if (score > 0) return "未通过";
            return "正在修读";
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
            GradeLevel as Grade,
            COUNT(*) * 100.0 / (
                SELECT COUNT(*) 
                FROM Grade 
                WHERE StudentID = @StudentID AND Score IS NOT NULL
            ) as Percentage
        FROM Grade
        WHERE StudentID = @StudentID AND Score IS NOT NULL
        GROUP BY GradeLevel
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
                c.Credit,
                g.Score,
                g.IsRetest,
                CASE 
                    WHEN g.IsRetest = 1 AND g.Score >= 60 THEN 1.0  -- 重修及格固定为1.0
                    WHEN g.Score >= 90 THEN 4.0
                    WHEN g.Score >= 85 THEN 3.7
                    WHEN g.Score >= 82 THEN 3.3
                    WHEN g.Score >= 78 THEN 3.0
                    WHEN g.Score >= 75 THEN 2.7
                    WHEN g.Score >= 71 THEN 2.3
                    WHEN g.Score >= 66 THEN 2.0
                    WHEN g.Score >= 62 THEN 1.7
                    WHEN g.Score >= 60 THEN 1.3
                    ELSE 0
                END * 
                CASE 
                    WHEN c.CourseType = '基础必修' THEN 1.2
                    WHEN c.CourseType = '专业必修' THEN 1.1
                    ELSE 1.0
                END as WeightedGradePoint
            FROM Grade g
            JOIN Course c ON g.CourseID = c.CourseID
            JOIN Semester s ON g.SemesterID = s.SemesterID
            WHERE g.StudentID = @StudentID
            AND g.AttemptNumber = (
                SELECT MAX(AttemptNumber)
                FROM Grade g2
                WHERE g2.StudentID = g.StudentID
                AND g2.CourseID = g.CourseID
            )
        )
        SELECT 
            SemesterID,
            SemesterName,
            CAST(SUM(Credit * WeightedGradePoint) / NULLIF(SUM(Credit), 0) AS float) as SemesterGPA
        FROM SemesterGrades
        GROUP BY SemesterID, SemesterName
        ORDER BY SemesterID";

            try
            {
                var results = await connection.QueryAsync<dynamic>(query, new { StudentID = studentId });

                // 创建一个新的 List<double>，进行显式类型转换
                List<double> gpaList = new List<double>();

                if (results != null)
                {
                    foreach (var result in results)
                    {
                        // 安全地处理可能的 null 值和类型转换
                        if (result.SemesterGPA != null)
                        {
                            double gpa = Convert.ToDouble(result.SemesterGPA);
                            gpaList.Add(gpa);
                        }
                        else
                        {
                            gpaList.Add(0.0); // 或者其他默认值
                        }
                    }
                }

                return gpaList;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"获取学期GPA时发生错误: {ex.Message}");
                return new List<double>();
            }
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

        public async Task<ProgramProgressInfo> GetProgramProgressAsync(string studentId)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new SqlCommand("GetStudentProgramProgress", connection);
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@StudentID", studentId);

                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    try
                    {
                        var progress = new ProgramProgressInfo
                        {
                            CompletedCredits = reader.GetDecimal(reader.GetOrdinal("CompletedCredits")),
                            TotalCredits = reader.GetDecimal(reader.GetOrdinal("TotalCredits")),
                            CompletionPercentage = reader.GetDecimal(reader.GetOrdinal("CompletionPercentage")),

                            BaseRequiredProgress = new ProgressInfo
                            {
                                CompletedCredits = reader.GetDecimal(reader.GetOrdinal("CompletedBaseRequired")),
                                TotalCredits = reader.GetDecimal(reader.GetOrdinal("TotalBaseRequired")),
                                Percentage = CalculatePercentage(
                                    reader.GetDecimal(reader.GetOrdinal("CompletedBaseRequired")),
                                    reader.GetDecimal(reader.GetOrdinal("TotalBaseRequired")))
                            },

                            MajorRequiredProgress = new ProgressInfo
                            {
                                CompletedCredits = reader.GetDecimal(reader.GetOrdinal("CompletedMajorRequired")),
                                TotalCredits = reader.GetDecimal(reader.GetOrdinal("TotalMajorRequired")),
                                Percentage = CalculatePercentage(
                                    reader.GetDecimal(reader.GetOrdinal("CompletedMajorRequired")),
                                    reader.GetDecimal(reader.GetOrdinal("TotalMajorRequired")))
                            },

                            ElectiveProgress = new ProgressInfo
                            {
                                CompletedCredits = reader.GetDecimal(reader.GetOrdinal("CompletedElective")),
                                TotalCredits = reader.GetDecimal(reader.GetOrdinal("TotalElective")),
                                Percentage = CalculatePercentage(
                                    reader.GetDecimal(reader.GetOrdinal("CompletedElective")),
                                    reader.GetDecimal(reader.GetOrdinal("TotalElective")))
                            },

                            CompletedCourses = reader.GetInt32(reader.GetOrdinal("CompletedCourses")),
                            OngoingCourses = reader.GetInt32(reader.GetOrdinal("OngoingCourses")),
                            FailedCourses = reader.GetInt32(reader.GetOrdinal("FailedCourses")),
                            RemainingCourses = reader.GetInt32(reader.GetOrdinal("RemainingCourses"))
                        };

                        return progress;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error parsing data: {ex.Message}");
                        throw new Exception($"Error parsing progress data: {ex.Message}", ex);
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Database error: {ex.Message}");
                throw new Exception($"Error accessing database: {ex.Message}", ex);
            }
        }

        #endregion

        #region 私有辅助方法
        private decimal CalculatePercentage(decimal completed, decimal total)
        {
            if (total == 0) return 0;
            return Math.Round((completed / total) * 100, 2);
        }


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