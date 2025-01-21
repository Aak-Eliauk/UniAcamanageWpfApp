using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Data;
using UniAcamanageWpfApp.Windows;

namespace UniAcamanageWpfApp.Views
{
    public partial class TeachingManagementView : UserControl
    {
        private readonly string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
        private DataTable coursesTable;
        private DataTable approvalsTable;

        public TeachingManagementView()
        {
            InitializeComponent();

            // 验证用户权限
            if (GlobalUserState.Role != "Admin" && GlobalUserState.Role != "Teacher")
            {
                MessageBox.Show("您没有权限访问此页面", "访问受限", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 如果是教师，验证LinkedID不为空
            if (GlobalUserState.Role == "Teacher" && string.IsNullOrEmpty(GlobalUserState.LinkedID))
            {
                MessageBox.Show("教师工号不能为空", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            InitializeControls();
            LoadSemesters();
            SetupEventHandlers();
        }

        private void InitializeControls()
        {
            // 根据角色设置界面元素可见性
            AddCourseButton.Visibility = Visibility.Visible; // 教师和管理员都可以看到添加课程按钮

            var addStudentCourseButton = (Button)FindName("AddStudentCourseButton");
            if (addStudentCourseButton != null)
            {
                addStudentCourseButton.Visibility = GlobalUserState.Role == "Admin" ?
                    Visibility.Visible : Visibility.Collapsed;
            }

            // 初始化审批状态下拉框
            ApprovalStatusFilter.Items.Clear();
            ApprovalStatusFilter.Items.Add(new ComboBoxItem { Content = "全部" });
            ApprovalStatusFilter.Items.Add(new ComboBoxItem { Content = "已确认" });
            ApprovalStatusFilter.Items.Add(new ComboBoxItem { Content = "待审核" });
            ApprovalStatusFilter.Items.Add(new ComboBoxItem { Content = "未通过" });
            ApprovalStatusFilter.SelectedIndex = 0;
        }

        private void LoadSemesters()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"
                        SELECT 
                            s.SemesterID,
                            s.SemesterName,
                            s.StartDate,
                            s.EndDate
                        FROM Semester s
                        ORDER BY s.StartDate DESC";

                    var adapter = new SqlDataAdapter(query, conn);
                    var dt = new DataTable();
                    adapter.Fill(dt);

                    if (dt.Rows.Count > 0)
                    {
                        // 添加一个"全部"选项
                        DataRow allRow = dt.NewRow();
                        allRow["SemesterID"] = -1;
                        allRow["SemesterName"] = "全部学期";
                        dt.Rows.InsertAt(allRow, 0);

                        // 绑定到两个ComboBox
                        TeachingSemesterComboBox.ItemsSource = dt.DefaultView;
                        TeachingSemesterComboBox.DisplayMemberPath = "SemesterName";
                        TeachingSemesterComboBox.SelectedValuePath = "SemesterID";

                        ApprovalSemesterComboBox.ItemsSource = dt.DefaultView;
                        ApprovalSemesterComboBox.DisplayMemberPath = "SemesterName";
                        ApprovalSemesterComboBox.SelectedValuePath = "SemesterID";

                        // 默认选择第一个非"全部"的学期（如果存在）
                        if (dt.Rows.Count > 1)
                        {
                            TeachingSemesterComboBox.SelectedIndex = 1;
                            ApprovalSemesterComboBox.SelectedIndex = 1;
                        }
                        else
                        {
                            TeachingSemesterComboBox.SelectedIndex = 0;
                            ApprovalSemesterComboBox.SelectedIndex = 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载学期数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetupEventHandlers()
        {
            // 设置课程搜索事件
            CourseSearchBox.TextChanged += CourseSearchBox_TextChanged;

            // 设置审批搜索事件
            ApprovalSearchBox.TextChanged += ApprovalSearchBox_TextChanged;

            // 设置学期选择变化事件
            TeachingSemesterComboBox.SelectionChanged += TeachingSemesterComboBox_SelectionChanged;
            ApprovalSemesterComboBox.SelectionChanged += ApprovalSemesterComboBox_SelectionChanged;

            // 设置筛选条件变化事件
            ApprovalStatusFilter.SelectionChanged += (s, e) => ApplyApprovalFilters();
            CourseFilterComboBox.SelectionChanged += (s, e) => ApplyApprovalFilters();
        }

        private void TeachingSemesterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadTeachingCourses();
        }

        private void ApprovalSemesterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ApprovalSemesterComboBox.SelectedValue != null)
            {
                LoadApprovalCourses();  // 先加载课程筛选数据
                LoadApprovals();        // 再加载审批数据
            }
        }

        private void CourseSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (coursesTable?.DefaultView != null)
            {
                string searchText = CourseSearchBox.Text.ToLower();
                coursesTable.DefaultView.RowFilter = string.IsNullOrWhiteSpace(searchText) ? "" :
                    $"CourseCode LIKE '%{searchText}%' OR CourseName LIKE '%{searchText}%' OR TeacherName LIKE '%{searchText}%'";
            }
        }

        private void ApprovalSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyApprovalFilters();
        }

        private void ApprovalStatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyApprovalFilters();
        }

