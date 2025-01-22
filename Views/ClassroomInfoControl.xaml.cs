//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using System.Windows;
//using System.Windows.Controls;
//using System.Windows.Data;
//using System.Windows.Documents;
//using System.Windows.Input;
//using System.Windows.Media;
//using System.Windows.Media.Imaging;
//using System.Windows.Navigation;
//using System.Windows.Shapes;
//using UniAcamanageWpfApp.GeometryT;

//namespace UniAcamanageWpfApp.Views
//{
//    public partial class ClassroomInfoControl : UserControl
//    {
//        public ClassroomInfoControl()
//        {
//            InitializeComponent();
//        }

//        public void UpdateInfo(Classroom classroom)
//        {
//            if (classroom == null) return;

//            txtRoomNumber.Text = classroom.RoomNumber;
//            txtFloor.Text = classroom.Floor?.ToString() ?? "-";
//            txtArea.Text = $"{classroom.Area:F2} m²";
//            txtCapacity.Text = classroom.Capacity?.ToString() ?? "-";
//            txtStatus.Text = GetStatusText(classroom);

//            // 更新课程信息
//            UpdateCoursesList(classroom);
//        }

//        private string GetStatusText(Classroom classroom)
//        {
//            // 获取当前时间
//            var now = DateTime.Now;

//            // 检查是否有当前正在进行的课程
//            var currentCourse = classroom.Courses?
//                .FirstOrDefault(c => IsCurrentTimeInCourseSchedule(c, now));

//            return currentCourse != null
//                ? $"使用中 ({currentCourse.CourseName})"
//                : "空闲";
//        }

//        private bool IsCurrentTimeInCourseSchedule(Course course, DateTime now)
//        {
//            // TODO: 实现课程时间判断逻辑
//            return false;
//        }

//        private async void UpdateCoursesList(Classroom classroom)
//        {
//            try
//            {
//                using var context = new YourDbContext();
//                var todayCourses = await context.Courses
//                    .Where(c => c.ClassroomId == classroom.ClassroomID
//                               && c.DayOfWeek == DateTime.Now.DayOfWeek)
//                    .OrderBy(c => c.StartTime)
//                    .ToListAsync();

//                listCourses.ItemsSource = todayCourses;
//            }
//            catch (Exception ex)
//            {
//                Debug.WriteLine($"加载课程信息失败: {ex.Message}");
//            }
//        }
//    }
//}
