using System;
using Microsoft.Data.SqlClient;
using System.Windows;
using System.Windows.Media;

namespace UniAcamanageWpfApp.Windows
{
    public partial class CourseSelectionDetailsWindow : Window
    {
        private readonly string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
        private readonly string studentId;
        private readonly int courseId;

        public CourseSelectionDetailsWindow(string studentId, int courseId)
        {
            InitializeComponent();
            this.studentId = studentId;
            this.courseId = courseId;
            LoadDetails();
        }

        private void LoadDetails()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"
                        SELECT 
                            s.StudentID,
                            s.Name AS StudentName,
                            s.Major,
                            s.YearOfAdmission,
                            s.GPA,
                            c.CourseCode,
                            c.CourseName,
                            c.CourseType,
                            c.Credit,
                            c.ScheduleTime,
                            cr.RoomNumber,
                            cr.Floor,
                            c.Capacity,
                            (SELECT COUNT(*) FROM StudentCourse WHERE CourseID = @CourseID AND SelectionType IN ('已通过', '已确认')) AS EnrolledCount,
                            sc.SelectionType,
                            sc.SelectionDate,
                            sc.RejectReason
                        FROM StudentCourse sc
                        JOIN Student s ON sc.StudentID = s.StudentID
                        JOIN Course c ON sc.CourseID = c.CourseID
                        LEFT JOIN Classroom cr ON c.ClassroomID = cr.ClassroomID
                        WHERE sc.StudentID = @StudentID AND sc.CourseID = @CourseID";

                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@StudentID", studentId);
                    cmd.Parameters.AddWithValue("@CourseID", courseId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            // 学生信息
                            txtStudentId.Text = reader["StudentID"].ToString();
                            txtStudentName.Text = reader["StudentName"].ToString();
                            txtMajor.Text = reader["Major"].ToString();
                            txtGrade.Text = reader["YearOfAdmission"].ToString() + "级";
                            txtGPA.Text = reader["GPA"].ToString();

                            // 课程信息
                            txtCourseCode.Text = reader["CourseCode"].ToString();
                            txtCourseName.Text = reader["CourseName"].ToString();
                            txtCourseType.Text = reader["CourseType"].ToString();
                            txtCredit.Text = reader["Credit"].ToString();

                            // 处理课程时间显示
                            string scheduleTime = reader["ScheduleTime"].ToString();
                            txtScheduleTime.Text = FormatScheduleTime(scheduleTime);

                            // 教室信息
                            string roomNumber = reader["RoomNumber"].ToString();
                            int floor = reader.IsDBNull(reader.GetOrdinal("Floor")) ? 0 : reader.GetInt32(reader.GetOrdinal("Floor"));
                            txtClassroom.Text = !string.IsNullOrEmpty(roomNumber) ? $"{roomNumber} ({floor}楼)" : "未分配";

                            // 容量信息
                            int capacity = reader.GetInt32(reader.GetOrdinal("Capacity"));
                            int enrolledCount = reader.GetInt32(reader.GetOrdinal("EnrolledCount"));
                            txtCapacity.Text = $"{enrolledCount}/{capacity}";

                            // 选课状态
                            string selectionType = reader["SelectionType"].ToString();
                            txtSelectionStatus.Text = selectionType;

                            // 设置状态颜色
                            switch (selectionType)
                            {
                                case "已确认":
                                    statusBorder.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                                    txtSelectionStatus.Foreground = Brushes.White;
                                    break;
                                case "待审核":
                                    statusBorder.Background = new SolidColorBrush(Color.FromRgb(255, 152, 0));
                                    txtSelectionStatus.Foreground = Brushes.White;
                                    break;
                                case "未通过":
                                    statusBorder.Background = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                                    txtSelectionStatus.Foreground = Brushes.White;
                                    break;
                            }

                            // 选课时间
                            if (!reader.IsDBNull(reader.GetOrdinal("SelectionDate")))
                            {
                                txtSelectionDate.Text = ((DateTime)reader["SelectionDate"]).ToString("yyyy-MM-dd HH:mm:ss");
                            }

                            // 退选原因
                            if (!reader.IsDBNull(reader.GetOrdinal("RejectReason")))
                            {
                                string rejectReason = reader["RejectReason"].ToString();
                                if (!string.IsNullOrEmpty(rejectReason))
                                {
                                    lblRejectReason.Visibility = Visibility.Visible;
                                    txtRejectReason.Visibility = Visibility.Visible;
                                    txtRejectReason.Text = rejectReason;
                                }
                            }
                        }
                        else
                        {
                            MessageBox.Show("未找到选课记录！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            Close();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private string FormatScheduleTime(string scheduleTime)
        {
            if (string.IsNullOrEmpty(scheduleTime)) return "未安排";

            var timeSlots = scheduleTime.Split(',');
            var formattedTimes = new System.Collections.Generic.List<string>();

            foreach (var slot in timeSlots)
            {
                var parts = slot.Split('-');
                if (parts.Length < 5) continue;

                string weekDay = GetWeekDayName(int.Parse(parts[0]));
                string sectionRange = $"第{parts[1]}-{parts[2]}节";
                string weekRange = $"第{parts[3]}-{parts[4]}周";
                string weekType = parts.Length > 5 ? (parts[5] == "A" ? "单周" : "双周") : "";

                formattedTimes.Add($"{weekDay} {sectionRange} ({weekRange}{weekType})");
            }

            return string.Join("\n", formattedTimes);
        }

        private string GetWeekDayName(int weekDay)
        {
            return weekDay switch
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
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}