        private void CourseFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyApprovalFilters();
        }

        private void ApplyApprovalFilters()
        {
            if (approvalsTable?.DefaultView == null) return;

            var filters = new List<string>();

            // 搜索文本过滤
            string searchText = ApprovalSearchBox.Text?.ToLower() ?? "";
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filters.Add($"(StudentId LIKE '%{searchText}%' OR StudentName LIKE '%{searchText}%')");
            }

            // 课程过滤
            if (CourseFilterComboBox.SelectedValue != null && CourseFilterComboBox.SelectedValue != DBNull.Value)
            {
                filters.Add($"CourseID = {CourseFilterComboBox.SelectedValue}");
            }

            // 状态过滤
            if (ApprovalStatusFilter.SelectedItem is ComboBoxItem selectedItem &&
                selectedItem.Content.ToString() != "全部")
            {
                filters.Add($"SelectionType = '{selectedItem.Content}'");
            }

            // 合并所有过滤条件
            approvalsTable.DefaultView.RowFilter = filters.Count > 0 ?
                string.Join(" AND ", filters) : "";
        }

        private string FormatScheduleTime(string scheduleTime)
        {
            if (string.IsNullOrEmpty(scheduleTime)) return "未安排";

            var timeSlots = scheduleTime.Split(',');
            var formattedTimes = new List<string>();

            foreach (var slot in timeSlots)
            {
                var parts = slot.Split('-');
                if (parts.Length < 5) continue;

                string weekDay = GetWeekDayName(int.Parse(parts[0]));
                string sectionRange = $"第{parts[1]}-{parts[2]}节";
                string weekRange = $"第{parts[3]}-{parts[4]}周";
                string weekType = parts.Length > 5 ? (parts[5] == "A" ? "(单周)" : "(双周)") : "";

                formattedTimes.Add($"{weekDay} {sectionRange} {weekRange}{weekType}");
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

        private void LoadTeachingCourses()
        {
            if (TeachingSemesterComboBox.SelectedValue == null) return;

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"
                SELECT 
                    c.CourseID,
                    c.CourseCode,
                    c.CourseName,
                    c.CourseType,
                    c.Credit,
                    t.TeacherID,
                    t.Name as TeacherName,
                    c.ScheduleTime,
                    ISNULL(cr.RoomNumber + ' (' + CAST(cr.Floor as nvarchar) + '楼)', '未分配') as Classroom,
                    c.Capacity,
                    (SELECT COUNT(*) FROM StudentCourse sc 
                     WHERE sc.CourseID = c.CourseID 
                     AND sc.SelectionType = '已确认') as ConfirmedCount,
                    (SELECT COUNT(*) FROM StudentCourse sc 
                     WHERE sc.CourseID = c.CourseID 
                     AND sc.SelectionType = '待审核') as PendingCount
                FROM Course c
                LEFT JOIN TeacherCourse tc ON c.CourseID = tc.CourseID
                LEFT JOIN Teacher t ON tc.TeacherID = t.TeacherID
                LEFT JOIN Classroom cr ON c.ClassroomID = cr.ClassroomID
                WHERE (@SemesterID = -1 OR c.SemesterID = @SemesterID) ";

                    if (GlobalUserState.Role == "Teacher")
                    {
                        query += "AND tc.TeacherID = @TeacherID ";
                    }

                    query += "ORDER BY c.CourseCode";

                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@SemesterID", TeachingSemesterComboBox.SelectedValue);

                    if (GlobalUserState.Role == "Teacher")
                    {
                        cmd.Parameters.AddWithValue("@TeacherID", GlobalUserState.LinkedID);
                    }

                    coursesTable = new DataTable();
                    using (var adapter = new SqlDataAdapter(cmd))
                    {
                        adapter.Fill(coursesTable);

                        // 创建计算列
                        if (!coursesTable.Columns.Contains("SelectionStatus"))
                            coursesTable.Columns.Add("SelectionStatus", typeof(string));
                        if (!coursesTable.Columns.Contains("EnrollmentStatus"))
                            coursesTable.Columns.Add("EnrollmentStatus", typeof(string));
                        if (!coursesTable.Columns.Contains("IsFull"))
                            coursesTable.Columns.Add("IsFull", typeof(bool));

                        foreach (DataRow row in coursesTable.Rows)
                        {
                            int confirmedCount = Convert.ToInt32(row["ConfirmedCount"]);
                            int pendingCount = Convert.ToInt32(row["PendingCount"]);
                            int capacity = Convert.ToInt32(row["Capacity"]);

                            row["SelectionStatus"] = $"{confirmedCount}/{capacity} ({pendingCount}待审核)";
                            row["IsFull"] = confirmedCount >= capacity;
                            row["EnrollmentStatus"] = confirmedCount >= capacity ? "已满" : "可选";

                            if (row["ScheduleTime"] != DBNull.Value)
                            {
                                row["ScheduleTime"] = FormatScheduleTime(row["ScheduleTime"].ToString());
                            }
                        }
                    }

                    TeachingCoursesGrid.ItemsSource = coursesTable.DefaultView;

                    // 应用当前的搜索条件
                    if (!string.IsNullOrWhiteSpace(CourseSearchBox.Text))
                    {
                        CourseSearchBox_TextChanged(null, null);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载课程数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadApprovalCourses()
        {
            if (ApprovalSemesterComboBox.SelectedValue == null) return;

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"
                SELECT DISTINCT 
                    c.CourseID,
                    CONCAT(c.CourseCode, ' - ', c.CourseName) as CourseDisplay
                FROM Course c
                WHERE c.SemesterID = @SemesterID ";

                    // 如果是教师，只显示自己的课程
                    if (GlobalUserState.Role == "Teacher")
                    {
                        query += @"
                    AND EXISTS (
                        SELECT 1 
                        FROM TeacherCourse tc 
                        WHERE tc.CourseID = c.CourseID 
                        AND tc.TeacherID = @TeacherID
                    ) ";
                    }

                    query += "ORDER BY CourseDisplay";

                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@SemesterID", ApprovalSemesterComboBox.SelectedValue);

                    if (GlobalUserState.Role == "Teacher")
                    {
                        cmd.Parameters.AddWithValue("@TeacherID", GlobalUserState.LinkedID);
                    }

                    // 确保下拉框有数据
                    var coursesTable = new DataTable();
                    using (var adapter = new SqlDataAdapter(cmd))
                    {
                        adapter.Fill(coursesTable);

                        // 添加"全部课程"选项
                        var allRow = coursesTable.NewRow();
                        allRow["CourseID"] = DBNull.Value;
                        allRow["CourseDisplay"] = "全部课程";
                        coursesTable.Rows.InsertAt(allRow, 0);
                    }

                    // 清空并重新设置数据源
                    CourseFilterComboBox.ItemsSource = null;
                    CourseFilterComboBox.Items.Clear();
                    CourseFilterComboBox.ItemsSource = coursesTable.DefaultView;
                    CourseFilterComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载课程列表失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadApprovals()
        {
            if (ApprovalSemesterComboBox.SelectedValue == null) return;

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"
                SELECT 
                    sc.Id,
                    sc.StudentID as StudentId,
                    s.Name as StudentName,
                    c.CourseID,
                    c.CourseCode,
                    c.CourseName,
                    sc.SelectionDate as ApplyTime,
                    sc.SelectionType,
                    sc.RejectReason,
                    t.TeacherID,
                    sc.ApproverID,
                    ta.Name as ApproverName,
                    sc.ApprovalDate
                FROM StudentCourse sc
                JOIN Student s ON sc.StudentID = s.StudentID
                JOIN Course c ON sc.CourseID = c.CourseID
                LEFT JOIN TeacherCourse tc ON c.CourseID = tc.CourseID
                LEFT JOIN Teacher t ON tc.TeacherID = t.TeacherID
                LEFT JOIN Teacher ta ON sc.ApproverID = ta.TeacherID
                WHERE c.SemesterID = @SemesterID ";

                    if (GlobalUserState.Role == "Teacher")
                    {
                        query += "AND tc.TeacherID = @TeacherID ";
                    }

                    query += "ORDER BY sc.SelectionDate DESC";

                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@SemesterID", ApprovalSemesterComboBox.SelectedValue);

                    if (GlobalUserState.Role == "Teacher")
                    {
                        cmd.Parameters.AddWithValue("@TeacherID", GlobalUserState.LinkedID);
                    }

                    // 将DataTable保存到类成员变量
                    approvalsTable = new DataTable();
                    using (var adapter = new SqlDataAdapter(cmd))
                    {
                        adapter.Fill(approvalsTable);
                    }

                    // 设置DataGrid的数据源为approvalsTable的DefaultView
                    ApprovalGrid.ItemsSource = approvalsTable.DefaultView;

                    // 应用筛选条件
                    ApplyApprovalFilters();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载审批数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddNewCourse_Click(object sender, RoutedEventArgs e)
        {
            var window = new CourseEditWindow(null, GlobalUserState.LinkedID);
            window.CourseUpdated += (s, args) => LoadTeachingCourses();
            window.ShowDialog();
        }

        private void EditCourse_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is DataRowView row)
            {
                // 检查教师权限
                if (GlobalUserState.Role == "Teacher")
                {
                    string teacherId = row["TeacherID"]?.ToString();
                    if (teacherId != GlobalUserState.LinkedID)
                    {
                        MessageBox.Show("您只能编辑自己的课程", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                int courseId = Convert.ToInt32(row["CourseID"]);
                var window = new CourseEditWindow(courseId, GlobalUserState.LinkedID);
                window.CourseUpdated += (s, args) => LoadTeachingCourses();
                window.ShowDialog();
            }
        }

        private void ViewStudents_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is DataRowView row)
            {
                int courseId = Convert.ToInt32(row["CourseID"]);
                string courseName = row["CourseName"].ToString();
                var window = new StudentListWindow(courseId, courseName);
                window.ShowDialog();
            }
        }

        private void AddStudentCourse_Click(object sender, RoutedEventArgs e)
        {
            if (GlobalUserState.Role != "Admin")
            {
                MessageBox.Show("只有管理员可以手动添加选课", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var window = new AddStudentCourseWindow();
            window.SelectionAdded += (s, args) =>
            {
                LoadTeachingCourses();
                LoadApprovals();
            };
            window.ShowDialog();
        }

        private void ApproveSelection_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is DataRowView row)
            {
                // 检查教师权限
                if (GlobalUserState.Role == "Teacher")
                {
                    string teacherId = row["TeacherID"]?.ToString();
                    if (teacherId != GlobalUserState.LinkedID)
                    {
                        MessageBox.Show("您只能审批自己课程的选课申请", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                // 检查选课状态
                string status = row["SelectionType"].ToString();
                if (status != "待审核")
                {
                    MessageBox.Show("只能审批待审核状态的选课申请", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                try
                {
                    using (var conn = new SqlConnection(connectionString))
                    {
                        conn.Open();

                        // 首先检查课程容量
                        var checkCmd = new SqlCommand(@"
                            SELECT 
                                c.Capacity,
                                (SELECT COUNT(*) FROM StudentCourse sc 
                                 WHERE sc.CourseID = c.CourseID 
                                 AND sc.SelectionType = '已确认') as CurrentCount
                            FROM Course c
                            WHERE c.CourseID = @CourseID", conn);

                        checkCmd.Parameters.AddWithValue("@CourseID", row["CourseID"]);
                        using (var reader = checkCmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                int capacity = reader.GetInt32(0);
                                int currentCount = reader.GetInt32(1);
                                if (currentCount >= capacity)
                                {
                                    MessageBox.Show("课程已达到容量上限，无法确认更多选课", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                                    return;
                                }
                            }
                        }

                        // 更新选课状态
                        var updateCmd = new SqlCommand(@"
                            UPDATE StudentCourse 
                            SET SelectionType = '已确认',
                                ApproverID = @ApproverID,
                                ApprovalDate = @ApprovalDate 
                            WHERE Id = @Id", conn);

                        updateCmd.Parameters.AddWithValue("@Id", row["Id"]);
                        updateCmd.Parameters.AddWithValue("@ApproverID", GlobalUserState.LinkedID);
                        updateCmd.Parameters.AddWithValue("@ApprovalDate", DateTime.Now);
                        updateCmd.ExecuteNonQuery();

                        MessageBox.Show("已确认该选课申请！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadTeachingCourses();
                        LoadApprovals();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RejectSelection_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is DataRowView row)
            {
                // 检查教师权限
                if (GlobalUserState.Role == "Teacher")
                {
                    string teacherId = row["TeacherID"]?.ToString();
                    if (teacherId != GlobalUserState.LinkedID)
                    {
                        MessageBox.Show("您只能审批自己课程的选课申请", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                // 检查选课状态
                string status = row["SelectionType"].ToString();
                if (status != "待审核")
                {
                    MessageBox.Show("只能拒绝待审核状态的选课申请", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var window = new CourseRejectWindow(Convert.ToInt32(row["Id"]));
                window.RejectConfirmed += (s, args) =>
                {
                    LoadTeachingCourses();
                    LoadApprovals();
                };
                window.ShowDialog();
            }
        }

        private void ViewDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is DataRowView row)
            {
                // 检查权限
                if (GlobalUserState.Role == "Teacher")
                {
                    string teacherId = row["TeacherID"]?.ToString();
                    if (teacherId != GlobalUserState.LinkedID)
                    {
                        MessageBox.Show("您只能查看自己课程的选课详情", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                string studentId = row["StudentId"].ToString();
                int courseId = Convert.ToInt32(row["CourseID"]);
                var window = new CourseSelectionDetailsWindow(studentId, courseId);
                window.ShowDialog();
            }
        }
    }
}