using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;

namespace UniAcamanageWpfApp.Views
{
    public partial class HomePageView : UserControl
    {
        private readonly string connectionString;
        private DispatcherTimer timer;
        private List<Course> currentUserCourses;
        private int currentWeek;

        // 导航事件
        public event Action<int> NavigateToAcademicStatus;

        private readonly Color[] courseColors = new Color[]
        {
            (Color)ColorConverter.ConvertFromString("#E3F2FD"),  // 浅蓝
            (Color)ColorConverter.ConvertFromString("#F3E5F5"),  // 浅紫
            (Color)ColorConverter.ConvertFromString("#E8F5E9"),  // 浅绿
            (Color)ColorConverter.ConvertFromString("#FFF3E0"),  // 浅橙
            (Color)ColorConverter.ConvertFromString("#FFEBEE"),  // 浅红
            (Color)ColorConverter.ConvertFromString("#F5F5F5"),  // 浅灰
            (Color)ColorConverter.ConvertFromString("#E0F7FA")   // 浅青
        };

        public HomePageView()
        {
            InitializeComponent();
            connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
            currentUserCourses = new List<Course>();
            InitializeUI();
            LoadData();

            // 启动时钟更新
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void InitializeUI()
        {
            // 设置欢迎信息
            string roleText = GlobalUserState.Role switch
            {
                "Student" => "同学",
                "Teacher" => "老师",
                "Admin" => "管理员",
                _ => ""
            };
            WelcomeText.Text = $"您好，{GlobalUserState.Username} {roleText}";

            // 获取当前学期信息
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var command = new SqlCommand(@"
                    SELECT SemesterName 
                    FROM Semester 
                    WHERE StartDate <= GETDATE() AND EndDate >= GETDATE()", conn);
                var semesterName = command.ExecuteScalar()?.ToString() ?? "未知学期";
                SemesterInfo.Text = semesterName;
            }

            // 初始化周次选择器
            currentWeek = CalculateCurrentWeek();
            WeekInfo.Text = $"第{currentWeek}周";
            InitializeWeekSelector();

            // 初始化课表网格
            InitializeCourseScheduleGrid();
        }

        private void InitializeWeekSelector()
        {
            for (int i = 1; i <= 20; i++)  // 假设一个学期20周
            {
                WeekSelector.Items.Add($"第{i}周");
            }
            WeekSelector.SelectedIndex = currentWeek - 1;
            WeekSelector.SelectionChanged += WeekSelector_SelectionChanged;
        }

        private void WeekSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (WeekSelector.SelectedIndex >= 0)
            {
                currentWeek = WeekSelector.SelectedIndex + 1;
                WeekInfo.Text = $"第{currentWeek}周";
                UpdateCourseSchedule();
            }
        }

        private int CalculateCurrentWeek()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var command = new SqlCommand(@"
                    SELECT TOP 1 StartDate 
                    FROM Semester 
                    WHERE StartDate <= GETDATE() AND EndDate >= GETDATE()", conn);

                var startDate = command.ExecuteScalar() as DateTime?;
                if (startDate.HasValue)
                {
                    TimeSpan difference = DateTime.Now - startDate.Value;
                    return (difference.Days / 7) + 1;
                }
                return 1;
            }
        }

        private void LoadData()
        {
            LoadCurrentWeekCourses();
            UpdateDateTime();
        }

        private void LoadCurrentWeekCourses()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var command = new SqlCommand();
                command.Connection = conn;

                switch (GlobalUserState.Role)
                {
                    case "Student":
                        command.CommandText = @"
                    SELECT DISTINCT
                        c.CourseID,
                        c.CourseName,
                        c.CourseCode,
                        c.ScheduleTime,
                        t.Name as TeacherName,
                        cr.RoomNumber
                    FROM Course c
                    JOIN StudentCourse sc ON c.CourseID = sc.CourseID
                    LEFT JOIN TeacherCourse tc ON c.CourseID = tc.CourseID
                    LEFT JOIN Teacher t ON tc.TeacherID = t.TeacherID
                    LEFT JOIN Classroom cr ON c.ClassroomID = cr.ClassroomID
                    WHERE sc.StudentID = @UserID 
                    AND c.SemesterId = @CurrentSemester";
                        break;

                    case "Teacher":
                        command.CommandText = @"
                    SELECT DISTINCT
                        c.CourseID,
                        c.CourseName,
                        c.CourseCode,
                        c.ScheduleTime,
                        t.Name as TeacherName,
                        cr.RoomNumber
                    FROM Course c
                    JOIN TeacherCourse tc ON c.CourseID = tc.CourseID
                    LEFT JOIN Teacher t ON tc.TeacherID = t.TeacherID
                    LEFT JOIN Classroom cr ON c.ClassroomID = cr.ClassroomID
                    WHERE tc.TeacherID = @UserID 
                    AND c.SemesterId = @CurrentSemester";
                        break;

                    default:
                        return;
                }

