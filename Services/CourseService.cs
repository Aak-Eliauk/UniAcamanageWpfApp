using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using UniAcamanageWpfApp.Models;

namespace UniAcamanageWpfApp.Services
{
    public interface ICourseService
    {
        Task<List<Semester>> GetCurrentSemestersAsync();
        Task<(List<Course> Basic, List<Course> Major, List<Course> Elective)> GetRecommendedCoursesAsync(int semesterId);
        Task<bool> SubmitCourseSelectionsAsync(int semesterId, List<string> courseCodes);
        Task<List<Course>> GetAvailableCoursesAsync(int semesterId, string courseType);
        Task<bool> AddCourseSelectionAsync(string studentId, int courseId);
        Task<bool> RemoveCourseSelectionAsync(string studentId, int courseId);
        Task<List<SystemNotification>> GetSystemNotificationsAsync(string studentId);
        Task<bool> MarkNotificationAsReadAsync(int notificationId);
        Task<List<Course>> GetOptimizedCourseSelectionAsync(string studentId, int semesterId);
        Task<List<Course>> GetSelectedCoursesAsync(string studentId, int semesterId);
    }

    public class CourseService : ICourseService
    {
        private readonly string _connectionString;

        public CourseService()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
        }

        // 添加选课
        public async Task<bool> AddCourseSelectionAsync(string studentId, int courseId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = @"
                INSERT INTO StudentCourse (StudentID, CourseID, SelectionDate)
                VALUES (@StudentID, @CourseID, @SelectionDate)";

                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@StudentID", studentId);
                    command.Parameters.AddWithValue("@CourseID", courseId);
                    command.Parameters.AddWithValue("@SelectionDate", DateTime.Now);

                    await connection.OpenAsync();
                    var result = await command.ExecuteNonQueryAsync();
                    return result > 0;
                }
            }
        }

        // 移除选课
        public async Task<bool> RemoveCourseSelectionAsync(string studentId, int courseId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = @"
                DELETE FROM StudentCourse 
                WHERE StudentID = @StudentID AND CourseID = @CourseID";

                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@StudentID", studentId);
                    command.Parameters.AddWithValue("@CourseID", courseId);

                    await connection.OpenAsync();
                    var result = await command.ExecuteNonQueryAsync();
                    return result > 0;
                }
            }
        }

        // 获取已选课程
        public async Task<List<Course>> GetSelectedCoursesAsync(string studentId, int semesterId)
        {
            var courses = new List<Course>();
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = @"
        SELECT 
            c.CourseID,
            c.CourseCode,
            c.CourseName,
            c.CourseType,
            c.Credit,
            c.ScheduleTime,
            ISNULL(cr.RoomNumber, '未分配') as Classroom,
            ISNULL(t.Name, '') as TeacherName,
            CONCAT(
                (SELECT COUNT(*) FROM StudentCourse WHERE CourseID = c.CourseID),
                '/',
                c.Capacity
            ) as Capacity,
            c.Description,
            sc.SelectionType as Status
        FROM Course c
        INNER JOIN StudentCourse sc ON c.CourseID = sc.CourseID
        LEFT JOIN Classroom cr ON c.ClassroomID = cr.ClassroomID
        LEFT JOIN TeacherCourse tc ON c.CourseID = tc.CourseID
        LEFT JOIN Teacher t ON tc.TeacherID = t.TeacherID
        WHERE sc.StudentID = @StudentID 
        AND c.SemesterID = @SemesterID";

                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@StudentID", studentId);
                    command.Parameters.AddWithValue("@SemesterID", semesterId);

                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            courses.Add(new Course
                            {
                                CourseID = reader.GetInt32(reader.GetOrdinal("CourseID")),
                                CourseCode = reader.GetString(reader.GetOrdinal("CourseCode")),
                                CourseName = reader.GetString(reader.GetOrdinal("CourseName")),
                                CourseType = reader.GetString(reader.GetOrdinal("CourseType")),
                                Credit = reader.GetDecimal(reader.GetOrdinal("Credit")),
                                ScheduleTime = reader.GetString(reader.GetOrdinal("ScheduleTime")),
                                Classroom = reader.GetString(reader.GetOrdinal("Classroom")),
                                TeacherName = reader.GetString(reader.GetOrdinal("TeacherName")),
                                Capacity = reader.GetString(reader.GetOrdinal("Capacity")),
                                Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
                                Status = reader.GetString(reader.GetOrdinal("Status"))
                            });
                        }
                    }
                }
            }
            return courses;
        }

        // 获取系统通知
        public async Task<List<SystemNotification>> GetSystemNotificationsAsync(string studentId)
        {
            var notifications = new List<SystemNotification>();
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = @"
                SELECT * FROM SystemNotifications 
                WHERE (StudentID = @StudentID OR StudentID IS NULL)
                AND CreatedTime >= DATEADD(day, -7, GETDATE())
                ORDER BY CreatedTime DESC";

                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@StudentID", studentId);

                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            notifications.Add(new SystemNotification
                            {
                                NotificationId = reader.GetInt32(reader.GetOrdinal("NotificationID")),
                                Title = reader.GetString(reader.GetOrdinal("Title")),
                                Message = reader.GetString(reader.GetOrdinal("Message")),
                                CreatedTime = reader.GetDateTime(reader.GetOrdinal("CreatedTime")),
                                IsRead = reader.GetBoolean(reader.GetOrdinal("IsRead")),
                                NotificationType = reader.GetString(reader.GetOrdinal("NotificationType"))
                            });
                        }
                    }
                }
            }
            return notifications;
        }

        // 标记通知为已读
        public async Task<bool> MarkNotificationAsReadAsync(int notificationId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = @"
                UPDATE SystemNotifications 
                SET IsRead = 1 
                WHERE NotificationID = @NotificationID";

                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@NotificationID", notificationId);

                    await connection.OpenAsync();
                    var result = await command.ExecuteNonQueryAsync();
                    return result > 0;
                }
            }
        }

        // 获取优化的课程选择建议
        public async Task<List<Course>> GetOptimizedCourseSelectionAsync(string studentId, int semesterId)
        {
            var courses = new List<Course>();
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = @"
                -- 这里实现课程优化逻辑
                -- 可以基于以下因素：
                -- 1. 必修课优先
                -- 2. 学分要求
                -- 3. 时间冲突检查
                -- 4. 课程容量
                -- 示例查询：
                SELECT c.*, ISNULL(t.Name, '') as TeacherName
                FROM Course c
                LEFT JOIN TeacherCourse tc ON c.CourseID = tc.CourseID
                LEFT JOIN Teacher t ON tc.TeacherID = t.TeacherID
                WHERE c.SemesterID = @SemesterID
                AND c.CourseID NOT IN (
                    SELECT CourseID FROM StudentCourse WHERE StudentID = @StudentID
                )
                AND (
                    c.CourseType = 'Required' 
                    OR (c.CourseType = 'Elective' AND c.Credit <= 4)
                )
                ORDER BY 
                    CASE c.CourseType 
                        WHEN 'Required' THEN 1 
                        ELSE 2 
                    END,
                    c.Credit DESC";

                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@StudentID", studentId);
                    command.Parameters.AddWithValue("@SemesterID", semesterId);

                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            courses.Add(new Course
                            {
                                CourseID = reader.GetInt32(reader.GetOrdinal("CourseID")),
                                CourseCode = reader.GetString(reader.GetOrdinal("CourseCode")),
                                CourseName = reader.GetString(reader.GetOrdinal("CourseName")),
                                CourseType = reader.GetString(reader.GetOrdinal("CourseType")),
                                Credit = reader.GetDecimal(reader.GetOrdinal("Credit")),
                                ScheduleTime = reader.GetString(reader.GetOrdinal("ScheduleTime")),
                                Classroom = reader.GetString(reader.GetOrdinal("Classroom")),
                                TeacherName = reader.GetString(reader.GetOrdinal("TeacherName")),
                                Capacity = reader.GetString(reader.GetOrdinal("Capacity"))
                            });
                        }
                    }
                }
            }
            return courses;
        }

        // 在 CourseService 中修改 GetAvailableCoursesAsync 方法
        public async Task<List<Course>> GetAvailableCoursesAsync(int semesterId, string courseType = null)
        {
            var courses = new List<Course>();
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = @"
            SELECT 
                c.CourseID,
                c.CourseCode,
                c.CourseName,
                c.CourseType,
                c.Credit,
                c.ScheduleTime,
                c.Capacity,
                ISNULL(cr.RoomNumber, '未分配') as Classroom,
                ISNULL(t.Name, '') as TeacherName,
                CONCAT(
                    (SELECT COUNT(*) FROM StudentCourse WHERE CourseID = c.CourseID),
                    '/',
                    c.Capacity
                ) as CurrentCapacity,
                c.Description
            FROM Course c
            LEFT JOIN Classroom cr ON c.ClassroomID = cr.ClassroomID
            LEFT JOIN TeacherCourse tc ON c.CourseID = tc.CourseID
            LEFT JOIN Teacher t ON tc.TeacherID = t.TeacherID
            WHERE c.SemesterID = @SemesterID
            AND (@CourseType IS NULL OR c.CourseType = @CourseType)";

                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@SemesterID", semesterId);
                    command.Parameters.AddWithValue("@CourseType", courseType ?? (object)DBNull.Value);

                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            courses.Add(new Course
                            {
                                CourseID = reader.GetInt32(reader.GetOrdinal("CourseID")),
                                CourseCode = reader.GetString(reader.GetOrdinal("CourseCode")),
                                CourseName = reader.GetString(reader.GetOrdinal("CourseName")),
                                CourseType = reader.GetString(reader.GetOrdinal("CourseType")),
                                Credit = reader.GetDecimal(reader.GetOrdinal("Credit")),
                                ScheduleTime = reader.GetString(reader.GetOrdinal("ScheduleTime")),
                                Classroom = reader.GetString(reader.GetOrdinal("Classroom")),
                                TeacherName = reader.GetString(reader.GetOrdinal("TeacherName")),
                                Capacity = reader.GetString(reader.GetOrdinal("CurrentCapacity")),
                                Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description"))
                            });
                        }
                    }
                }
            }
            return courses;
        }

        public async Task<List<Semester>> GetCurrentSemestersAsync()
        {
            var semesters = new List<Semester>();
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = @"
        SELECT s.SemesterID, 
               s.SemesterName, 
               s.StartDate, 
               s.EndDate,
               ay.YearName as AcademicYearName
        FROM Semester s
        INNER JOIN AcademicYear ay ON s.AcademicYearID = ay.AcademicYearID
        WHERE s.StartDate <= DATEADD(MONTH, 1, GETDATE())
        AND s.EndDate >= DATEADD(MONTH, -1, GETDATE())
        ORDER BY s.StartDate DESC";

                using (var command = new SqlCommand(sql, connection))
                {
                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            semesters.Add(new Semester
                            {
                                SemesterId = reader.GetInt32(0),
                                SemesterName = reader.GetString(1),
                                StartDate = reader.GetDateTime(2),
                                EndDate = reader.GetDateTime(3),
                                AcademicYearName = reader.GetString(4)
                            });
                        }
                    }
                }
            }
            return semesters;
        }

        public async Task<(List<Course> Basic, List<Course> Major, List<Course> Elective)>
            GetRecommendedCoursesAsync(int semesterId)
        {
            var basicCourses = new List<Course>();
            var majorCourses = new List<Course>();
            var electiveCourses = new List<Course>();

            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = @"
                    SELECT 
                        c.CourseID,
                        c.CourseCode,
                        c.CourseName,
                        c.CourseType,
                        c.Credit,
                        c.ScheduleTime,
                        cr.RoomNumber as Classroom,
                        t.Name as TeacherName,
                        CONCAT(
                            (SELECT COUNT(*) FROM StudentCourse sc WHERE sc.CourseID = c.CourseID),
                            '/',
                            c.Capacity
                        ) as Capacity,
                        c.Description,
                        CASE WHEN sc.CourseID IS NOT NULL THEN 1 ELSE 0 END as IsSelected
                    FROM Course c
                    INNER JOIN CourseRecommendation rec ON c.CourseID = rec.CourseID
                    LEFT JOIN Classroom cr ON c.ClassroomID = cr.ClassroomID
                    LEFT JOIN TeacherCourse tc ON c.CourseID = tc.CourseID
                    LEFT JOIN Teacher t ON tc.TeacherID = t.TeacherID
                    LEFT JOIN StudentCourse sc ON c.CourseID = sc.CourseID 
                        AND sc.StudentID = @StudentID
                    WHERE rec.SemesterID = @SemesterID
                        AND rec.ClassID = (
                            SELECT ClassID FROM Student WHERE StudentID = @StudentID
                        )
                    ORDER BY rec.Priority DESC";

                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@StudentID", GlobalUserState.LinkedID);
                    command.Parameters.AddWithValue("@SemesterID", semesterId);

                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var course = new Course
                            {
                                CourseID = reader.GetInt32(0),
                                CourseCode = reader.GetString(1),
                                CourseName = reader.GetString(2),
                                CourseType = reader.GetString(3),
                                Credit = reader.GetDecimal(4),
                                ScheduleTime = reader.GetString(5),
                                Classroom = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                                TeacherName = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                                Capacity = reader.GetString(8),
                                Description = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                                IsSelected = reader.GetInt32(10) == 1
                            };

                            switch (course.CourseType)
                            {
                                case "基础必修":
                                    basicCourses.Add(course);
                                    break;
                                case "专业必修":
                                    majorCourses.Add(course);
                                    break;
                                case "选修":
                                    electiveCourses.Add(course);
                                    break;
                            }
                        }
                    }
                }
            }

            return (basicCourses, majorCourses, electiveCourses);
        }

        public async Task<bool> SubmitCourseSelectionsAsync(int semesterId, List<string> courseCodes)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. 删除当前学期的选课记录
                        var deleteSql = @"
                            DELETE FROM StudentCourse 
                            WHERE StudentID = @StudentID 
                            AND CourseID IN (
                                SELECT CourseID FROM Course 
                                WHERE SemesterID = @SemesterID
                            )";

                        using (var command = new SqlCommand(deleteSql, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@StudentID", GlobalUserState.LinkedID);
                            command.Parameters.AddWithValue("@SemesterID", semesterId);
                            await command.ExecuteNonQueryAsync();
                        }

                        // 2. 插入新的选课记录
                        foreach (var courseCode in courseCodes)
                        {
                            var insertSql = @"
                                INSERT INTO StudentCourse (StudentID, CourseID, SelectionType, SelectionDate)
                                SELECT @StudentID, CourseID, '待审核', GETDATE()
                                FROM Course 
                                WHERE CourseCode = @CourseCode
                                AND SemesterID = @SemesterID";

                            using (var command = new SqlCommand(insertSql, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@StudentID", GlobalUserState.LinkedID);
                                command.Parameters.AddWithValue("@CourseCode", courseCode);
                                command.Parameters.AddWithValue("@SemesterID", semesterId);
                                await command.ExecuteNonQueryAsync();
                            }
                        }

                        transaction.Commit();
                        return true;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }
    }
}