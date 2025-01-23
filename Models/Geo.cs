using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UniAcamanageWpfApp.Models
{
    public class FacilityType
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string[] Keywords { get; set; }
        public string IconKind { get; set; }  // MaterialDesign图标名称

        public static readonly Dictionary<string, FacilityType> Types = new()
        {
            ["teaching_building"] = new FacilityType
            {
                Id = "teaching_building",
                Name = "教学楼",
                Keywords = new[] { "教学楼" },
                IconKind = "School"
            },
            ["college_building"] = new FacilityType
            {
                Id = "college_building",
                Name = "学院楼",
                Keywords = new[] { "学院", "系", "教研" },
                IconKind = "AccountMultiple"
            },
            ["laboratory"] = new FacilityType
            {
                Id = "laboratory",
                Name = "实验室",
                Keywords = new[] { "实验室", "实验中心" },
                IconKind = "Flask"
            },
            ["library"] = new FacilityType
            {
                Id = "library",
                Name = "图书馆",
                Keywords = new[] { "图书馆", "阅览室" },
                IconKind = "Library"
            },
            ["gymnasium"] = new FacilityType
            {
                Id = "gymnasium",
                Name = "体育馆",
                Keywords = new[] { "体育馆", "体育场", "运动场" },
                IconKind = "Basketball"
            },
            ["cafeteria"] = new FacilityType
            {
                Id = "cafeteria",
                Name = "食堂",
                Keywords = new[] { "食堂", "餐厅", "餐饮" },
                IconKind = "FoodForkDrink"
            },
            ["hospital"] = new FacilityType
            {
                Id = "hospital",
                Name = "医院",
                Keywords = new[] { "医院", "卫生院", "医务室" },
                IconKind = "Hospital"
            },
            ["dormitory"] = new FacilityType
            {
                Id = "dormitory",
                Name = "学生宿舍",
                Keywords = new[] { "宿舍", "公寓", "住宿" },
                IconKind = "Home"
            }
        };
    }

    public class NavigationPoint
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public class NavigationStep
    {
        public string Instruction { get; set; }
        public double Distance { get; set; }  // 米
        public double Duration { get; set; }  // 秒
        public string Name { get; set; }
        public List<double[]> Geometry { get; set; }  // [[lon, lat], ...]
    }

    public class NavigationResult
    {
        public List<NavigationStep> Steps { get; set; }
        public double TotalDistance { get; set; }  // 米
        public double TotalDuration { get; set; }  // 秒
        public List<double[]> RouteGeometry { get; set; }  // 完整路线几何数据
        public string Mode { get; set; }  // "foot" 或 "bicycle"
    }

    public class CourseSchedule
    {
        public int CourseID { get; set; }
        public string CourseName { get; set; }
        public string TeacherName { get; set; }
        public ClassroomSpatial Classroom { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public int WeekNumber { get; set; }
        public bool IsCurrentCourse =>
            DateTime.Now >= StartTime && DateTime.Now <= EndTime;

        public string TimeSlot => $"{StartTime:HH:mm}-{EndTime:HH:mm}";

        public string Location =>
            Classroom != null ? $"{Classroom.SpatialLocation} {Classroom.RoomNumber}" : "未安排教室";

        public CourseStatus Status
        {
            get
            {
                var now = DateTime.Now;
                if (now < StartTime)
                    return CourseStatus.Upcoming;
                if (now > EndTime)
                    return CourseStatus.Finished;
                return CourseStatus.InProgress;
            }
        }
    }

    public enum CourseStatus
    {
        Upcoming,
        InProgress,
        Finished
    }

    public class CheckInResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public DateTime CheckInTime { get; set; }
        public bool IsInRange { get; set; }
        public double Distance { get; set; }
    }

    public class GeoJsonFeature
    {
        public string Type { get; set; } = "Feature";
        public GeoJsonGeometry Geometry { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }

    public class GeoJsonGeometry
    {
        public string Type { get; set; }  // "Point", "Polygon", "MultiPolygon" 等
        public object Coordinates { get; set; }  // 根据Type不同而不同的坐标数据
    }

    public class GeoJsonFeatureCollection
    {
        public string Type { get; set; } = "FeatureCollection";
        public List<GeoJsonFeature> Features { get; set; }
    }

    public class CourseLocationInfo
    {
        public int CourseID { get; set; }
        public string CourseName { get; set; }
        public ClassroomSpatial Classroom { get; set; }   // 这里使用 ClassroomSpatial
        public string TimeSlot { get; set; }
        public int CurrentWeek { get; set; }
    }

    public class ClassroomLocationInfo
    {
        public int ClassroomId { get; set; }
        public string RoomNumber { get; set; }
        public string Building { get; set; }
        public int Floor { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public Point CenterPoint { get; set; }
        public Polygon Shape { get; set; }
    }

    public class StudentCourse
    {
        public int Id { get; set; }
        public string StudentID { get; set; }
        public int CourseID { get; set; }
        public string SelectionType { get; set; }
        public DateTime SelectionDate { get; set; }
        public decimal? Score { get; set; }
        public string Remarks { get; set; }
        public string RejectReason { get; set; }

        public virtual Course Course { get; set; }
    }

    public class Semester
    {
        public int SemesterID { get; set; }
        public string SemesterName { get; set; }
        public int AcademicYearID { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class CourseLocation
    {
        public int CourseID { get; set; }
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public int? ClassroomID { get; set; }
        public string ScheduleTime { get; set; }
        public ClassroomSpatial Classroom { get; set; }
    }

    public class BuildingClassrooms
    {
        public string Building { get; set; }
        public List<ClassroomSpatial> Classrooms { get; set; }
    }
}