                command.Parameters.AddWithValue("@UserID", GlobalUserState.LinkedID);
                command.Parameters.AddWithValue("@CurrentSemester", GetCurrentSemesterID());

                currentUserCourses.Clear();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        currentUserCourses.Add(new Course
                        {
                            CourseID = reader.GetInt32(0),
                            CourseName = reader.GetString(1),
                            CourseCode = reader.GetString(2),
                            ScheduleTime = reader.GetString(3),
                            TeacherName = reader.IsDBNull(4) ? "" : reader.GetString(4),
                            Classroom = reader.IsDBNull(5) ? "" : reader.GetString(5)
                        });
                    }
                }
            }

            UpdateCourseSchedule();
        }


        private void InitializeCourseScheduleGrid()
        {
            CourseScheduleGrid.Children.Clear();

            // 添加表头
            string[] headers = { "节次", "周一", "周二", "周三", "周四", "周五", "周六", "周日" };
            for (int i = 0; i < 8; i++)
            {
                var header = new Border
                {
                    Background = (SolidColorBrush)FindResource("PrimaryBrush"),
                    CornerRadius = new CornerRadius(i == 0 ? 4 : 0, i == 7 ? 4 : 0, 0, 0)
                };

                var text = new TextBlock
                {
                    Text = headers[i],
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(5)
                };

                header.Child = text;
                Grid.SetRow(header, 0);
                Grid.SetColumn(header, i);
                CourseScheduleGrid.Children.Add(header);
            }

            // 添加节次标签
            for (int i = 1; i <= 11; i++)
            {
                var timeLabel = new TextBlock
                {
                    Text = $"第{i}节",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(5)
                };
                Grid.SetRow(timeLabel, i);
                Grid.SetColumn(timeLabel, 0);
                CourseScheduleGrid.Children.Add(timeLabel);
            }
        }

        private void UpdateCourseSchedule()
        {
            // 清除现有课程显示
            var elementsToRemove = new List<UIElement>();
            foreach (UIElement element in CourseScheduleGrid.Children)
            {
                if (element is Border border && Grid.GetRow(border) > 0 && Grid.GetColumn(border) > 0)
                {
                    elementsToRemove.Add(element);
                }
            }
            foreach (var element in elementsToRemove)
            {
                CourseScheduleGrid.Children.Remove(element);
            }

            // 添加课程到课表
            int colorIndex = 0;
            foreach (var course in currentUserCourses)
            {
                var slots = ParseScheduleTime(course.ScheduleTime);
                foreach (var slot in slots)
                {
                    if (ShouldShowCourse(slot))
                    {
                        var courseCard = CreateCourseCard(course, courseColors[colorIndex % courseColors.Length]);
                        Grid.SetRow(courseCard, slot.StartPeriod);
                        Grid.SetColumn(courseCard, slot.DayOfWeek);
                        Grid.SetRowSpan(courseCard, slot.EndPeriod - slot.StartPeriod + 1);
                        CourseScheduleGrid.Children.Add(courseCard);
                    }
                }
                colorIndex++;
            }
        }

        private Border CreateCourseCard(Course course, Color backgroundColor)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(backgroundColor),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(2),
                Padding = new Thickness(5),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 0, 0, 0)),
                BorderThickness = new Thickness(1)
            };

            var stackPanel = new StackPanel
            {
                Margin = new Thickness(3)
            };

            // 课程名称
            stackPanel.Children.Add(new TextBlock
            {
                Text = course.CourseName,
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 2)
            });

            // 教室信息
            if (!string.IsNullOrEmpty(course.Classroom))
            {
                stackPanel.Children.Add(new TextBlock
                {
                    Text = $"📍{course.Classroom}",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Colors.DarkGray),
                    Margin = new Thickness(0, 0, 0, 2)
                });
            }

            // 教师信息
            if (!string.IsNullOrEmpty(course.TeacherName))
            {
                stackPanel.Children.Add(new TextBlock
                {
                    Text = $"👨‍🏫{course.TeacherName}",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Colors.DarkGray)
                });
            }

            border.Child = stackPanel;

            // 添加鼠标悬停效果
            border.MouseEnter += (s, e) =>
            {
                border.Background = new SolidColorBrush(Color.FromArgb(
                    255,
                    (byte)(backgroundColor.R * 0.9),
                    (byte)(backgroundColor.G * 0.9),
                    (byte)(backgroundColor.B * 0.9)));
            };

            border.MouseLeave += (s, e) =>
            {
                border.Background = new SolidColorBrush(backgroundColor);
            };

            // 点击显示教室位置
            border.MouseLeftButtonDown += (s, e) => ShowClassroomLocation(course.Classroom);

            // 更详细的工具提示
            var toolTipContent = new StackPanel();
            toolTipContent.Children.Add(new TextBlock
            {
                Text = course.CourseName,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5)
            });
            toolTipContent.Children.Add(new TextBlock { Text = $"课程代码：{course.CourseCode}" });

            if (!string.IsNullOrEmpty(course.TeacherName))
                toolTipContent.Children.Add(new TextBlock { Text = $"任课教师：{course.TeacherName}" });

            if (!string.IsNullOrEmpty(course.Classroom))
                toolTipContent.Children.Add(new TextBlock { Text = $"上课地点：{course.Classroom}" });

            var scheduleSlot = ParseScheduleTime(course.ScheduleTime).FirstOrDefault();
            if (scheduleSlot != null)
            {
                var weekType = scheduleSlot.WeekType == "A" ? "单周" :
                              scheduleSlot.WeekType == "B" ? "双周" : "每周";
                toolTipContent.Children.Add(new TextBlock
                {
                    Text = $"上课周次：第{scheduleSlot.StartWeek}-{scheduleSlot.EndWeek}周（{weekType}）"
                });
            }

            border.ToolTip = new ToolTip { Content = toolTipContent };

            return border;
        }

        public void ShowClassroomLocation(string classroomNumber)
        {
            var mapWindow = new ClassroomMapWindow(classroomNumber);
            mapWindow.ShowDialog();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            UpdateDateTime();
        }

        private void UpdateDateTime()
        {
            DateText.Text = DateTime.Now.ToString("yyyy年MM月dd日");
            TimeText.Text = DateTime.Now.ToString("HH:mm:ss");
        }

        private class ScheduleTimeSlot
        {
            public int DayOfWeek { get; set; }
            public int StartPeriod { get; set; }
            public int EndPeriod { get; set; }
            public int StartWeek { get; set; }
            public int EndWeek { get; set; }
            public string WeekType { get; set; }
        }

        private List<ScheduleTimeSlot> ParseScheduleTime(string scheduleTime)
        {
            var result = new List<ScheduleTimeSlot>();
            if (string.IsNullOrEmpty(scheduleTime)) return result;

            var slots = scheduleTime.Split(',');
            foreach (var slot in slots)
            {
                var parts = slot.Split('-');
                if (parts.Length < 5) continue;

                try
                {
                    result.Add(new ScheduleTimeSlot
                    {
                        DayOfWeek = int.Parse(parts[0]),
                        StartPeriod = int.Parse(parts[1]),
                        EndPeriod = int.Parse(parts[2]),
                        StartWeek = int.Parse(parts[3]),
                        EndWeek = int.Parse(parts[4]),
                        WeekType = parts.Length > 5 ? parts[5] : null
                    });
                }
                catch
                {
                    continue;
                }
            }

            return result;
        }

        private bool ShouldShowCourse(ScheduleTimeSlot slot)
        {
            bool isCurrentWeekOdd = currentWeek % 2 == 1;

            if (currentWeek < slot.StartWeek || currentWeek > slot.EndWeek)
                return false;

            if (slot.WeekType == "A" && !isCurrentWeekOdd)
                return false;

            if (slot.WeekType == "B" && isCurrentWeekOdd)
                return false;

            return true;
        }

        private int GetCurrentSemesterID()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var command = new SqlCommand(
                    "SELECT TOP 1 SemesterID FROM Semester WHERE StartDate <= GETDATE() AND EndDate >= GETDATE()",
                    conn);
                var result = command.ExecuteScalar();
                return result != null ? (int)result : 1;
            }
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (timer != null)
            {
                timer.Stop();
                timer = null;
            }
        }
    }
}