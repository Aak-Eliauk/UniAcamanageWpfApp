using Microsoft.Data.SqlClient;
using Dapper;
using UniAcamanageWpfApp.Models;

namespace UniAcamanageWpfApp.Services
{
    public class InfoManagementService : IInfoManagementService
    {
        private readonly string _connectionString;

        public InfoManagementService()
        {
            _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
        }

        public async Task<bool> UpdateStudentAsync(Student student)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
            UPDATE Student 
            SET Name = @Name,
                Gender = @Gender,
                BirthDate = @BirthDate,
                ClassID = @ClassID,
                YearOfAdmission = @YearOfAdmission,
                Major = @Major,
                Phone = @Phone,
                Email = @Email,
                Status = @Status,
                Address = @Address
            WHERE StudentID = @StudentID";

                var parameters = new
                {
                    student.StudentID,
                    student.Name,
                    student.Gender,
                    student.BirthDate,
                    student.ClassID,
                    student.YearOfAdmission,
                    student.Major,
                    student.Phone,
                    student.Email,
                    student.Status,
                    student.Address
                };

                var result = await connection.ExecuteAsync(query, parameters);
                return result > 0;
            }
        }

        public async Task<List<Student>> GetClassStudentsAsync(string classId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
            SELECT s.*, c.ClassName, d.DepartmentName
            FROM Student s
            LEFT JOIN Class c ON s.ClassID = c.ClassID
            LEFT JOIN Department d ON c.DepartmentID = d.DepartmentID
            WHERE s.ClassID = @ClassId
            ORDER BY s.StudentID";

                var students = await connection.QueryAsync<Student>(query, new { ClassId = classId });
                return students.ToList();
            }
        }

        public async Task<bool> CheckSemesterOverlapAsync(DateTime startDate, DateTime endDate)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
            SELECT COUNT(1)
            FROM Semester
            WHERE (StartDate <= @EndDate AND EndDate >= @StartDate)";

                var count = await connection.ExecuteScalarAsync<int>(query, new { StartDate = startDate, EndDate = endDate });
                return count > 0;
            }
        }

        // 实现更新教师信息的方法
        public async Task<bool> UpdateTeacherAsync(Teacher teacher)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
                UPDATE Teacher 
                SET Name = @Name,
                    Title = @Title,
                    Phone = @Phone,
                    Email = @Email,
                    DepartmentID = @DepartmentID
                WHERE TeacherID = @TeacherID";

                var parameters = new
                {
                    teacher.TeacherID,
                    teacher.Name,
                    teacher.Title,
                    teacher.Phone,
                    teacher.Email,
                    teacher.DepartmentID
                };

                var result = await connection.ExecuteAsync(query, parameters);
                return result > 0;
            }
        }

        // 实现搜索教师的方法
        public async Task<List<Teacher>> SearchTeachersAsync(string searchText, string departmentId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
                SELECT t.*, d.DepartmentName 
                FROM Teacher t
                LEFT JOIN Department d ON t.DepartmentID = d.DepartmentID
                WHERE (@SearchText IS NULL 
                      OR t.TeacherID LIKE @SearchText + '%' 
                      OR t.Name LIKE '%' + @SearchText + '%')
                  AND (@DepartmentId IS NULL OR t.DepartmentID = @DepartmentId)";

                var parameters = new
                {
                    SearchText = string.IsNullOrWhiteSpace(searchText) ? null : searchText,
                    DepartmentId = string.IsNullOrWhiteSpace(departmentId) ? null : departmentId
                };

                var teachers = await connection.QueryAsync<Teacher>(query, parameters);
                return teachers.ToList();
            }
        }

        // 实现获取单个教师信息的方法
        public async Task<Teacher> GetTeacherByIdAsync(string teacherId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
                SELECT t.*, d.DepartmentName 
                FROM Teacher t
                LEFT JOIN Department d ON t.DepartmentID = d.DepartmentID
                WHERE t.TeacherID = @TeacherId";

                return await connection.QueryFirstOrDefaultAsync<Teacher>(query, new { TeacherId = teacherId });
            }
        }

        // 实现添加教师的方法
        public async Task<bool> AddTeacherAsync(Teacher teacher)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
                INSERT INTO Teacher (TeacherID, Name, Title, Phone, Email, DepartmentID)
                VALUES (@TeacherID, @Name, @Title, @Phone, @Email, @DepartmentID)";

                var parameters = new
                {
                    teacher.TeacherID,
                    teacher.Name,
                    teacher.Title,
                    teacher.Phone,
                    teacher.Email,
                    teacher.DepartmentID
                };

                var result = await connection.ExecuteAsync(query, parameters);
                return result > 0;
            }
        }

        // 实现删除教师的方法
        public async Task<bool> DeleteTeacherAsync(string teacherId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // 首先检查是否有关联的课程
                var checkQuery = @"
                SELECT COUNT(1) 
                FROM TeacherCourse 
                WHERE TeacherID = @TeacherId";

                var count = await connection.ExecuteScalarAsync<int>(checkQuery, new { TeacherId = teacherId });
                if (count > 0)
                {
                    throw new InvalidOperationException("该教师有关联的课程，无法删除");
                }

                var query = "DELETE FROM Teacher WHERE TeacherID = @TeacherId";
                var result = await connection.ExecuteAsync(query, new { TeacherId = teacherId });
                return result > 0;
            }
        }

        // 实现获取所有教师的方法
        public async Task<List<Teacher>> GetTeachersAsync()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
                SELECT t.*, d.DepartmentName 
                FROM Teacher t
                LEFT JOIN Department d ON t.DepartmentID = d.DepartmentID";

                var teachers = await connection.QueryAsync<Teacher>(query);
                return teachers.ToList();
            }
        }

        // 获取所有院系
        public async Task<List<Department>> GetDepartmentsAsync()  
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var departments = await connection.QueryAsync<Department>(
                    "SELECT DepartmentID, DepartmentName, OfficePhone FROM Department");
                return departments.ToList();
            }
        }

        // 按院系获取班级
        public async Task<List<Class>> GetClassesByDepartmentAsync(string departmentId)  
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var classes = await connection.QueryAsync<Class>(
                    "SELECT ClassID, ClassName FROM Class WHERE DepartmentID = @DepartmentID",
                    new { DepartmentID = departmentId });
                return classes.ToList();
            }
        }

        // 搜索学生
        public async Task<List<Student>> SearchStudentsAsync(string searchText, string major = null, string classId = null)  // 添加Async后缀
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = @"
                SELECT s.*, c.ClassName, d.DepartmentName 
                FROM Student s
                LEFT JOIN Class c ON s.ClassID = c.ClassID
                LEFT JOIN Department d ON c.DepartmentID = d.DepartmentID
                WHERE (@SearchText IS NULL OR s.StudentID LIKE @SearchText + '%' 
                    OR s.Name LIKE '%' + @SearchText + '%')
                AND (@Major IS NULL OR s.Major = @Major)
                AND (@ClassID IS NULL OR s.ClassID = @ClassID)";

                var students = await connection.QueryAsync<Student>(query,
                    new
                    {
                        SearchText = string.IsNullOrWhiteSpace(searchText) ? null : searchText,
                        Major = major,
                        ClassID = classId
                    });
                return students.ToList();
            }
        }

        // 获取班级学生名单
        public async Task<List<Student>> GetStudentsByClassAsync(string departmentId, int admissionYear, string classId = null)  // 添加Async后缀
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = @"
                SELECT s.*, c.ClassName, d.DepartmentName
                FROM Student s
                JOIN Class c ON s.ClassID = c.ClassID
                JOIN Department d ON c.DepartmentID = d.DepartmentID
                WHERE d.DepartmentID = @DepartmentID
                AND s.YearOfAdmission = @YearOfAdmission
                AND (@ClassID IS NULL OR s.ClassID = @ClassID)";

                var students = await connection.QueryAsync<Student>(query,
                    new
                    {
                        DepartmentID = departmentId,
                        YearOfAdmission = admissionYear,
                        ClassID = classId
                    });
                return students.ToList();
            }
        }

        // 获取学期列表
        public async Task<List<Semester>> GetSemestersAsync()  
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var semesters = await connection.QueryAsync<Semester>(
                    "SELECT * FROM Semester ORDER BY StartDate DESC");
                return semesters.ToList();
            }
        }

        // 添加学期
        public async Task<int> AddSemesterAsync(Semester semester)  
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var sql = @"
                INSERT INTO Semester (SemesterName, AcademicYearID, StartDate, EndDate)
                VALUES (@SemesterName, @AcademicYearID, @StartDate, @EndDate);
                SELECT CAST(SCOPE_IDENTITY() as int)";

                return await connection.ExecuteScalarAsync<int>(sql, semester);
            }
        }

        public async Task<bool> CheckSemesterOverlapExceptCurrentAsync(int currentSemesterId, DateTime startDate, DateTime endDate)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var sql = @"
            SELECT COUNT(1)
            FROM Semester
            WHERE SemesterID != @CurrentSemesterId
            AND (StartDate <= @EndDate AND EndDate >= @StartDate)";

                var count = await connection.ExecuteScalarAsync<int>(sql,
                    new { CurrentSemesterId = currentSemesterId, StartDate = startDate, EndDate = endDate });
                return count > 0;
            }
        }

        // 更新学期
        public async Task<bool> UpdateSemesterAsync(Semester semester)  
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var rowsAffected = await connection.ExecuteAsync(@"
                UPDATE Semester 
                SET SemesterName = @SemesterName,
                    AcademicYearID = @AcademicYearID,
                    StartDate = @StartDate,
                    EndDate = @EndDate
                WHERE SemesterID = @SemesterID",
                    semester);
                return rowsAffected > 0;
            }
        }

        // 删除学期
        public async Task<bool> DeleteSemesterAsync(int semesterId) 
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var rowsAffected = await connection.ExecuteAsync(
                    "DELETE FROM Semester WHERE SemesterID = @SemesterID",
                    new { SemesterID = semesterId });
                return rowsAffected > 0;
            }
        }

        public async Task<IEnumerable<Semester>> GetSemestersBySearchTermAsync(string searchTerm)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                const string sql = @"
                SELECT SemesterID, SemesterName, AcademicYearID, StartDate, EndDate 
                FROM Semester 
                WHERE SemesterName LIKE @SearchTerm 
                   OR CAST(AcademicYearID AS NVARCHAR) LIKE @SearchTerm
                   OR CONVERT(NVARCHAR, StartDate, 23) LIKE @SearchTerm
                   OR CONVERT(NVARCHAR, EndDate, 23) LIKE @SearchTerm
                ORDER BY StartDate DESC";

                var parameters = new { SearchTerm = $"%{searchTerm}%" };
                return await connection.QueryAsync<Semester>(sql, parameters);
            }
        }

        public async Task<IEnumerable<Course>> GetCoursesBySemesterAsync(int semesterID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                const string sql = @"
                SELECT DISTINCT c.* 
                FROM Course c
                JOIN TeacherCourse tc ON c.CourseID = tc.CourseID
                WHERE tc.SemesterID = @SemesterID";

                return await connection.QueryAsync<Course>(sql, new { SemesterID = semesterID });
            }
        }

        public async Task<IEnumerable<TeacherCourse>> GetTeacherCoursesBySemesterAsync(int semesterID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                const string sql = @"
                SELECT tc.*, t.Name as TeacherName, c.CourseName
                FROM TeacherCourse tc
                JOIN Teacher t ON tc.TeacherID = t.TeacherID
                JOIN Course c ON tc.CourseID = c.CourseID
                WHERE tc.SemesterID = @SemesterID";

                return await connection.QueryAsync<TeacherCourse>(sql, new { SemesterID = semesterID });
            }
        }

        public async Task<bool> CheckSemesterHasRelatedDataAsync(int semesterId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // 检查是否有关联的课程数据
                // 如果有Course表与Semester相关联，就需要检查Course表
                var sql = @"
            SELECT COUNT(1) 
            FROM Course 
            WHERE SemesterID = @SemesterId";

                var count = await connection.ExecuteScalarAsync<int>(sql, new { SemesterId = semesterId });
                return count > 0;
            }
        }
    }
}