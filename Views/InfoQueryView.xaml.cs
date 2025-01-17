using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Linq;
using System.Windows.Threading;
using System.ComponentModel;

namespace UniAcamanageWpfApp.Views
{
    public partial class InfoQueryView : UserControl, INotifyPropertyChanged
    {
        private readonly string ConnectionString =
            ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;

        #region Properties

        private DateTime _queryTime;
        public DateTime QueryTime
        {
            get => _queryTime;
            set
            {
                _queryTime = value;
                OnPropertyChanged(nameof(QueryTime));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void UpdateQueryTime()
        {
            QueryTime = DateTime.Now;
        }

        #endregion

        public InfoQueryView()
        {
            InitializeComponent();
            DataContext = this;
            QueryTime = DateTime.Now;
        }

        #region 加载控制
        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowLoading();

                // 播放淡入动画
                var storyboard = (Storyboard)FindResource("FadeInStoryboard");
                if (storyboard != null)
                {
                    MainGrid.Opacity = 0;
                    storyboard.Begin();
                }

                // 加载数据
                LoadStudentBasicInfo();
                LoadSemesterComboBoxes();
                LoadCurrentExams();
                InitializeClassroomControls(); // 初始化教室查询控件
            }
            catch (Exception ex)
            {
                MessageBox.Show($"页面加载出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideLoading();
            }
        }

        private void InitializeClassroomControls()
        {
            // 设置日期选择器默认值为当前日期
            ClassroomDatePicker.SelectedDate = DateTime.Today;
            ClassroomTimeSlotComboBox.SelectedIndex = 0;
        }
        private void ShowLoading()
        {
            LoadingCard.Visibility = Visibility.Visible;
        }
        private void HideLoading()
        {
            LoadingCard.Visibility = Visibility.Collapsed;
        }
        #endregion

        #region 基本信息加载
        private void LoadStudentBasicInfo()
        {
            try
            {
                string studentID = GlobalUserState.LinkedID;
                if (string.IsNullOrEmpty(studentID))
                {
                    MessageBox.Show("未获取到学生信息！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string query = @"
            SELECT s.StudentID,
                   s.[Name],
                   s.Gender,
                   s.BirthDate,
                   s.YearOfAdmission,
                   s.Major,
                   c.ClassName,
                   s.Status
            FROM Student s
            INNER JOIN Class c ON s.ClassID = c.ClassID
            WHERE s.StudentID = @studentID";

                var dt = ExecuteQuery(query, new SqlParameter("@studentID", studentID));
                if (dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    txtStudentID.Text = row["StudentID"].ToString();
                    txtName.Text = row["Name"].ToString();
                    txtGender.Text = row["Gender"].ToString();
                    txtYearOfAdmission.Text = row["YearOfAdmission"].ToString();
                    txtMajor.Text = row["Major"].ToString();
                    txtClass.Text = row["ClassName"].ToString();
                    txtStatus.Text = row["Status"].ToString();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载学生信息失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void LoadSemesterComboBoxes()
        {
            try
            {
                string query = @"
                    SELECT s.SemesterID,
                           s.SemesterName,
                           ay.YearName,
                           CASE 
                                WHEN GETDATE() BETWEEN s.StartDate AND s.EndDate THEN 1 
                                ELSE 0 
                           END AS IsCurrent
                    FROM Semester s
                    INNER JOIN AcademicYear ay ON s.AcademicYearID = ay.AcademicYearID
                    ORDER BY s.StartDate DESC";

                var dt = ExecuteQuery(query);
                var semesterList = dt.AsEnumerable()
                    .Select(row => new
                    {
                        SemesterID = row.Field<int>("SemesterID"),
                        DisplayName = $"{row.Field<string>("YearName")} {row.Field<string>("SemesterName")}",
                        IsCurrent = row.Field<int>("IsCurrent") == 1
                    })
                    .ToList();

                // 设置个人课表的学期选择
                SemesterComboBox.ItemsSource = semesterList;
                SemesterComboBox.DisplayMemberPath = "DisplayName";
                SemesterComboBox.SelectedValuePath = "SemesterID";

                // 设置考试查询的学期选择
                ExamSemesterComboBox.ItemsSource = new List<object>(semesterList);
                ExamSemesterComboBox.DisplayMemberPath = "DisplayName";
                ExamSemesterComboBox.SelectedValuePath = "SemesterID";

                // 选择当前学期
                var currentSemester = semesterList.FirstOrDefault(s => s.IsCurrent);
                if (currentSemester != null)
                {
                    SemesterComboBox.SelectedValue = currentSemester.SemesterID;
                    ExamSemesterComboBox.SelectedValue = currentSemester.SemesterID;
                }
                else if (semesterList.Any())
                {
                    SemesterComboBox.SelectedIndex = 0;
                    ExamSemesterComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载学期信息失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region 个人课表查询
        private void QueryPersonalSchedule(object sender, RoutedEventArgs e)
        {
            if (SemesterComboBox.SelectedValue == null)
            {
                MessageBox.Show("请选择要查询的学期！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                ShowLoading();
                int semesterID = (int)SemesterComboBox.SelectedValue;
                string studentID = GlobalUserState.LinkedID;

                string query = @"
                    SELECT 
                        c.CourseCode,
                        c.CourseName,
                        c.CourseType,
                        c.Credit,
                        c.ScheduleTime,
                        cr.RoomNumber as Classroom,
                        t.Name as TeacherName
                    FROM StudentCourse sc
                    INNER JOIN Course c ON sc.CourseID = c.CourseID
                    LEFT JOIN Classroom cr ON c.ClassroomID = cr.ClassroomID
                    LEFT JOIN TeacherCourse tc ON c.CourseID = tc.CourseID
                    LEFT JOIN Teacher t ON tc.TeacherID = t.TeacherID
                    WHERE sc.StudentID = @studentID
                    AND c.SemesterID = @semesterID
                    ORDER BY c.CourseCode";

                var parameters = new[]
                {
                    new SqlParameter("@studentID", studentID),
                    new SqlParameter("@semesterID", semesterID)
                };

                var dt = ExecuteQuery(query, parameters);
                ScheduleDataGrid.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"查询课表失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideLoading();
            }
        }
        #endregion

        #region 课程查询
        private void CourseSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = CourseSearchTextBox.Text.Trim();
            if (string.IsNullOrEmpty(searchText))
            {
                CourseSearchResultListBox.Visibility = Visibility.Collapsed;
                return;
            }

            try
            {
                string query = @"
                    SELECT TOP 10
                        c.CourseCode,
                        c.CourseName,
                        t.Name as TeacherName
                    FROM Course c
                    LEFT JOIN TeacherCourse tc ON c.CourseID = tc.CourseID
                    LEFT JOIN Teacher t ON tc.TeacherID = t.TeacherID
                    WHERE c.SemesterID = (
                        SELECT TOP 1 SemesterID 
                        FROM Semester 
                        WHERE GETDATE() BETWEEN StartDate AND EndDate
                    )
                    AND (c.CourseCode LIKE @searchText 
                         OR c.CourseName LIKE @searchText)
                    ORDER BY c.CourseCode";

                var dt = ExecuteQuery(query,
                    new SqlParameter("@searchText", $"%{searchText}%"));

                if (dt.Rows.Count > 0)
                {
                    CourseSearchResultListBox.ItemsSource = dt.AsEnumerable()
                        .Select(row => $"{row["CourseCode"]} - {row["CourseName"]} ({row["TeacherName"]})")
                        .ToList();
                    CourseSearchResultListBox.Visibility = Visibility.Visible;
                }
                else
                {
                    CourseSearchResultListBox.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"搜索课程时出错: {ex.Message}");
            }
        }

        private void CourseSearchResultListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CourseSearchResultListBox.SelectedItem != null)
            {
                string selected = CourseSearchResultListBox.SelectedItem.ToString();
                string courseCode = selected.Split('-')[0].Trim();
                CourseSearchTextBox.Text = courseCode;
                CourseSearchResultListBox.Visibility = Visibility.Collapsed;
                QueryCourseSchedule(null, null);
            }
        }

        private void QueryCourseSchedule(object sender, RoutedEventArgs e)
        {
            string courseCode = CourseSearchTextBox.Text.Trim();
            if (string.IsNullOrEmpty(courseCode))
            {
                MessageBox.Show("请输入课程代码或名称！");
                return;
            }

            try
            {
                ShowLoading();
                string query = @"
                    SELECT 
                        c.CourseCode,
                        c.CourseName,
                        c.CourseType,
                        c.Credit,
                        c.ScheduleTime,
                        cr.RoomNumber as Classroom,
                        t.Name as TeacherName,
                        c.Capacity,
                        (SELECT COUNT(*) FROM StudentCourse sc WHERE sc.CourseID = c.CourseID) as EnrolledCount
                    FROM Course c
                    LEFT JOIN Classroom cr ON c.ClassroomID = cr.ClassroomID
                    LEFT JOIN TeacherCourse tc ON c.CourseID = tc.CourseID
                    LEFT JOIN Teacher t ON tc.TeacherID = t.TeacherID
                    WHERE c.CourseCode LIKE @courseCode
                    AND c.SemesterID = (
                        SELECT TOP 1 SemesterID 
                        FROM Semester 
                        WHERE GETDATE() BETWEEN StartDate AND EndDate
                    )";

                var dt = ExecuteQuery(query,
                    new SqlParameter("@courseCode", $"%{courseCode}%"));

                CourseScheduleDataGrid.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"查询课程失败: {ex.Message}");
            }
            finally
            {
                HideLoading();
            }
        }
        #endregion
        private void SemesterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (SemesterComboBox.SelectedValue != null)
                {
                    UpdateQueryTime(); // 更新查询时间
                    QueryPersonalSchedule(sender, null); // 自动触发课表查询
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"切换学期时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region 教师课表查询
        private void TeacherSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = TeacherIDTextBox.Text.Trim();
            if (string.IsNullOrEmpty(searchText))
            {
                TeacherSearchResultListBox.Visibility = Visibility.Collapsed;
                return;
            }

            try
            {
                string query = @"
                    SELECT TOP 10
                        t.TeacherID,
                        t.Name,
                        t.Title,
                        d.DepartmentName
                    FROM Teacher t
                    LEFT JOIN Department d ON t.DepartmentID = d.DepartmentID
                    WHERE t.TeacherID LIKE @searchText 
                          OR t.Name LIKE @searchText
                    ORDER BY t.TeacherID";

                var dt = ExecuteQuery(query,
                    new SqlParameter("@searchText", $"%{searchText}%"));

                if (dt.Rows.Count > 0)
                {
                    TeacherSearchResultListBox.ItemsSource = dt.AsEnumerable()
                        .Select(row => $"{row["TeacherID"]} - {row["Name"]} ({row["Title"]}) - {row["DepartmentName"]}")
                        .ToList();
                    TeacherSearchResultListBox.Visibility = Visibility.Visible;
                }
                else
                {
                    TeacherSearchResultListBox.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"搜索教师时出错: {ex.Message}");
            }
        }

        private void QueryTeacherSchedule(object sender, RoutedEventArgs e)
        {
            string teacherSearch = TeacherIDTextBox.Text.Trim();
            if (string.IsNullOrEmpty(teacherSearch))
            {
                MessageBox.Show("请输入教师工号或姓名！");
                return;
            }

            try
            {
                ShowLoading();
                UpdateQueryTime(); // 更新查询时间

                string query = @"
                    SELECT 
                        t.TeacherID,
                        t.Name as TeacherName,
                        c.CourseCode,
                        c.CourseName,
                        c.ScheduleTime,
                        cr.RoomNumber as Classroom,
                        (SELECT COUNT(*) FROM StudentCourse sc WHERE sc.CourseID = c.CourseID) as StudentCount,
                        c.Capacity
                    FROM Teacher t
                    INNER JOIN TeacherCourse tc ON t.TeacherID = tc.TeacherID
                    INNER JOIN Course c ON tc.CourseID = c.CourseID
                    LEFT JOIN Classroom cr ON c.ClassroomID = cr.ClassroomID
                    WHERE (t.TeacherID LIKE @searchText OR t.Name LIKE @searchText)
                    AND c.SemesterID = (
                        SELECT TOP 1 SemesterID 
                        FROM Semester 
                        WHERE GETDATE() BETWEEN StartDate AND EndDate
                    )
                    ORDER BY c.ScheduleTime";

                var dt = ExecuteQuery(query,
                    new SqlParameter("@searchText", $"%{teacherSearch}%"));

                TeacherScheduleDataGrid.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"查询教师课表失败: {ex.Message}");
            }
            finally
            {
                HideLoading();
            }
        }
        #endregion

        private void TeacherSearchResultListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TeacherSearchResultListBox.SelectedItem != null)
            {
                try
                {
                    string selected = TeacherSearchResultListBox.SelectedItem.ToString();
                    string teacherId = selected.Split('-')[0].Trim(); // 提取教师ID
                    TeacherIDTextBox.Text = teacherId;
                    TeacherSearchResultListBox.Visibility = Visibility.Collapsed;

                    // 更新查询时间
                    UpdateQueryTime();

                    // 自动触发查询
                    QueryTeacherSchedule(null, null);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"选择教师时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #region 教室查询
        private void QueryClassroom(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowLoading();
                UpdateQueryTime(); // 更新查询时间

                DateTime queryDate = ClassroomDatePicker.SelectedDate ?? DateTime.Today;
                string timeSlot = ((ComboBoxItem)ClassroomTimeSlotComboBox.SelectedItem)?.Content.ToString() ?? "全天";

                string timeCondition = GetTimeCondition(timeSlot);

                string query = $@"
                    SELECT 
                        cr.RoomNumber,
                        cr.Floor,
                        cr.Capacity,
                        cr.SpatialLocation,
                        CASE 
                            WHEN c.CourseID IS NULL THEN '空闲'
                            ELSE '占用'
                        END AS CurrentStatus,
                        COALESCE(c.CourseName, '') as CurrentCourse,
                        COALESCE(c.ScheduleTime, '') as ScheduleTime
                    FROM Classroom cr
                    LEFT JOIN Course c ON cr.ClassroomID = c.ClassroomID
                        AND c.SemesterID = (
                            SELECT TOP 1 SemesterID 
                            FROM Semester 
                            WHERE @queryDate BETWEEN StartDate AND EndDate
                        )
                        {timeCondition}
                    ORDER BY cr.Floor, cr.RoomNumber";

                var dt = ExecuteQuery(query, new SqlParameter("@queryDate", queryDate));
                ClassroomDataGrid.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"查询教室使用情况失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideLoading();
            }
        }

        private string GetTimeCondition(string timeSlot)
        {
            return timeSlot switch
            {
                "上午（第1-4节）" => "AND c.ScheduleTime LIKE '%1-%' OR c.ScheduleTime LIKE '%2-%' OR c.ScheduleTime LIKE '%3-%' OR c.ScheduleTime LIKE '%4-%'",
                "下午（第5-8节）" => "AND c.ScheduleTime LIKE '%5-%' OR c.ScheduleTime LIKE '%6-%' OR c.ScheduleTime LIKE '%7-%' OR c.ScheduleTime LIKE '%8-%'",
                "晚上（第9-11节）" => "AND c.ScheduleTime LIKE '%9-%' OR c.ScheduleTime LIKE '%10-%' OR c.ScheduleTime LIKE '%11-%'",
                _ => "" // 全天
            };
        }
        #endregion

        #region 考试信息查询
        private void QueryExam(object sender, RoutedEventArgs e)
        {
            if (ExamSemesterComboBox.SelectedValue == null)
            {
                MessageBox.Show("请选择要查询的学期！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                ShowLoading();
                int semesterID = (int)ExamSemesterComboBox.SelectedValue;
                string studentID = GlobalUserState.LinkedID; // 获取当前登录学生ID
                string courseCode = ExamCodeTextBox.Text.Trim();

                string query = @"
                    SELECT 
                        c.CourseCode,
                        c.CourseName,
                        e.ExamDate,
                        e.ExamLocation,
                        e.Duration as '考试时长(分钟)',
                        e.ExamType,
                        t.Name as Invigilator,
                        e.BatchNumber as '考试批次',
                        CASE 
                            WHEN e.ExamDate > GETDATE() THEN '未开始'
                            WHEN e.ExamDate <= GETDATE() AND DATEADD(MINUTE, e.Duration, e.ExamDate) > GETDATE() THEN '进行中'
                            ELSE '已结束'
                        END as ExamStatus
                    FROM Exam e
                    INNER JOIN Course c ON e.CourseID = c.CourseID
                    INNER JOIN StudentCourse sc ON c.CourseID = sc.CourseID
                    LEFT JOIN Teacher t ON e.InvigilatorID = t.TeacherID
                    WHERE sc.StudentID = @studentID
                    AND c.SemesterID = @semesterID";

                if (!string.IsNullOrEmpty(courseCode))
                {
                    query += " AND c.CourseCode LIKE @courseCode";
                }

                query += @" ORDER BY 
                            CASE 
                                WHEN e.ExamDate > GETDATE() THEN 1
                                WHEN e.ExamDate <= GETDATE() AND DATEADD(MINUTE, e.Duration, e.ExamDate) > GETDATE() THEN 2
                                ELSE 3
                            END,
                            e.ExamDate";

                var parameters = new List<SqlParameter>
                {
                    new SqlParameter("@studentID", studentID),
                    new SqlParameter("@semesterID", semesterID)
                };

                if (!string.IsNullOrEmpty(courseCode))
                {
                    parameters.Add(new SqlParameter("@courseCode", $"%{courseCode}%"));
                }

                var dt = ExecuteQuery(query, parameters.ToArray());
                ExamDataGrid.ItemsSource = dt.DefaultView;

                // 检查是否有即将到来的考试（24小时内）
                var upcomingExams = dt.AsEnumerable()
                    .Where(row =>
                    {
                        var examDate = row.Field<DateTime>("ExamDate");
                        var timeUntilExam = examDate - DateTime.Now;
                        return timeUntilExam > TimeSpan.Zero && timeUntilExam <= TimeSpan.FromHours(24);
                    })
                    .ToList();

                if (upcomingExams.Any())
                {
                    var message = "您有以下即将到来的考试：\n\n";
                    foreach (var exam in upcomingExams)
                    {
                        message += $"课程：{exam["CourseName"]}\n" +
                                 $"时间：{((DateTime)exam["ExamDate"]).ToString("yyyy-MM-dd HH:mm")}\n" +
                                 $"地点：{exam["ExamLocation"]}\n\n";
                    }
                    MessageBox.Show(message, "考试提醒", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"查询考试信息失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideLoading();
            }
        }

        private void LoadCurrentExams()
        {
            // 在页面加载时自动显示当前学期的考试信息
            if (ExamSemesterComboBox.SelectedValue != null)
            {
                QueryExam(null, null);
            }
        }
        #endregion

        private void ExamSemesterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (ExamSemesterComboBox.SelectedValue != null)
                {
                    UpdateQueryTime(); // 更新查询时间
                    QueryExam(sender, null); // 自动触发考试信息查询
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"切换学期时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region 通用数据库操作
        private DataTable ExecuteQuery(string query, params SqlParameter[] parameters)
        {
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (parameters != null)
                    {
                        cmd.Parameters.AddRange(parameters);
                    }

                    DataTable dt = new DataTable();
                    try
                    {
                        conn.Open();
                        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                        {
                            adapter.Fill(dt);
                        }
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                    return dt;
                }
            }
        }
        #endregion

        #region 查询时间更新
        private void UpdateQueryTimeForAll()
        {
            // 在每次查询操作时调用此方法
            UpdateQueryTime();
        }
        #endregion
    }
}