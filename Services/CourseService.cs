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
        
        Task<List<Course>> GetAvailableCoursesAsync(int semesterId, string courseType, string timeSlot);
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
                await connection.OpenAsync();
                using var transaction = await connection.BeginTransactionAsync() as SqlTransaction;

                try
                {
                    // 1. 获取课程所属学期
                    var getSemesterSql = "SELECT SemesterID FROM Course WHERE CourseID = @CourseID";
                    int semesterId;

                    using (var command = new SqlCommand(getSemesterSql, connection, transaction))
                    {
                        command.Parameters.AddWithValue("@CourseID", courseId);
                        semesterId = (int)await command.ExecuteScalarAsync();

                        // 2. 检查时间冲突
                        if (await HasTimeConflict(connection, transaction, studentId, courseId, semesterId))
                        {
                            throw new InvalidOperationException("所选课程与已选课程时间冲突");
                        }

                        // 3. 检查课程容量
                        var checkCapacitySql = @"
                    SELECT 
                        CASE 
                            WHEN (SELECT COUNT(*) FROM StudentCourse WHERE CourseID = @CourseID) < c.Capacity 
                            THEN 1 
                            ELSE 0 
                        END
                    FROM Course c
                    WHERE c.CourseID = @CourseID";

                        using (var capacityCmd = new SqlCommand(checkCapacitySql, connection, transaction))
                        {
                            capacityCmd.Parameters.AddWithValue("@CourseID", courseId);
                            var hasCapacity = (int)await capacityCmd.ExecuteScalarAsync() == 1;
                            if (!hasCapacity)
                            {
                                throw new InvalidOperationException("课程已达到人数上限");
                            }
                        }

                        // 4. 添加选课记录
                        var insertSql = @"
                    INSERT INTO StudentCourse (StudentID, CourseID, SelectionType, SelectionDate)
                    VALUES (@StudentID, @CourseID, '待审核', @SelectionDate)";

                        using (var insertCmd = new SqlCommand(insertSql, connection, transaction))
                        {
                            insertCmd.Parameters.AddWithValue("@StudentID", studentId);
                            insertCmd.Parameters.AddWithValue("@CourseID", courseId);
                            insertCmd.Parameters.AddWithValue("@SelectionDate", DateTime.Now);
                            await insertCmd.ExecuteNonQueryAsync();
                        }
                    }

                    await transaction.CommitAsync();
                    return true;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }

        private async Task<bool> HasTimeConflict(SqlConnection connection, SqlTransaction transaction, string studentId, int courseId, int semesterId)
        {
            var sql = @"
    WITH ParsedNewCourse AS (
        SELECT 
            value AS TimeSlot,
            CAST(SUBSTRING(value, 1, CHARINDEX('-', value) - 1) AS INT) as WeekDay,
            CAST(SUBSTRING(value, 
                CHARINDEX('-', value) + 1, 
                CHARINDEX('-', value, CHARINDEX('-', value) + 1) - 
                CHARINDEX('-', value) - 1) AS INT) as StartSection,
            CAST(SUBSTRING(value,
                CHARINDEX('-', value, CHARINDEX('-', value) + 1) + 1,
                CHARINDEX('-', value, CHARINDEX('-', value, 
                    CHARINDEX('-', value) + 1) + 1) - 
                CHARINDEX('-', value, CHARINDEX('-', value) + 1) - 1) AS INT) as EndSection,
            CAST(SUBSTRING(value,
                CHARINDEX('-', value, CHARINDEX('-', value, 
                    CHARINDEX('-', value) + 1) + 1) + 1,
                CHARINDEX('-', value, CHARINDEX('-', value, CHARINDEX('-', value, 
                    CHARINDEX('-', value) + 1) + 1) + 1) - 
                CHARINDEX('-', value, CHARINDEX('-', value, 
                    CHARINDEX('-', value) + 1) + 1) - 1) AS INT) as StartWeek,
            CAST(SUBSTRING(value,
                CHARINDEX('-', value, CHARINDEX('-', value, CHARINDEX('-', value, 
                    CHARINDEX('-', value) + 1) + 1) + 1) + 1,
                CASE 
                    WHEN CHARINDEX('A', value) > 0 OR CHARINDEX('B', value) > 0 
                    THEN CHARINDEX('-', value + '-', 
                         LAST_VALUE(CHARINDEX('-', value)) OVER (ORDER BY (SELECT NULL))) - 
                         LAST_VALUE(CHARINDEX('-', value)) OVER (ORDER BY (SELECT NULL)) - 1
                    ELSE LEN(value) - 
                         LAST_VALUE(CHARINDEX('-', value)) OVER (ORDER BY (SELECT NULL))
                END) AS INT) as EndWeek,
            CASE 
                WHEN CHARINDEX('A', value) > 0 THEN 'A'
                WHEN CHARINDEX('B', value) > 0 THEN 'B'
                ELSE NULL 
            END as WeekType
        FROM Course c
        CROSS APPLY STRING_SPLIT(c.ScheduleTime, ',') st
        WHERE c.CourseID = @CourseID
    ),
    ParsedExistingCourses AS (
        SELECT 
            value AS TimeSlot,
            CAST(SUBSTRING(value, 1, CHARINDEX('-', value) - 1) AS INT) as WeekDay,
            CAST(SUBSTRING(value, 
                CHARINDEX('-', value) + 1, 
                CHARINDEX('-', value, CHARINDEX('-', value) + 1) - 
                CHARINDEX('-', value) - 1) AS INT) as StartSection,
            CAST(SUBSTRING(value,
                CHARINDEX('-', value, CHARINDEX('-', value) + 1) + 1,
                CHARINDEX('-', value, CHARINDEX('-', value, 
                    CHARINDEX('-', value) + 1) + 1) - 
                CHARINDEX('-', value, CHARINDEX('-', value) + 1) - 1) AS INT) as EndSection,
            CAST(SUBSTRING(value,
                CHARINDEX('-', value, CHARINDEX('-', value, 
                    CHARINDEX('-', value) + 1) + 1) + 1,
                CHARINDEX('-', value, CHARINDEX('-', value, CHARINDEX('-', value, 
                    CHARINDEX('-', value) + 1) + 1) + 1) - 
                CHARINDEX('-', value, CHARINDEX('-', value, 
                    CHARINDEX('-', value) + 1) + 1) - 1) AS INT) as StartWeek,
            CAST(SUBSTRING(value,
                CHARINDEX('-', value, CHARINDEX('-', value, CHARINDEX('-', value, 
                    CHARINDEX('-', value) + 1) + 1) + 1) + 1,
                CASE 
                    WHEN CHARINDEX('A', value) > 0 OR CHARINDEX('B', value) > 0 
                    THEN CHARINDEX('-', value + '-', 
                         LAST_VALUE(CHARINDEX('-', value)) OVER (ORDER BY (SELECT NULL))) - 
                         LAST_VALUE(CHARINDEX('-', value)) OVER (ORDER BY (SELECT NULL)) - 1
                    ELSE LEN(value) - 
                         LAST_VALUE(CHARINDEX('-', value)) OVER (ORDER BY (SELECT NULL))
                END) AS INT) as EndWeek,
            CASE 
                WHEN CHARINDEX('A', value) > 0 THEN 'A'
                WHEN CHARINDEX('B', value) > 0 THEN 'B'
                ELSE NULL 
            END as WeekType
        FROM Course c
        INNER JOIN StudentCourse sc ON c.CourseID = sc.CourseID
        CROSS APPLY STRING_SPLIT(c.ScheduleTime, ',') st
        WHERE sc.StudentID = @StudentID
        AND c.SemesterID = @SemesterID
    )
    SELECT 1
    FROM ParsedNewCourse n
    CROSS JOIN ParsedExistingCourses e
    WHERE 
        n.WeekDay = e.WeekDay
        AND n.StartSection <= e.EndSection 
        AND e.StartSection <= n.EndSection
        AND n.StartWeek <= e.EndWeek
        AND e.StartWeek <= n.EndWeek
        AND (
            (n.WeekType IS NULL AND e.WeekType IS NULL)
            OR (n.WeekType IS NULL AND e.WeekType IS NOT NULL)
            OR (n.WeekType IS NOT NULL AND e.WeekType IS NULL)
            OR (n.WeekType = e.WeekType)
        )";

            using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@StudentID", studentId);
            command.Parameters.AddWithValue("@CourseID", courseId);
            command.Parameters.AddWithValue("@SemesterID", semesterId);

            var result = await command.ExecuteScalarAsync();
            return result != null;
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
            sc.SelectionType as ApprovalStatus,
            sc.RejectReason
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
                                ApprovalStatus = reader.GetString(reader.GetOrdinal("ApprovalStatus")),
                                RejectReason = reader.IsDBNull(reader.GetOrdinal("RejectReason")) ? null : reader.GetString(reader.GetOrdinal("RejectReason")),
                                IsSelected = true // Mark the course as selected
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

        public async Task<List<Course>> GetAvailableCoursesAsync(int semesterId, string courseType = null, string timeSlot = null)
        {
            var courses = new List<Course>();
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = @"
            WITH SplitSchedule AS (
                SELECT 
                    c.CourseID,
                    c.CourseCode,
                    c.CourseName,
                    c.CourseType,
                    c.Credit,
                    c.ScheduleTime,
                    c.Capacity,
                    ISNULL(cr.RoomNumber, N'未分配') as Classroom,
                    ISNULL(t.Name, N'') as TeacherName,
                    c.Description,
                    value AS SingleSchedule
                FROM Course c
                CROSS APPLY STRING_SPLIT(c.ScheduleTime, ',') st
                LEFT JOIN Classroom cr ON c.ClassroomID = cr.ClassroomID
                LEFT JOIN TeacherCourse tc ON c.CourseID = tc.CourseID
                LEFT JOIN Teacher t ON tc.TeacherID = t.TeacherID
                WHERE c.SemesterID = @SemesterID
                AND (@CourseType IS NULL OR c.CourseType = @CourseType)
            ),
            ParsedSchedule AS (
                SELECT 
                    *,
                    CAST(SUBSTRING(SingleSchedule, 
                        CHARINDEX('-', SingleSchedule) + 1,
                        CHARINDEX('-', SingleSchedule, CHARINDEX('-', SingleSchedule) + 1) - 
                        CHARINDEX('-', SingleSchedule) - 1) AS INT) as StartSection
                FROM SplitSchedule
            )
            SELECT DISTINCT
                ps.CourseID,
                ps.CourseCode,
                ps.CourseName,
                ps.CourseType,
                ps.Credit,
                ps.ScheduleTime,
                ps.Classroom,
                ps.TeacherName,
                ps.Description,
                CONCAT(
                    (SELECT COUNT(*) FROM StudentCourse WHERE CourseID = ps.CourseID),
                    '/',
                    ps.Capacity
                ) as CurrentCapacity
            FROM ParsedSchedule ps
            WHERE 1=1
            AND (@TimeSlot IS NULL OR 
                CASE @TimeSlot
                    WHEN '1-4' THEN CASE WHEN ps.StartSection BETWEEN 1 AND 4 THEN 1 ELSE 0 END
                    WHEN '5-8' THEN CASE WHEN ps.StartSection BETWEEN 5 AND 8 THEN 1 ELSE 0 END
                    WHEN '9-11' THEN CASE WHEN ps.StartSection BETWEEN 9 AND 11 THEN 1 ELSE 0 END
                    ELSE 1
                END = 1)";

                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@SemesterID", semesterId);
                    command.Parameters.AddWithValue("@CourseType",
                        string.IsNullOrEmpty(courseType) || courseType == "全部类型" ?
                        (object)DBNull.Value : courseType);
                    command.Parameters.AddWithValue("@TimeSlot",
                        (object)timeSlot ?? DBNull.Value);

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
                                Description = reader.IsDBNull(reader.GetOrdinal("Description")) ?
                                    null : reader.GetString(reader.GetOrdinal("Description"))
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
                using var transaction = await connection.BeginTransactionAsync() as SqlTransaction;

                try
                {
                    // 1. 获取所有要选择的课程的详细信息
                    var getCoursesSql = @"
                SELECT 
                    CourseID,
                    CourseCode,
                    ScheduleTime,
                    Capacity,
                    (SELECT COUNT(*) FROM StudentCourse WHERE CourseID = c.CourseID) as CurrentCount
                FROM Course c
                WHERE CourseCode IN (SELECT value FROM STRING_SPLIT(@CourseCodes, ','))
                AND SemesterID = @SemesterID";

                    var courseDetails = new List<(int CourseId, string ScheduleTime, int Capacity, int CurrentCount)>();

                    using (var command = new SqlCommand(getCoursesSql, connection, transaction))
                    {
                        command.Parameters.AddWithValue("@CourseCodes", string.Join(",", courseCodes));
                        command.Parameters.AddWithValue("@SemesterID", semesterId);

                        using var reader = await command.ExecuteReaderAsync();
                        while (await reader.ReadAsync())
                        {
                            courseDetails.Add((
                                reader.GetInt32(reader.GetOrdinal("CourseID")),
                                reader.GetString(reader.GetOrdinal("ScheduleTime")),
                                reader.GetInt32(reader.GetOrdinal("Capacity")),
                                reader.GetInt32(reader.GetOrdinal("CurrentCount"))
                            ));
                        }
                    }

                    // 2. 检查课程容量
                    var overCapacityCourses = courseDetails.Where(c => c.CurrentCount >= c.Capacity).ToList();
                    if (overCapacityCourses.Any())
                    {
                        throw new InvalidOperationException("以下课程已达到人数上限：" +
                            string.Join(", ", overCapacityCourses.Select(c => c.CourseId)));
                    }


                    // 4. 删除当前学期的选课记录（排除已确认的选课）
                    var deleteSql = @"
                DELETE FROM StudentCourse 
                WHERE StudentID = @StudentID 
                AND CourseID IN (
                    SELECT CourseID 
                    FROM Course 
                    WHERE SemesterID = @SemesterID
                )
                AND SelectionType = '待审核'";

                    using (var command = new SqlCommand(deleteSql, connection, transaction))
                    {
                        command.Parameters.AddWithValue("@StudentID", GlobalUserState.LinkedID);
                        command.Parameters.AddWithValue("@SemesterID", semesterId);
                        await command.ExecuteNonQueryAsync();
                    }

                    // 5. 插入新的选课记录
                    var insertSql = @"
                INSERT INTO StudentCourse (StudentID, CourseID, SelectionType, SelectionDate)
                VALUES (@StudentID, @CourseID, '待审核', @SelectionDate)";

                    foreach (var course in courseDetails)
                    {
                        using var command = new SqlCommand(insertSql, connection, transaction);
                        command.Parameters.AddWithValue("@StudentID", GlobalUserState.LinkedID);
                        command.Parameters.AddWithValue("@CourseID", course.CourseId);
                        command.Parameters.AddWithValue("@SelectionDate", DateTime.Now);
                        await command.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();
                    return true;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }

        // 添加一个新的辅助方法用于检查两个课程之间的时间冲突
        private async Task<bool> CheckTimeConflictBetweenCourses(string scheduleTime1, string scheduleTime2)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var sql = @"
        WITH ParsedTime1 AS (
            SELECT 
                CAST(SUBSTRING(value, 1, CHARINDEX('-', value) - 1) AS INT) as WeekDay,
                CAST(SUBSTRING(value, 
                    CHARINDEX('-', value) + 1, 
                    CHARINDEX('-', value, CHARINDEX('-', value) + 1) - 
                    CHARINDEX('-', value) - 1) AS INT) as StartSection,
                CAST(SUBSTRING(value,
                    CHARINDEX('-', value, CHARINDEX('-', value) + 1) + 1,
                    CHARINDEX('-', value, CHARINDEX('-', value, 
                        CHARINDEX('-', value) + 1) + 1) - 
                    CHARINDEX('-', value, CHARINDEX('-', value) + 1) - 1) AS INT) as EndSection,
                CAST(SUBSTRING(value,
                    CHARINDEX('-', value, CHARINDEX('-', value, 
                        CHARINDEX('-', value) + 1) + 1) + 1,
                    CHARINDEX('-', value, CHARINDEX('-', value, CHARINDEX('-', value, 
                        CHARINDEX('-', value) + 1) + 1) + 1) - 
                    CHARINDEX('-', value, CHARINDEX('-', value, 
                        CHARINDEX('-', value) + 1) + 1) - 1) AS INT) as StartWeek,
                CAST(SUBSTRING(value,
                    CHARINDEX('-', value, CHARINDEX('-', value, CHARINDEX('-', value, 
                        CHARINDEX('-', value) + 1) + 1) + 1) + 1,
                    CASE 
                        WHEN CHARINDEX('A', value) > 0 OR CHARINDEX('B', value) > 0 
                        THEN CHARINDEX('-', value + '-', 
                             LAST_VALUE(CHARINDEX('-', value)) OVER (ORDER BY (SELECT NULL))) - 
                             LAST_VALUE(CHARINDEX('-', value)) OVER (ORDER BY (SELECT NULL)) - 1
                        ELSE LEN(value) - 
                             LAST_VALUE(CHARINDEX('-', value)) OVER (ORDER BY (SELECT NULL))
                    END) AS INT) as EndWeek,
                CASE 
                    WHEN CHARINDEX('A', value) > 0 THEN 'A'
                    WHEN CHARINDEX('B', value) > 0 THEN 'B'
                    ELSE NULL 
                END as WeekType
            FROM STRING_SPLIT(@ScheduleTime1, ',')
        ),
        ParsedTime2 AS (
            SELECT 
                CAST(SUBSTRING(value, 1, CHARINDEX('-', value) - 1) AS INT) as WeekDay,
                CAST(SUBSTRING(value, 
                    CHARINDEX('-', value) + 1, 
                    CHARINDEX('-', value, CHARINDEX('-', value) + 1) - 
                    CHARINDEX('-', value) - 1) AS INT) as StartSection,
                CAST(SUBSTRING(value,
                    CHARINDEX('-', value, CHARINDEX('-', value) + 1) + 1,
                    CHARINDEX('-', value, CHARINDEX('-', value, 
                        CHARINDEX('-', value) + 1) + 1) - 
                    CHARINDEX('-', value, CHARINDEX('-', value) + 1) - 1) AS INT) as EndSection,
                CAST(SUBSTRING(value,
                    CHARINDEX('-', value, CHARINDEX('-', value, 
                        CHARINDEX('-', value) + 1) + 1) + 1,
                    CHARINDEX('-', value, CHARINDEX('-', value, CHARINDEX('-', value, 
                        CHARINDEX('-', value) + 1) + 1) + 1) - 
                    CHARINDEX('-', value, CHARINDEX('-', value, 
                        CHARINDEX('-', value) + 1) + 1) - 1) AS INT) as StartWeek,
                CAST(SUBSTRING(value,
                    CHARINDEX('-', value, CHARINDEX('-', value, CHARINDEX('-', value, 
                        CHARINDEX('-', value) + 1) + 1) + 1) + 1,
                    CASE 
                        WHEN CHARINDEX('A', value) > 0 OR CHARINDEX('B', value) > 0 
                        THEN CHARINDEX('-', value + '-', 
                             LAST_VALUE(CHARINDEX('-', value)) OVER (ORDER BY (SELECT NULL))) - 
                             LAST_VALUE(CHARINDEX('-', value)) OVER (ORDER BY (SELECT NULL)) - 1
                        ELSE LEN(value) - 
                             LAST_VALUE(CHARINDEX('-', value)) OVER (ORDER BY (SELECT NULL))
                    END) AS INT) as EndWeek,
                CASE 
                    WHEN CHARINDEX('A', value) > 0 THEN 'A'
                    WHEN CHARINDEX('B', value) > 0 THEN 'B'
                    ELSE NULL 
                END as WeekType
            FROM STRING_SPLIT(@ScheduleTime2, ',')
        )
        SELECT 1
        FROM ParsedTime1 t1
        CROSS JOIN ParsedTime2 t2
        WHERE 
            t1.WeekDay = t2.WeekDay
            AND t1.StartSection <= t2.EndSection 
            AND t2.StartSection <= t1.EndSection
            AND t1.StartWeek <= t2.EndWeek
            AND t2.StartWeek <= t1.EndWeek
            AND (
                (t1.WeekType IS NULL AND t2.WeekType IS NULL)
                OR (t1.WeekType IS NULL AND t2.WeekType IS NOT NULL)
                OR (t1.WeekType IS NOT NULL AND t2.WeekType IS NULL)
                OR (t1.WeekType = t2.WeekType)
            )";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@ScheduleTime1", scheduleTime1);
                command.Parameters.AddWithValue("@ScheduleTime2", scheduleTime2);

                var result = await command.ExecuteScalarAsync();
                return result != null;
            }
        }
    }
}