using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniAcamanageWpfApp.Data;
using UniAcamanageWpfApp.Models;
using Microsoft.EntityFrameworkCore;
using ControlzEx.Standard;

namespace UniAcamanageWpfApp.Services
{
    public class CourseLocationService
    {
        private readonly CampusDbContext _context;

        public CourseLocationService(CampusDbContext context)
        {
            _context = context;
        }

        public async Task<List<CourseLocationInfo>> GetCurrentLocationCourses()
        {
            var now = DateTime.UtcNow;
            var linkedId = GlobalUserState.LinkedID;
            var userRole = GlobalUserState.Role;

            // 获取当前学期信息
            var currentSemester = await _context.Semesters
                .Where(s => s.StartDate <= now.Date && s.EndDate >= now.Date)
                .FirstOrDefaultAsync();

            if (currentSemester == null)
                return new List<CourseLocationInfo>();

            var currentWeek = GetCurrentWeek(currentSemester.StartDate);
            var dayOfWeek = (int)now.DayOfWeek == 0 ? 7 : (int)now.DayOfWeek;
            var currentTimeSlot = GetCurrentTimeSlot(now);

            var query = _context.Courses.AsQueryable();

            // 根据用户角色筛选课程
            if (userRole.Equals("Student", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Join(_context.StudentCourses,
                    course => course.CourseID,
                    sc => sc.CourseID,
                    (course, sc) => new { Course = course, StudentCourse = sc })
                    .Where(x => x.StudentCourse.StudentID == linkedId &&
                           x.StudentCourse.SelectionType == "已确认")
                    .Select(x => x.Course);
            }
            else if (userRole.Equals("Teacher", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Join(_context.TeacherCourses,
                    course => course.CourseID,
                    tc => tc.CourseID,
                    (course, tc) => new { Course = course, TeacherCourse = tc })
                    .Where(x => x.TeacherCourse.TeacherID == linkedId)
                    .Select(x => x.Course);
            }

            var courses = await query
                .Where(c => c.SemesterId == currentSemester.SemesterID)
                .Join(_context.ClassroomSpatials,
                    course => int.Parse(course.Classroom ?? "0"),
                    classroom => classroom.ClassroomID,
                    (course, classroom) => new CourseLocationInfo
                    {
                        CourseID = course.CourseID,
                        CourseName = course.CourseName,
                        Classroom = classroom,
                        TimeSlot = course.ScheduleTime,
                        CurrentWeek = currentWeek
                    })
                .Where(c => IsCurrentTimeCourse(c.TimeSlot, dayOfWeek, currentWeek, currentTimeSlot))
                .ToListAsync();

            // 格式化显示时间
            foreach (var course in courses)
            {
                course.TimeSlot = GetFormattedTimeSlot(course.TimeSlot);
            }

            return courses;
        }

        public async Task<List<CourseLocationInfo>> GetUpcomingLocationCourses()
        {
            var now = DateTime.UtcNow;
            var linkedId = GlobalUserState.LinkedID;
            var userRole = GlobalUserState.Role;

            var currentSemester = await _context.Semesters
                .Where(s => s.StartDate <= now.Date && s.EndDate >= now.Date)
                .FirstOrDefaultAsync();

            if (currentSemester == null)
                return new List<CourseLocationInfo>();

            var currentWeek = GetCurrentWeek(currentSemester.StartDate);
            var dayOfWeek = (int)now.DayOfWeek == 0 ? 7 : (int)now.DayOfWeek;
            var currentTimeSlot = GetCurrentTimeSlot(now);

            var query = _context.Courses.AsQueryable();

            if (userRole.Equals("Student", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Join(_context.StudentCourses,
                    course => course.CourseID,
                    sc => sc.CourseID,
                    (course, sc) => new { Course = course, StudentCourse = sc })
                    .Where(x => x.StudentCourse.StudentID == linkedId &&
                           x.StudentCourse.SelectionType == "已确认")
                    .Select(x => x.Course);
            }
            else if (userRole.Equals("Teacher", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Join(_context.TeacherCourses,
                    course => course.CourseID,
                    tc => tc.CourseID,
                    (course, tc) => new { Course = course, TeacherCourse = tc })
                    .Where(x => x.TeacherCourse.TeacherID == linkedId)
                    .Select(x => x.Course);
            }

            var courses = await query
                .Where(c => c.SemesterId == currentSemester.SemesterID)
                .Join(_context.ClassroomSpatials,
                    course => int.Parse(course.Classroom ?? "0"),
                    classroom => classroom.ClassroomID,
                    (course, classroom) => new CourseLocationInfo
                    {
                        CourseID = course.CourseID,
                        CourseName = course.CourseName,
                        Classroom = classroom,
                        TimeSlot = course.ScheduleTime,
                        CurrentWeek = currentWeek
                    })
                .Where(c => IsUpcomingCourse(c.TimeSlot, dayOfWeek, currentWeek, currentTimeSlot))
                .ToListAsync();

            foreach (var course in courses)
            {
                course.TimeSlot = GetFormattedTimeSlot(course.TimeSlot);
            }

            return courses;
        }

        private bool IsCurrentTimeCourse(string scheduleTime, int currentDayOfWeek, int currentWeek, int currentTimeSlot)
        {
            if (string.IsNullOrEmpty(scheduleTime)) return false;

            var timeSlots = scheduleTime.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var slot in timeSlots)
            {
                var parts = slot.Trim().Split('-', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5) continue;

                if (!int.TryParse(parts[0], out int dayOfWeek) ||
                    !int.TryParse(parts[1], out int startSlot) ||
                    !int.TryParse(parts[2], out int endSlot) ||
                    !int.TryParse(parts[3], out int startWeek) ||
                    !int.TryParse(parts[4], out int endWeek))
                {
                    continue;
                }

                // 检查周次范围
                if (currentWeek < startWeek || currentWeek > endWeek)
                    continue;

                // 检查单双周
                string weekType = parts.Length > 5 ? parts[5] : "";
                if (!string.IsNullOrEmpty(weekType))
                {
                    bool isCurrentWeekOdd = currentWeek % 2 == 1;
                    if ((weekType == "A" && !isCurrentWeekOdd) || // A表示单周
                        (weekType == "B" && isCurrentWeekOdd))    // B表示双周
                    {
                        continue;
                    }
                }

                // 检查是否是当前星期
                if (dayOfWeek != currentDayOfWeek)
                    continue;

                // 检查当前时间段
                if (currentTimeSlot >= startSlot && currentTimeSlot <= endSlot)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsUpcomingCourse(string scheduleTime, int currentDayOfWeek, int currentWeek, int currentTimeSlot)
        {
            if (string.IsNullOrEmpty(scheduleTime)) return false;

            var timeSlots = scheduleTime.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var slot in timeSlots)
            {
                var parts = slot.Trim().Split('-', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5) continue;

                if (!int.TryParse(parts[0], out int dayOfWeek) ||
                    !int.TryParse(parts[1], out int startSlot) ||
                    !int.TryParse(parts[2], out int endSlot) ||
                    !int.TryParse(parts[3], out int startWeek) ||
                    !int.TryParse(parts[4], out int endWeek))
                {
                    continue;
                }

                if (currentWeek < startWeek || currentWeek > endWeek)
                    continue;

                string weekType = parts.Length > 5 ? parts[5] : "";
                if (!string.IsNullOrEmpty(weekType))
                {
                    bool isCurrentWeekOdd = currentWeek % 2 == 1;
                    if ((weekType == "A" && !isCurrentWeekOdd) ||
                        (weekType == "B" && isCurrentWeekOdd))
                    {
                        continue;
                    }
                }

                // 检查是否是即将开始的课程
                if (dayOfWeek == currentDayOfWeek && startSlot > currentTimeSlot)
                {
                    return true;
                }
                else if (dayOfWeek > currentDayOfWeek)
                {
                    return true;
                }
            }

            return false;
        }

        private string GetFormattedTimeSlot(string scheduleTime)
        {
            if (string.IsNullOrEmpty(scheduleTime)) return string.Empty;

            var formattedSlots = new List<string>();
            var timeSlots = scheduleTime.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var slot in timeSlots)
            {
                var parts = slot.Trim().Split('-');
                if (parts.Length < 5) continue;

                if (!int.TryParse(parts[0], out int dayOfWeek) ||
                    !int.TryParse(parts[1], out int startSlot) ||
                    !int.TryParse(parts[2], out int endSlot) ||
                    !int.TryParse(parts[3], out int startWeek) ||
                    !int.TryParse(parts[4], out int endWeek))
                {
                    continue;
                }

                string weekDay = dayOfWeek switch
                {
                    1 => "周一",
                    2 => "周二",
                    3 => "周三",
                    4 => "周四",
                    5 => "周五",
                    6 => "周六",
                    7 => "周日",
                    _ => "未知"
                };

                string weekType = parts.Length > 5 ? parts[5] switch
                {
                    "A" => "(单周)",
                    "B" => "(双周)",
                    _ => ""
                } : "";

                formattedSlots.Add(
                    $"{weekDay} 第{startSlot}-{endSlot}节 {startWeek}-{endWeek}周{weekType}");
            }

            return string.Join("\n", formattedSlots);
        }

        private int GetCurrentWeek(DateTime semesterStart)
        {
            var diff = DateTime.UtcNow - semesterStart;
            return (diff.Days / 7) + 1;
        }

        private int GetCurrentTimeSlot(DateTime now)
        {
            var timeOfDay = now.TimeOfDay;

            if (timeOfDay < new TimeSpan(8, 0, 0)) return 0;
            if (timeOfDay < new TimeSpan(9, 40, 0)) return 1;
            if (timeOfDay < new TimeSpan(10, 0, 0)) return 2;
            if (timeOfDay < new TimeSpan(11, 40, 0)) return 3;
            if (timeOfDay < new TimeSpan(14, 0, 0)) return 4;
            if (timeOfDay < new TimeSpan(15, 40, 0)) return 5;
            if (timeOfDay < new TimeSpan(16, 0, 0)) return 6;
            if (timeOfDay < new TimeSpan(17, 40, 0)) return 7;
            if (timeOfDay < new TimeSpan(19, 0, 0)) return 8;
            if (timeOfDay < new TimeSpan(20, 40, 0)) return 9;
            if (timeOfDay < new TimeSpan(21, 0, 0)) return 10;
            if (timeOfDay < new TimeSpan(22, 40, 0)) return 11;

            return 0;
        }
    

        public async Task<CheckInResult> CheckInAtLocation(int courseId, double latitude, double longitude)
        {
            var course = await _context.Courses
            .Where(c => c.CourseID == courseId)
            .Join(_context.ClassroomSpatials,
                course => int.Parse(course.Classroom ?? "0"),
                cs => cs.ClassroomID,
                (course, cs) => new { Course = course, Classroom = cs })
            .FirstOrDefaultAsync();

            if (course == null)
                return new CheckInResult
                {
                    Success = false,
                    Message = "未找到课程信息"
                };

            var now = DateTime.UtcNow;
            var currentSemester = await _context.Semesters
                .Where(s => s.StartDate <= now.Date && s.EndDate >= now.Date)
                .FirstOrDefaultAsync();

            if (currentSemester == null)
                return new CheckInResult
                {
                    Success = false,
                    Message = "未在学期内"
                };

            var currentWeek = GetCurrentWeek(currentSemester.StartDate);
            var dayOfWeek = (int)now.DayOfWeek == 0 ? 7 : (int)now.DayOfWeek;
            var currentTimeSlot = GetCurrentTimeSlot(now);

            if (!IsCurrentTimeCourse(course.Course.ScheduleTime, dayOfWeek, currentWeek, currentTimeSlot))
                return new CheckInResult
                {
                    Success = false,
                    Message = "不在课程时间内"
                };

            // 计算用户位置与教室的距离
            var userPoint = GeometryFactory.Default.CreatePoint(
                new Coordinate(longitude, latitude));

            var distance = userPoint.Distance(course.Classroom.CenterPoint);
            var isInRange = distance <= 50; // 假设50米为有效签到范围

            var result = new CheckInResult
            {
                Success = isInRange,
                Message = isInRange ? "签到成功" : "不在教室范围内",
                CheckInTime = DateTime.Now,
                IsInRange = isInRange,
                Distance = distance
            };

            return result;
        }

        public async Task<ClassroomLocationInfo> GetClassroomLocation(int courseId)
    {
        var course = await _context.Courses
            .Where(c => c.CourseID == courseId)
            .Join(_context.ClassroomSpatials,
                course => int.Parse(course.Classroom ?? "0"),
                cs => cs.ClassroomID,
                (course, cs) => new { Course = course, Classroom = cs })
            .FirstOrDefaultAsync();

        if (course?.Classroom == null)
            return null;

        return new ClassroomLocationInfo
        {
            ClassroomId = course.Classroom.ClassroomID,
            RoomNumber = course.Classroom.RoomNumber,
            Building = course.Classroom.SpatialLocation,
            Floor = course.Classroom.Floor,
            Latitude = course.Classroom.CenterPoint.Y,
            Longitude = course.Classroom.CenterPoint.X,
            CenterPoint = course.Classroom.CenterPoint,
            Shape = course.Classroom.Shape as Polygon
        };
    }

        private async Task<List<CourseLocationInfo>> GetStudentCurrentCourses(string studentId, int semesterId, int dayOfWeek, int currentWeek, int currentTimeSlot)
        {
            return await _context.Courses
                .Join(_context.StudentCourses,
                    course => course.CourseID,
                    sc => sc.CourseID,
                    (course, sc) => new { Course = course, StudentCourse = sc })
                .Where(x => x.StudentCourse.StudentID == studentId
                        && x.StudentCourse.SelectionType == "已确认"
                        && x.Course.SemesterId == semesterId)
                .Select(x => x.Course)
                .Where(course => IsCurrentTimeCourse(course.ScheduleTime, dayOfWeek, currentWeek, currentTimeSlot))
                .Join(_context.ClassroomSpatials,
                    course => int.Parse(course.Classroom ?? "0"),
                    classroom => classroom.ClassroomID,
                    (course, classroom) => new CourseLocationInfo
                    {
                        CourseID = course.CourseID,
                        CourseName = course.CourseName,
                        Classroom = classroom,  // 使用 classroom 而不是 cs
                        TimeSlot = GetFormattedTimeSlot(course.ScheduleTime),
                        CurrentWeek = currentWeek
                    })
                .ToListAsync();
        }

        private async Task<List<CourseLocationInfo>> GetTeacherCurrentCourses(string teacherId, int semesterId, int dayOfWeek, int currentWeek, int currentTimeSlot)
        {
            return await _context.Courses
                .Join(_context.TeacherCourses,
                    course => course.CourseID,
                    tc => tc.CourseID,
                    (course, tc) => new { Course = course, TeacherCourse = tc })
                .Where(x => x.TeacherCourse.TeacherID == teacherId
                        && x.Course.SemesterId == semesterId)
                .Select(x => x.Course)
                .Where(course => IsCurrentTimeCourse(course.ScheduleTime, dayOfWeek, currentWeek, currentTimeSlot))
                .Join(_context.ClassroomSpatials,
                    course => int.Parse(course.Classroom ?? "0"),
                    classroom => classroom.ClassroomID,
                    (course, classroom) => new CourseLocationInfo
                    {
                        CourseID = course.CourseID,
                        CourseName = course.CourseName,
                        Classroom = classroom,  // 使用 classroom 而不是 cs
                        TimeSlot = GetFormattedTimeSlot(course.ScheduleTime),
                        CurrentWeek = currentWeek
                    })
                .ToListAsync();
        }

    }


}