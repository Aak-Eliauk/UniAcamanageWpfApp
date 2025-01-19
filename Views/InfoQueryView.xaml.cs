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
using UniAcamanageWpfApp.Utils.Schedule;
using System.Windows.Input;
using System.Windows.Data;
using System.Diagnostics;

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
                // 先获取当前学期
                var currentSemester = GetCurrentSemester(DateTime.Now);
                if (currentSemester == null)
                {
                    MessageBox.Show("获取当前学期信息失败！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string query = @"
            SELECT 
                s.SemesterID,
                s.SemesterName,
                ay.YearName,
                s.StartDate,
                s.EndDate
            FROM Semester s
            INNER JOIN AcademicYear ay ON s.AcademicYearID = ay.AcademicYearID
            ORDER BY s.StartDate DESC";

                var dt = ExecuteQuery(query);
                var semesterList = dt.AsEnumerable()
                    .Select(row => new
                    {
                        SemesterID = row.Field<int>("SemesterID"),
                        DisplayName = $"{row.Field<string>("YearName")} {row.Field<string>("SemesterName")}",
                        IsCurrent = row.Field<int>("SemesterID") == currentSemester.SemesterID
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
                SemesterComboBox.SelectedValue = currentSemester.SemesterID;
                ExamSemesterComboBox.SelectedValue = currentSemester.SemesterID;

                // 如果没有选中任何学期，则选择第一个
                if (SemesterComboBox.SelectedValue == null && semesterList.Any())
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
            WITH ParsedSchedule AS (
                SELECT 
                    c.CourseID,
                    c.CourseCode,
                    c.CourseName,
                    c.CourseType,
                    c.Credit,
                    cr.RoomNumber as Classroom,
                    t.Name as TeacherName,
                    value AS SingleSchedule
                FROM Course c
                CROSS APPLY STRING_SPLIT(c.ScheduleTime, ',')
                LEFT JOIN Classroom cr ON c.ClassroomID = cr.ClassroomID
                LEFT JOIN TeacherCourse tc ON c.CourseID = tc.CourseID
                LEFT JOIN Teacher t ON tc.TeacherID = t.TeacherID
                WHERE c.SemesterID = @semesterID
            )
            SELECT 
                ps.CourseCode,
                ps.CourseName,
                ps.CourseType,
                ps.Credit,
                STUFF((
                    SELECT CHAR(13) + CHAR(10) + 
                           CASE LEFT(p.SingleSchedule, 1)
                                WHEN '1' THEN '周一'
                                WHEN '2' THEN '周二'
                                WHEN '3' THEN '周三'
                                WHEN '4' THEN '周四'
                                WHEN '5' THEN '周五'
                                WHEN '6' THEN '周六'
                                WHEN '7' THEN '周日'
                           END + 
                           ' 第' + 
                           SUBSTRING(p.SingleSchedule, 
                                CHARINDEX('-', p.SingleSchedule) + 1,
                                CHARINDEX('-', p.SingleSchedule, CHARINDEX('-', p.SingleSchedule) + 1) - 
                                CHARINDEX('-', p.SingleSchedule) - 1) +
                           '-' +
                           SUBSTRING(p.SingleSchedule,
                                CHARINDEX('-', p.SingleSchedule, CHARINDEX('-', p.SingleSchedule) + 1) + 1,
                                CHARINDEX('-', p.SingleSchedule, CHARINDEX('-', p.SingleSchedule, 
                                    CHARINDEX('-', p.SingleSchedule) + 1) + 1) - 
                                CHARINDEX('-', p.SingleSchedule, CHARINDEX('-', p.SingleSchedule) + 1) - 1) +
                           '节 第' +
                           SUBSTRING(p.SingleSchedule,
                                CHARINDEX('-', p.SingleSchedule, CHARINDEX('-', p.SingleSchedule, 
                                    CHARINDEX('-', p.SingleSchedule) + 1) + 1) + 1,
                                CHARINDEX('-', p.SingleSchedule, CHARINDEX('-', p.SingleSchedule, 
                                    CHARINDEX('-', p.SingleSchedule, CHARINDEX('-', p.SingleSchedule) + 1) + 1) + 1) - 
                                CHARINDEX('-', p.SingleSchedule, CHARINDEX('-', p.SingleSchedule, 
                                    CHARINDEX('-', p.SingleSchedule) + 1) + 1) - 1) +
                           '-' +
                           CASE 
                                WHEN CHARINDEX('A', p.SingleSchedule) > 0 OR CHARINDEX('B', p.SingleSchedule) > 0
                                THEN SUBSTRING(p.SingleSchedule,
                                    CHARINDEX('-', p.SingleSchedule, CHARINDEX('-', p.SingleSchedule, 
                                        CHARINDEX('-', p.SingleSchedule, CHARINDEX('-', p.SingleSchedule) + 1) + 1) + 1) + 1,
                                    CHARINDEX('A', p.SingleSchedule + 'A') - 
                                    CHARINDEX('-', p.SingleSchedule, CHARINDEX('-', p.SingleSchedule, 
                                        CHARINDEX('-', p.SingleSchedule, CHARINDEX('-', p.SingleSchedule) + 1) + 1) + 1) - 1)
                                ELSE SUBSTRING(p.SingleSchedule,
                                    CHARINDEX('-', p.SingleSchedule, CHARINDEX('-', p.SingleSchedule, 
                                        CHARINDEX('-', p.SingleSchedule, CHARINDEX('-', p.SingleSchedule) + 1) + 1) + 1) + 1,
                                    LEN(p.SingleSchedule) - 
                                    CHARINDEX('-', p.SingleSchedule, CHARINDEX('-', p.SingleSchedule, 
                                        CHARINDEX('-', p.SingleSchedule, CHARINDEX('-', p.SingleSchedule) + 1) + 1) + 1))
                           END +
                           '周' +
                           CASE RIGHT(p.SingleSchedule, 1)
                                WHEN 'A' THEN ' [单周]'
                                WHEN 'B' THEN ' [双周]'
                                ELSE ''
                           END
                    FROM ParsedSchedule p
                    WHERE p.CourseID = ps.CourseID
                    FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS ClassTime,
                ps.Classroom,
                ps.TeacherName
            FROM ParsedSchedule ps
            INNER JOIN StudentCourse sc ON ps.CourseID = sc.CourseID
            WHERE sc.StudentID = @studentID
            GROUP BY 
                ps.CourseID,
                ps.CourseCode,
                ps.CourseName,
                ps.CourseType,
                ps.Credit,
                ps.Classroom,
                ps.TeacherName
            ORDER BY ps.CourseCode";

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
                // 保持原有的模糊查询SQL
                string query = @"
            WITH CurrentSemester AS (
                SELECT TOP 1 SemesterID
                FROM Semester 
                WHERE GETDATE() BETWEEN StartDate AND EndDate
                ORDER BY StartDate DESC
            )
            SELECT TOP 10
                c.CourseCode,
                c.CourseName,
                c.CourseType,
                STRING_AGG(t.Name, '/') as TeacherNames,
                c.Credit,
                s.SemesterName,
                ay.YearName,
                CONCAT(
                    (SELECT COUNT(*) FROM StudentCourse sc WHERE sc.CourseID = c.CourseID),
                    '/',
                    c.Capacity
                ) as EnrollmentStatus,
                CASE 
                    WHEN c.SemesterID = (SELECT SemesterID FROM CurrentSemester) THEN '(当前学期)'
                    ELSE ''
                END as CurrentSemesterFlag
            FROM Course c
            INNER JOIN Semester s ON c.SemesterID = s.SemesterID
            INNER JOIN AcademicYear ay ON s.AcademicYearID = ay.AcademicYearID
            LEFT JOIN TeacherCourse tc ON c.CourseID = tc.CourseID
            LEFT JOIN Teacher t ON tc.TeacherID = t.TeacherID
            WHERE (
                c.CourseCode LIKE @searchText 
                OR c.CourseName LIKE @searchText
                OR t.Name LIKE @searchText
            )
            GROUP BY 
                c.CourseID,
                c.CourseCode,
                c.CourseName,
                c.CourseType,
                c.Credit,
                c.Capacity,
                s.SemesterName,
                ay.YearName,
                c.SemesterID
            ORDER BY 
                CASE WHEN c.SemesterID = (SELECT SemesterID FROM CurrentSemester) THEN 0 ELSE 1 END,
                s.SemesterName DESC,
                CASE 
                    WHEN c.CourseCode LIKE @searchText THEN 1
                    WHEN c.CourseName LIKE @searchText THEN 2
                    ELSE 3
                END,
                c.CourseCode";

                var dt = ExecuteQuery(query,
                    new SqlParameter("@searchText", $"%{searchText}%"));

                if (dt.Rows.Count > 0)
                {
                    CourseSearchResultListBox.ItemsSource = dt.AsEnumerable()
                        .Select(row => new
                        {
                            DisplayText = $"{row["CourseCode"]} - {row["CourseName"]}",
                            CourseCode = row["CourseCode"].ToString()
                        })
                        .ToList();
                    CourseSearchResultListBox.DisplayMemberPath = "DisplayText";
                    CourseSearchResultListBox.SelectedValuePath = "CourseCode";
                    CourseSearchResultListBox.Visibility = Visibility.Visible;
                }
                else
                {
                    CourseSearchResultListBox.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"搜索课程时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 添加选择事件处理
        private void CourseSearchResultListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CourseSearchResultListBox.SelectedItem != null)
            {
                try
                {
                    dynamic selected = CourseSearchResultListBox.SelectedItem;
                    string courseCode = selected.CourseCode;

                    // 更新搜索框文本
                    CourseSearchTextBox.Text = courseCode;

                    // 隐藏搜索结果
                    CourseSearchResultListBox.Visibility = Visibility.Collapsed;

                    // 执行查询
                    QueryCourseSchedule(null, null);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"选择课程时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }


        private void QueryCourseSchedule(object sender, RoutedEventArgs e)
        {
            string courseCode = CourseSearchTextBox.Text.Trim();
            if (string.IsNullOrEmpty(courseCode))
            {
                MessageBox.Show("请输入课程代码或名称！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                ShowLoading();
                string query = @"
            WITH SplitSchedule AS (
                SELECT 
                    c.CourseID,
                    c.CourseCode,
                    c.CourseName,
                    c.CourseType,
                    c.Credit,
                    c.Capacity,
                    c.ClassroomID,
                    c.SemesterID,
                    value AS SingleSchedule
                FROM Course c
                CROSS APPLY STRING_SPLIT(c.ScheduleTime, ',')
            )
            SELECT 
                s.CourseCode,
                s.CourseName,
                s.CourseType,
                s.Credit,
                STUFF((
                    SELECT CHAR(13) + CHAR(10) + 
                           CASE LEFT(sp2.SingleSchedule, 1)
                                WHEN '1' THEN '周一'
                                WHEN '2' THEN '周二'
                                WHEN '3' THEN '周三'
                                WHEN '4' THEN '周四'
                                WHEN '5' THEN '周五'
                                WHEN '6' THEN '周六'
                                WHEN '7' THEN '周日'
                           END + 
                           ' 第' + 
                           SUBSTRING(sp2.SingleSchedule, 
                                CHARINDEX('-', sp2.SingleSchedule) + 1,
                                CHARINDEX('-', sp2.SingleSchedule, CHARINDEX('-', sp2.SingleSchedule) + 1) - 
                                CHARINDEX('-', sp2.SingleSchedule) - 1) +
                           '-' +
                           SUBSTRING(sp2.SingleSchedule,
                                CHARINDEX('-', sp2.SingleSchedule, CHARINDEX('-', sp2.SingleSchedule) + 1) + 1,
                                CHARINDEX('-', sp2.SingleSchedule, CHARINDEX('-', sp2.SingleSchedule, 
                                    CHARINDEX('-', sp2.SingleSchedule) + 1) + 1) - 
                                CHARINDEX('-', sp2.SingleSchedule, CHARINDEX('-', sp2.SingleSchedule) + 1) - 1) +
                           '节 第' +
                           SUBSTRING(sp2.SingleSchedule,
                                CHARINDEX('-', sp2.SingleSchedule, CHARINDEX('-', sp2.SingleSchedule, 
                                    CHARINDEX('-', sp2.SingleSchedule) + 1) + 1) + 1,
                                CHARINDEX('-', sp2.SingleSchedule, CHARINDEX('-', sp2.SingleSchedule, 
                                    CHARINDEX('-', sp2.SingleSchedule, CHARINDEX('-', sp2.SingleSchedule) + 1) + 1) + 1) - 
                                CHARINDEX('-', sp2.SingleSchedule, CHARINDEX('-', sp2.SingleSchedule, 
                                    CHARINDEX('-', sp2.SingleSchedule) + 1) + 1) - 1) +
                           '-' +
                           CASE 
                                WHEN CHARINDEX('A', sp2.SingleSchedule) > 0 OR CHARINDEX('B', sp2.SingleSchedule) > 0
                                THEN SUBSTRING(sp2.SingleSchedule,
                                    CHARINDEX('-', sp2.SingleSchedule, CHARINDEX('-', sp2.SingleSchedule, 
                                        CHARINDEX('-', sp2.SingleSchedule, CHARINDEX('-', sp2.SingleSchedule) + 1) + 1) + 1) + 1,
                                    CHARINDEX('A', sp2.SingleSchedule + 'A') - 
                                    CHARINDEX('-', sp2.SingleSchedule, CHARINDEX('-', sp2.SingleSchedule, 
                                        CHARINDEX('-', sp2.SingleSchedule, CHARINDEX('-', sp2.SingleSchedule) + 1) + 1) + 1) - 1)
                                ELSE SUBSTRING(sp2.SingleSchedule,
                                    CHARINDEX('-', sp2.SingleSchedule, CHARINDEX('-', sp2.SingleSchedule, 
                                        CHARINDEX('-', sp2.SingleSchedule, CHARINDEX('-', sp2.SingleSchedule) + 1) + 1) + 1) + 1,
                                    LEN(sp2.SingleSchedule) - 
                                    CHARINDEX('-', sp2.SingleSchedule, CHARINDEX('-', sp2.SingleSchedule, 
                                        CHARINDEX('-', sp2.SingleSchedule, CHARINDEX('-', sp2.SingleSchedule) + 1) + 1) + 1))
                           END +
                           '周' +
                           CASE RIGHT(sp2.SingleSchedule, 1)
                                WHEN 'A' THEN ' [单周]'
                                WHEN 'B' THEN ' [双周]'
                                ELSE ''
                           END
                    FROM SplitSchedule sp2
                    WHERE sp2.CourseID = s.CourseID
                    FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS ClassTime,
                cr.RoomNumber as Classroom,
                STUFF((
                    SELECT '/' + t2.Name
                    FROM TeacherCourse tc2
                    JOIN Teacher t2 ON tc2.TeacherID = t2.TeacherID
                    WHERE tc2.CourseID = s.CourseID
                    FOR XML PATH('')), 1, 1, '') as TeacherName,
                CONCAT(s.Capacity, ' (已选', 
                    (SELECT COUNT(*) FROM StudentCourse sc WHERE sc.CourseID = s.CourseID),
                    '人)') as Capacity,
                CONCAT(ay.YearName, ' ', sem.SemesterName) as Semester,
                sem.StartDate
            FROM SplitSchedule s
            INNER JOIN Semester sem ON s.SemesterID = sem.SemesterID
            INNER JOIN AcademicYear ay ON sem.AcademicYearID = ay.AcademicYearID
            LEFT JOIN Classroom cr ON s.ClassroomID = cr.ClassroomID
            WHERE s.CourseCode LIKE @courseCode
               OR s.CourseName LIKE @courseCode
            GROUP BY 
                s.CourseID,
                s.CourseCode,
                s.CourseName,
                s.CourseType,
                s.Credit,
                s.Capacity,
                cr.RoomNumber,
                ay.YearName,
                sem.SemesterName,
                sem.StartDate
            ORDER BY sem.StartDate DESC";

                var dt = ExecuteQuery(query,
                    new SqlParameter("@courseCode", $"%{courseCode}%"));

                if (dt.Rows.Count == 0)
                {
                    MessageBox.Show("未找到相关课程信息！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    CourseScheduleDataGrid.ItemsSource = null;
                    return;
                }

                // 在设置 ItemsSource 之前清除现有的数据
                CourseScheduleDataGrid.ItemsSource = null;
                CourseScheduleDataGrid.Items.Clear();

                // 重新设置 ItemsSource
                CourseScheduleDataGrid.ItemsSource = dt.DefaultView;

                // 强制刷新 DataGrid
                CourseScheduleDataGrid.UpdateLayout();

                // 如果需要，可以手动设置每行的高度
                foreach (var item in CourseScheduleDataGrid.Items)
                {
                    var row = (DataGridRow)CourseScheduleDataGrid.ItemContainerGenerator
                        .ContainerFromItem(item);
                    if (row != null)
                    {
                        row.Height = Double.NaN; // Auto
                        row.UpdateLayout();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"查询课程失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show("请输入教师工号或姓名！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                ShowLoading();
                UpdateQueryTime();

                string query = @"
            WITH ParsedSchedule AS (
                SELECT 
                    c.CourseID,
                    value AS SingleSchedule
                FROM Course c
                CROSS APPLY STRING_SPLIT(c.ScheduleTime, ',')
            )
            SELECT 
                t.TeacherID,
                t.Name as TeacherName,
                c.CourseCode,
                c.CourseName,
                STUFF((
                    SELECT CHAR(13) + CHAR(10) + 
                           CASE LEFT(ps.SingleSchedule, 1)
                                WHEN '1' THEN '周一'
                                WHEN '2' THEN '周二'
                                WHEN '3' THEN '周三'
                                WHEN '4' THEN '周四'
                                WHEN '5' THEN '周五'
                                WHEN '6' THEN '周六'
                                WHEN '7' THEN '周日'
                           END + 
                           ' 第' + 
                           SUBSTRING(ps.SingleSchedule, 
                                CHARINDEX('-', ps.SingleSchedule) + 1,
                                CHARINDEX('-', ps.SingleSchedule, CHARINDEX('-', ps.SingleSchedule) + 1) - 
                                CHARINDEX('-', ps.SingleSchedule) - 1) +
                           '-' +
                           SUBSTRING(ps.SingleSchedule,
                                CHARINDEX('-', ps.SingleSchedule, CHARINDEX('-', ps.SingleSchedule) + 1) + 1,
                                CHARINDEX('-', ps.SingleSchedule, CHARINDEX('-', ps.SingleSchedule, 
                                    CHARINDEX('-', ps.SingleSchedule) + 1) + 1) - 
                                CHARINDEX('-', ps.SingleSchedule, CHARINDEX('-', ps.SingleSchedule) + 1) - 1) +
                           '节 第' +
                           SUBSTRING(ps.SingleSchedule,
                                CHARINDEX('-', ps.SingleSchedule, CHARINDEX('-', ps.SingleSchedule, 
                                    CHARINDEX('-', ps.SingleSchedule) + 1) + 1) + 1,
                                CHARINDEX('-', ps.SingleSchedule, CHARINDEX('-', ps.SingleSchedule, 
                                    CHARINDEX('-', ps.SingleSchedule, CHARINDEX('-', ps.SingleSchedule) + 1) + 1) + 1) - 
                                CHARINDEX('-', ps.SingleSchedule, CHARINDEX('-', ps.SingleSchedule, 
                                    CHARINDEX('-', ps.SingleSchedule) + 1) + 1) - 1) +
                           '-' +
                           CASE 
                                WHEN CHARINDEX('A', ps.SingleSchedule) > 0 OR CHARINDEX('B', ps.SingleSchedule) > 0
                                THEN SUBSTRING(ps.SingleSchedule,
                                    CHARINDEX('-', ps.SingleSchedule, CHARINDEX('-', ps.SingleSchedule, 
                                        CHARINDEX('-', ps.SingleSchedule, CHARINDEX('-', ps.SingleSchedule) + 1) + 1) + 1) + 1,
                                    CHARINDEX('A', ps.SingleSchedule + 'A') - 
                                    CHARINDEX('-', ps.SingleSchedule, CHARINDEX('-', ps.SingleSchedule, 
                                        CHARINDEX('-', ps.SingleSchedule, CHARINDEX('-', ps.SingleSchedule) + 1) + 1) + 1) - 1)
                                ELSE SUBSTRING(ps.SingleSchedule,
                                    CHARINDEX('-', ps.SingleSchedule, CHARINDEX('-', ps.SingleSchedule, 
                                        CHARINDEX('-', ps.SingleSchedule, CHARINDEX('-', ps.SingleSchedule) + 1) + 1) + 1) + 1,
                                    LEN(ps.SingleSchedule) - 
                                    CHARINDEX('-', ps.SingleSchedule, CHARINDEX('-', ps.SingleSchedule, 
                                        CHARINDEX('-', ps.SingleSchedule, CHARINDEX('-', ps.SingleSchedule) + 1) + 1) + 1))
                           END +
                           '周' +
                           CASE RIGHT(ps.SingleSchedule, 1)
                                WHEN 'A' THEN ' [单周]'
                                WHEN 'B' THEN ' [双周]'
                                ELSE ''
                           END
                    FROM ParsedSchedule ps
                    WHERE ps.CourseID = c.CourseID
                    FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS ClassTime,
                cr.RoomNumber as Classroom,
                CONCAT((SELECT COUNT(*) FROM StudentCourse sc WHERE sc.CourseID = c.CourseID), 
                       '/', c.Capacity) as StudentCount,
                d.DepartmentName,
                CONCAT(ay.YearName, ' ', s.SemesterName) as Semester
            FROM Teacher t
            INNER JOIN TeacherCourse tc ON t.TeacherID = tc.TeacherID
            INNER JOIN Course c ON tc.CourseID = c.CourseID
            INNER JOIN Department d ON t.DepartmentID = d.DepartmentID
            INNER JOIN Semester s ON c.SemesterID = s.SemesterID
            INNER JOIN AcademicYear ay ON s.AcademicYearID = ay.AcademicYearID
            LEFT JOIN Classroom cr ON c.ClassroomID = cr.ClassroomID
            WHERE (t.TeacherID LIKE @searchText OR t.Name LIKE @searchText)
            GROUP BY 
                t.TeacherID,
                t.Name,
                c.CourseID,
                c.CourseCode,
                c.CourseName,
                cr.RoomNumber,
                c.Capacity,
                d.DepartmentName,
                ay.YearName,
                s.SemesterName,
                s.StartDate
            ORDER BY s.StartDate DESC, c.CourseCode";

                var dt = ExecuteQuery(query,
                    new SqlParameter("@searchText", $"%{teacherSearch}%"));

                if (dt.Rows.Count == 0)
                {
                    MessageBox.Show("未找到相关教师课表信息！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    TeacherScheduleDataGrid.ItemsSource = null;
                    return;
                }

                TeacherScheduleDataGrid.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"查询教师课表失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                UpdateQueryTime();

                DateTime queryDate = ClassroomDatePicker.SelectedDate ?? DateTime.Today;
                string timeSlot = ((ComboBoxItem)ClassroomTimeSlotComboBox.SelectedItem)?.Content.ToString() ?? "全天";

                // 1. 计算当前周次和星期几
                var semester = GetCurrentSemester(queryDate);
                if (semester == null)
                {
                    MessageBox.Show("所选日期不在任何学期内！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 修正周次计算逻辑
                int weekNumber = (int)Math.Ceiling((queryDate - semester.StartDate).TotalDays / 7.0);
                int weekDay = (int)queryDate.DayOfWeek;
                weekDay = weekDay == 0 ? 7 : weekDay; // 将周日的0转换为7
                bool isOddWeek = weekNumber % 2 == 1;

                // 根据时间段设置节次范围
                int startSection = 1, endSection = 11;
                switch (timeSlot)
                {
                    case "上午（第1-4节）":
                        startSection = 1;
                        endSection = 4;
                        break;
                    case "下午（第5-8节）":
                        startSection = 5;
                        endSection = 8;
                        break;
                    case "晚上（第9-11节）":
                        startSection = 9;
                        endSection = 11;
                        break;
                }

                string query = @"
            WITH SplitSchedule AS (
                SELECT 
                    c.CourseID,
                    c.CourseName,
                    c.ClassroomID,
                    value AS SingleSchedule
                FROM Course c
                CROSS APPLY STRING_SPLIT(c.ScheduleTime, ',')
                WHERE c.SemesterID = @SemesterID
            ),
            ParsedSchedule AS (
                SELECT 
                    CourseID,
                    CourseName,
                    ClassroomID,
                    SingleSchedule,
                    CAST(LEFT(SingleSchedule, 1) AS INT) as WeekDay,
                    CAST(SUBSTRING(SingleSchedule, 
                        CHARINDEX('-', SingleSchedule) + 1,
                        CHARINDEX('-', SingleSchedule, CHARINDEX('-', SingleSchedule) + 1) - 
                        CHARINDEX('-', SingleSchedule) - 1) AS INT) as StartSection,
                    CAST(SUBSTRING(SingleSchedule,
                        CHARINDEX('-', SingleSchedule, CHARINDEX('-', SingleSchedule) + 1) + 1,
                        CHARINDEX('-', SingleSchedule, CHARINDEX('-', SingleSchedule, 
                            CHARINDEX('-', SingleSchedule) + 1) + 1) - 
                        CHARINDEX('-', SingleSchedule, CHARINDEX('-', SingleSchedule) + 1) - 1) AS INT) as EndSection,
                    CAST(SUBSTRING(SingleSchedule,
                        CHARINDEX('-', SingleSchedule, CHARINDEX('-', SingleSchedule, 
                            CHARINDEX('-', SingleSchedule) + 1) + 1) + 1,
                        CHARINDEX('-', SingleSchedule, CHARINDEX('-', SingleSchedule, 
                            CHARINDEX('-', SingleSchedule, CHARINDEX('-', SingleSchedule) + 1) + 1) + 1) - 
                        CHARINDEX('-', SingleSchedule, CHARINDEX('-', SingleSchedule, 
                            CHARINDEX('-', SingleSchedule) + 1) + 1) - 1) AS INT) as StartWeek,
                    CAST(SUBSTRING(SingleSchedule,
                        CHARINDEX('-', SingleSchedule, CHARINDEX('-', SingleSchedule, 
                            CHARINDEX('-', SingleSchedule, CHARINDEX('-', SingleSchedule) + 1) + 1) + 1) + 1,
                        CASE 
                            WHEN CHARINDEX('A', SingleSchedule) > 0 OR CHARINDEX('B', SingleSchedule) > 0 
                            THEN CHARINDEX('A', SingleSchedule + 'A') - 
                                CHARINDEX('-', SingleSchedule, CHARINDEX('-', SingleSchedule, 
                                    CHARINDEX('-', SingleSchedule, CHARINDEX('-', SingleSchedule) + 1) + 1) + 1) - 1
                            ELSE LEN(SingleSchedule) - 
                                CHARINDEX('-', SingleSchedule, CHARINDEX('-', SingleSchedule, 
                                    CHARINDEX('-', SingleSchedule, CHARINDEX('-', SingleSchedule) + 1) + 1) + 1)
                        END) AS INT) as EndWeek,
                    RIGHT(SingleSchedule, 1) as WeekType
                FROM SplitSchedule
            ),
            OccupiedClassrooms AS (
                SELECT DISTINCT 
                    ps.ClassroomID,
                    ps.CourseName,
                    STRING_AGG(CAST(CONCAT('第', ps.StartSection, '-', ps.EndSection, '节') AS NVARCHAR(MAX)), 
                             CHAR(13) + CHAR(10)) as ScheduleTimes
                FROM ParsedSchedule ps
                WHERE 
                    ps.WeekDay = @WeekDay
                    AND @WeekNumber BETWEEN ps.StartWeek AND ps.EndWeek
                    AND (
                        @TimeSlot = '全天'
                        OR (ps.StartSection <= @EndSection AND ps.EndSection >= @StartSection)
                    )
                    AND (
                        ps.WeekType NOT IN ('A', 'B')
                        OR (ps.WeekType = 'A' AND @IsOddWeek = 1)
                        OR (ps.WeekType = 'B' AND @IsOddWeek = 0)
                    )
                GROUP BY ps.ClassroomID, ps.CourseName
            )
            SELECT 
                cr.RoomNumber,
                cr.Floor,
                cr.Capacity,
                cr.SpatialLocation,
                CASE 
                    WHEN oc.ClassroomID IS NULL THEN '空闲'
                    ELSE '占用'
                END AS CurrentStatus,
                COALESCE(oc.CourseName, '') as CurrentCourse,
                COALESCE(oc.ScheduleTimes, '') as UsedTimeSlots
            FROM Classroom cr
            LEFT JOIN OccupiedClassrooms oc ON cr.ClassroomID = oc.ClassroomID
            ORDER BY cr.Floor, cr.RoomNumber";

                var parameters = new[]
                {
            new SqlParameter("@SemesterID", semester.SemesterID),
            new SqlParameter("@WeekDay", weekDay),
            new SqlParameter("@WeekNumber", weekNumber),
            new SqlParameter("@IsOddWeek", isOddWeek),
            new SqlParameter("@TimeSlot", timeSlot),
            new SqlParameter("@StartSection", startSection),
            new SqlParameter("@EndSection", endSection)
        };

                var dt = ExecuteQuery(query, parameters);
                ClassroomDataGrid.ItemsSource = dt.DefaultView;

                // 更新状态显示
                UpdateStatusText(queryDate, weekNumber, weekDay, isOddWeek);
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

        // 辅助方法：获取时间段条件
        private string GetTimeCondition(string tableAlias)
        {
            return $@"(
        CASE @TimeSlot
            WHEN '上午（第1-4节）' THEN ({tableAlias}.StartSection <= 4 AND {tableAlias}.EndSection >= 1)
            WHEN '下午（第5-8节）' THEN ({tableAlias}.StartSection <= 8 AND {tableAlias}.EndSection >= 5)
            WHEN '晚上（第9-11节）' THEN ({tableAlias}.StartSection <= 11 AND {tableAlias}.EndSection >= 9)
            ELSE 1=1  -- 全天
        END = 1)";
        }

        private SemesterInfo GetCurrentSemester(DateTime date)
        {
            string query = @"
        WITH RankedSemesters AS (
            SELECT 
                s.SemesterID,
                s.StartDate,
                s.EndDate,
                s.SemesterName,
                ay.YearName,
                CASE
                    -- 当前日期在学期内，优先级最高
                    WHEN @Date BETWEEN s.StartDate AND s.EndDate THEN 1
                    -- 当前日期在学期开始之前，选择最近的未来学期
                    WHEN @Date < s.StartDate THEN 2
                    -- 当前日期在学期结束之后，优先级最低
                    ELSE 3
                END AS Priority,
                -- 计算与当前日期的天数差
                ABS(DATEDIFF(DAY, @Date, 
                    CASE
                        WHEN @Date < s.StartDate THEN s.StartDate
                        WHEN @Date > s.EndDate THEN s.EndDate
                        ELSE @Date
                    END
                )) AS DaysDifference
            FROM Semester s
            INNER JOIN AcademicYear ay ON s.AcademicYearID = ay.AcademicYearID
        )
        SELECT TOP 1 
            SemesterID,
            StartDate,
            EndDate,
            SemesterName,
            YearName
        FROM RankedSemesters
        ORDER BY 
            Priority ASC,           -- 优先选择当前学期
            DaysDifference ASC,     -- 其次选择最近的学期
            StartDate ASC           -- 如果距离相同，选择较早开始的学期
    ";

            try
            {
                var dt = ExecuteQuery(query, new SqlParameter("@Date", date));
                if (dt.Rows.Count == 0) return null;

                return new SemesterInfo
                {
                    SemesterID = Convert.ToInt32(dt.Rows[0]["SemesterID"]),
                    StartDate = Convert.ToDateTime(dt.Rows[0]["StartDate"]),
                    EndDate = Convert.ToDateTime(dt.Rows[0]["EndDate"]),
                    SemesterName = dt.Rows[0]["SemesterName"].ToString(),
                    YearName = dt.Rows[0]["YearName"].ToString()
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"获取当前学期失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        // 更新 SemesterInfo 类
        public class SemesterInfo
        {
            public int SemesterID { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public string SemesterName { get; set; }
            public string YearName { get; set; }

            public override string ToString()
            {
                return $"{YearName} {SemesterName}";
            }
        }

        private void UpdateStatusText(DateTime queryDate, int weekNumber, int weekDay, bool isOddWeek)
        {
            string weekDayText = weekDay switch
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

            StatusTextBlock.Text = $"查询日期：{queryDate:yyyy-MM-dd} ({weekDayText})，第{weekNumber}周（{(isOddWeek ? "单" : "双")}周）";
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
                string studentID = GlobalUserState.LinkedID;
                string courseCode = ExamCodeTextBox.Text.Trim();

                string query = @"
            DECLARE @CurrentTime DATETIME = GETDATE();
            
            SELECT 
                c.CourseCode,
                c.CourseName,
                FORMAT(e.ExamDate, 'yyyy-MM-dd HH:mm') as ExamDateTime,
                e.ExamLocation,
                CAST(e.Duration as NVARCHAR) + ' 分钟' as ExamDuration,
                e.ExamType,
                t.Name as Invigilator,
                CAST(e.BatchNumber as NVARCHAR) as BatchNumber,
                CASE 
                    WHEN e.ExamDate > @CurrentTime THEN '未开始'
                    WHEN e.ExamDate <= @CurrentTime AND 
                         DATEADD(MINUTE, e.Duration, e.ExamDate) > @CurrentTime THEN '进行中'
                    ELSE '已结束'
                END as ExamStatus,
                e.ExamDate as OriginalExamDate
            FROM Exam e
            INNER JOIN Course c ON e.CourseID = c.CourseID
            INNER JOIN StudentCourse sc ON c.CourseID = sc.CourseID AND sc.StudentID = @studentID
            LEFT JOIN Teacher t ON e.InvigilatorID = t.TeacherID
            WHERE c.SemesterID = @semesterID";

                if (!string.IsNullOrEmpty(courseCode))
                {
                    query += " AND c.CourseCode LIKE @courseCode";
                }

                query += @" ORDER BY e.ExamDate";

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

                // 只在第一次加载时显示考试提醒（不是在查询时）
                if (sender != ExamCodeTextBox && sender != null)  // 确保不是通过查询按钮触发的
                {
                    var currentTime = DateTime.Now;
                    var upcomingExams = dt.AsEnumerable()
                        .Where(row =>
                        {
                            var examDate = row.Field<DateTime>("OriginalExamDate");
                            var timeUntilExam = examDate - currentTime;
                            return timeUntilExam > TimeSpan.Zero &&
                                   timeUntilExam <= TimeSpan.FromHours(24) &&
                                   row["ExamStatus"].ToString() != "已结束";
                        })
                        .ToList();

                    if (upcomingExams.Any())
                    {
                        var message = "您有以下即将到来的考试：\n\n";
                        foreach (var exam in upcomingExams)
                        {
                            message += $"课程：{exam["CourseName"]}\n" +
                                      $"时间：{exam["ExamDateTime"]}\n" +
                                      $"地点：{exam["ExamLocation"]}\n\n";
                        }
                        MessageBox.Show(message, "考试提醒", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
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