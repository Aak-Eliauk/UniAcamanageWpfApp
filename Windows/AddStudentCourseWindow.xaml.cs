using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;

namespace UniAcamanageWpfApp.Windows
{
    public partial class AddStudentCourseWindow : Window
    {
        private readonly string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
        private bool isValidStudent = false;
        public event EventHandler SelectionAdded;

        public AddStudentCourseWindow()
        {
            InitializeComponent();
            LoadSemesters();
        }

        private void LoadSemesters()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand(
                        "SELECT SemesterID, SemesterName FROM Semester ORDER BY StartDate DESC", conn);
                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);

                    SemesterComboBox.ItemsSource = dt.DefaultView;
                    SemesterComboBox.DisplayMemberPath = "SemesterName";
                    SemesterComboBox.SelectedValuePath = "SemesterID";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载学期数据失败: {ex.Message}");
            }
        }

        private void LoadCourses(int semesterId)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand(@"
                        SELECT 
                            c.CourseID,
                            c.CourseCode + ' - ' + c.CourseName AS CourseDisplay,
                            c.CourseName,
                            c.CourseCode,
                            c.CourseType,
                            c.Credit,
                            c.ScheduleTime,
                            c.Capacity,
                            (SELECT COUNT(*) FROM StudentCourse WHERE CourseID = c.CourseID AND SelectionType IN ('已通过', '已确认')) AS EnrolledCount
                        FROM Course c
                        WHERE c.SemesterID = @SemesterID", conn);

                    cmd.Parameters.AddWithValue("@SemesterID", semesterId);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);

                    CourseComboBox.ItemsSource = dt.DefaultView;
                    CourseComboBox.DisplayMemberPath = "CourseDisplay";
                    CourseComboBox.SelectedValuePath = "CourseID";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载课程数据失败: {ex.Message}");
            }
        }

        private void SearchStudent_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(StudentIDTextBox.Text))
            {
                MessageBox.Show("请输入学号");
                return;
            }

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand(
                        "SELECT Name, Major, YearOfAdmission FROM Student WHERE StudentID = @StudentID", conn);
                    cmd.Parameters.AddWithValue("@StudentID", StudentIDTextBox.Text);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string name = reader["Name"].ToString();
                            string major = reader["Major"].ToString();
                            int yearOfAdmission = reader.GetInt32(reader.GetOrdinal("YearOfAdmission"));

                            StudentInfoText.Text = $"学生信息：{name} | {yearOfAdmission}级 | {major}";
                            StudentInfoText.Visibility = Visibility.Visible;
                            isValidStudent = true;
                            CourseComboBox.IsEnabled = true;
                        }
                        else
                        {
                            MessageBox.Show("未找到该学生");
                            StudentInfoText.Visibility = Visibility.Collapsed;
                            isValidStudent = false;
                            CourseComboBox.IsEnabled = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"查询学生信息失败: {ex.Message}");
            }
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput())
                return;

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // 检查是否已选过这门课
                            var checkCmd = new SqlCommand(@"
                                SELECT COUNT(*) FROM StudentCourse 
                                WHERE StudentID = @StudentID AND CourseID = @CourseID",
                                conn, transaction);

                            checkCmd.Parameters.AddWithValue("@StudentID", StudentIDTextBox.Text);
                            checkCmd.Parameters.AddWithValue("@CourseID", CourseComboBox.SelectedValue);

                            int existingCount = (int)checkCmd.ExecuteScalar();
                            if (existingCount > 0)
                            {
                                MessageBox.Show("该学生已经选过这门课程");
                                return;
                            }

                            // 添加选课记录
                            var cmd = new SqlCommand(@"
                                INSERT INTO StudentCourse (
                                    StudentID, CourseID, SelectionType, SelectionDate, Remarks
                                ) VALUES (
                                    @StudentID, @CourseID, '已确认', @SelectionDate, @Remarks
                                )", conn, transaction);

                            cmd.Parameters.AddWithValue("@StudentID", StudentIDTextBox.Text);
                            cmd.Parameters.AddWithValue("@CourseID", CourseComboBox.SelectedValue);
                            cmd.Parameters.AddWithValue("@SelectionDate", DateTime.Now);
                            cmd.Parameters.AddWithValue("@Remarks", RemarksTextBox.Text ?? (object)DBNull.Value);

                            cmd.ExecuteNonQuery();
                            transaction.Commit();

                            SelectionAdded?.Invoke(this, EventArgs.Empty);
                            MessageBox.Show("选课成功！");
                            Close();
                        }
                        catch (Exception)
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"选课失败: {ex.Message}");
            }
        }

        private bool ValidateInput()
        {
            if (SemesterComboBox.SelectedValue == null)
            {
                MessageBox.Show("请选择学期");
                return false;
            }

            if (!isValidStudent)
            {
                MessageBox.Show("请输入有效的学号并查找学生");
                return false;
            }

            if (CourseComboBox.SelectedValue == null)
            {
                MessageBox.Show("请选择课程");
                return false;
            }

            return true;
        }

        private void SemesterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SemesterComboBox.SelectedValue != null)
            {
                LoadCourses((int)SemesterComboBox.SelectedValue);
            }
        }

        private void CourseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CourseComboBox.SelectedItem != null)
            {
                DataRowView row = (DataRowView)CourseComboBox.SelectedItem;
                string courseType = row["CourseType"].ToString();
                decimal credit = Convert.ToDecimal(row["Credit"]);
                string scheduleTime = row["ScheduleTime"].ToString();
                int capacity = Convert.ToInt32(row["Capacity"]);
                int enrolledCount = Convert.ToInt32(row["EnrolledCount"]);

                CourseInfoText.Text = $"课程信息：{courseType} | {credit}学分 | 已选{enrolledCount}/{capacity}人\n" +
                                    $"上课时间：{FormatScheduleTime(scheduleTime)}";
                CourseInfoText.Visibility = Visibility.Visible;
            }
            else
            {
                CourseInfoText.Visibility = Visibility.Collapsed;
            }
        }

        private string FormatScheduleTime(string scheduleTime)
        {
            if (string.IsNullOrEmpty(scheduleTime)) return "未安排";

            var slots = scheduleTime.Split(',');
            var formattedTimes = new System.Collections.Generic.List<string>();

            foreach (var slot in slots)
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

        private void StudentIDTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 重置学生信息显示
            if (StudentInfoText.Visibility == Visibility.Visible)
            {
                StudentInfoText.Visibility = Visibility.Collapsed;
                isValidStudent = false;
                CourseComboBox.IsEnabled = false;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}