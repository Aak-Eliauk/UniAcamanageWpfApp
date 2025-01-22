using System;
using System.Collections.ObjectModel;
using Microsoft.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Configuration;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using UniAcamanageWpfApp.Windows;
using System.Collections.Generic;
using static UniAcamanageWpfApp.Windows.BatchGradeEntryWindow;
using UniAcamanageWpfApp.Models;
using static UniAcamanageWpfApp.Windows.GradeEntryWindow;

namespace UniAcamanageWpfApp.Views
{
    public partial class GradeManagementView : UserControl
    {
        private readonly string connectionString;
        private readonly string currentTeacherId;
        private ObservableCollection<GradeInfo> grades;
        private ObservableCollection<ExamScheduleInfo> examSchedules;
        private ICollectionView gradesView;
        private ICollectionView examSchedulesView;

        public GradeManagementView()
        {
            InitializeComponent();
            connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
            currentTeacherId = GlobalUserState.LinkedID;

            // 初始化集合
            grades = new ObservableCollection<GradeInfo>();
            examSchedules = new ObservableCollection<ExamScheduleInfo>();

            // 设置数据源
            GradesGrid.ItemsSource = grades;
            ExamScheduleGrid.ItemsSource = examSchedules;

            // 获取集合视图
            gradesView = CollectionViewSource.GetDefaultView(grades);
            examSchedulesView = CollectionViewSource.GetDefaultView(examSchedules);

            // 初始化数据
            LoadSemesters();
            SetupFilters();
        }

        #region 通用方法

        private void LoadSemesters()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var command = new SqlCommand(
                        "SELECT SemesterID, SemesterName FROM Semester ORDER BY StartDate DESC", conn);

                    var semesters = new List<SemesterInfo>();
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            semesters.Add(new SemesterInfo
                            {
                                SemesterID = reader.GetInt32(0),
                                SemesterName = reader.GetString(1)
                            });
                        }
                    }

                    // 设置两个学期下拉框的数据源
                    GradeSemesterComboBox.ItemsSource = semesters;
                    ExamSemesterComboBox.ItemsSource = semesters;

                    // 设置显示成员和值成员
                    GradeSemesterComboBox.DisplayMemberPath = "SemesterName";
                    GradeSemesterComboBox.SelectedValuePath = "SemesterID";
                    ExamSemesterComboBox.DisplayMemberPath = "SemesterName";
                    ExamSemesterComboBox.SelectedValuePath = "SemesterID";

                    // 选择第一项
                    if (semesters.Any())
                    {
                        GradeSemesterComboBox.SelectedIndex = 0;
                        ExamSemesterComboBox.SelectedIndex = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载学期信息失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetupFilters()
        {
            // 添加延迟搜索的计时器
            var gradeSearchTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            gradeSearchTimer.Tick += (s, e) =>
            {
                gradeSearchTimer.Stop();
                ApplyGradeFilters();
            };

            var examSearchTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            examSearchTimer.Tick += (s, e) =>
            {
                examSearchTimer.Stop();
                ApplyExamFilters();
            };

            // 搜索框文本改变事件
            GradeSearchBox.TextChanged += (s, e) =>
            {
                gradeSearchTimer.Stop();
                gradeSearchTimer.Start();
            };

            ExamSearchBox.TextChanged += (s, e) =>
            {
                examSearchTimer.Stop();
                examSearchTimer.Start();
            };

            // 设置过滤器
            gradesView.Filter = GradeFilter;
            examSchedulesView.Filter = ExamFilter;
        }

        private bool GradeFilter(object item)
        {
            if (item is GradeInfo grade)
            {
                // 搜索过滤
                var searchText = GradeSearchBox.Text?.ToLower() ?? "";
                if (!string.IsNullOrEmpty(searchText))
                {
                    if (!grade.StudentID.ToLower().Contains(searchText) &&
                        !grade.StudentName.ToLower().Contains(searchText))
                        return false;
                }

                // 课程过滤
                if (GradeCourseFilter.SelectedItem != null &&
                    GradeCourseFilter.SelectedValue is int courseId && courseId != 0)
                {
                    if (grade.CourseID != courseId)
                        return false;
                }

                // 成绩状态过滤
                if (GradeStatusFilter.SelectedItem is ComboBoxItem statusItem)
                {
                    var status = statusItem.Content.ToString();
                    switch (status)
                    {
                        case "已录入":
                            if (grade.Score == null) return false;
                            break;
                        case "未录入":
                            if (grade.Score != null) return false;
                            break;
                        case "不及格":
                            if (grade.Score == null || grade.Score >= 60) return false;
                            break;
                    }
                }

                return true;
            }
            return false;
        }

        private bool ExamFilter(object item)
        {
            if (item is ExamScheduleInfo exam)
            {
                // 搜索过滤
                var searchText = ExamSearchBox.Text?.ToLower() ?? "";
                if (!string.IsNullOrEmpty(searchText))
                {
                    if (!exam.CourseCode.ToLower().Contains(searchText) &&
                        !exam.CourseName.ToLower().Contains(searchText))
                        return false;
                }

                // 状态过滤
                if (ExamStatusFilter.SelectedItem is ComboBoxItem statusItem &&
                    statusItem.Content.ToString() != "全部")
                {
                    if (exam.Status != statusItem.Content.ToString())
                        return false;
                }

                return true;
            }
            return false;
        }

        private void ApplyGradeFilters()
        {
            gradesView?.Refresh();
        }

        private void ApplyExamFilters()
        {
            examSchedulesView?.Refresh();
        }

        #endregion

        #region 成绩管理事件处理

        private void GradeSemesterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GradeSemesterComboBox.SelectedValue != null)
            {
                LoadGrades((int)GradeSemesterComboBox.SelectedValue);
                LoadCourses((int)GradeSemesterComboBox.SelectedValue);
                ApplyGradeFilters(); // 重新应用过滤器
            }
        }

        private void GradeSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            gradesView?.Refresh();
        }

        private void GradeCourseFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyGradeFilters();
        }

        private void GradeStatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyGradeFilters();
        }


        private void ImportGrades_Click(object sender, RoutedEventArgs e)
        {
            if (GradeCourseFilter.SelectedItem == null ||
                GradeCourseFilter.SelectedValue.ToString() == "全部")
            {
                MessageBox.Show("请先选择具体课程", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedCourse = (CourseInfo)GradeCourseFilter.SelectedItem;
            var window = new BatchGradeEntryWindow(
        currentTeacherId,
        (int)GradeSemesterComboBox.SelectedValue,
        GlobalUserState.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase));
            if (window.ShowDialog() == true)
            {
                LoadGrades((int)GradeSemesterComboBox.SelectedValue);
            }
        }

        private void EnterGrade_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var grade = (GradeInfo)button.DataContext;

            var window = new GradeEntryWindow(
                new GradeEntryWindow.StudentGradeInfo
                {
                    GradeID = grade.GradeID,
                    StudentID = grade.StudentID,
                    StudentName = grade.StudentName,
                    CourseID = grade.CourseID,
                    CourseCode = grade.CourseCode,
                    CourseName = grade.CourseName,
                    ClassID = grade.ClassID,
                    Credit = grade.Credit,
                    ExistingScore = grade.Score,
                    ExistingIsRetest = grade.IsRetest,
                    Score = null,
                    IsRetest = false,
                    SemesterID = (int)GradeSemesterComboBox.SelectedValue,
                    ModifiedBy = currentTeacherId,
                    ModifiedAt = DateTime.Now,
                    AttemptNumber = 1,
                },
                currentTeacherId,
                false);

            if (window.ShowDialog() == true)
            {
                LoadGrades((int)GradeSemesterComboBox.SelectedValue);
            }
        }

        private void EditGrade_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var grade = (GradeInfo)button.DataContext;

            var window = new GradeEntryWindow(
                new GradeEntryWindow.StudentGradeInfo
                {
                    GradeID = grade.GradeID,
                    StudentID = grade.StudentID,
                    StudentName = grade.StudentName,
                    CourseID = grade.CourseID,
                    CourseCode = grade.CourseCode,
                    CourseName = grade.CourseName,
                    ClassID = grade.ClassID,
                    Credit = grade.Credit,
                    ExistingScore = grade.Score,
                    ExistingIsRetest = grade.IsRetest,
                    Score = grade.Score,
                    IsRetest = grade.IsRetest,
                    SemesterID = (int)GradeSemesterComboBox.SelectedValue,
                    ModifiedBy = currentTeacherId,
                    ModifiedAt = DateTime.Now,
                    AttemptNumber = grade.AttemptNumber + 1,
                },
                currentTeacherId,
                false);

            if (window.ShowDialog() == true)
            {
                LoadGrades((int)GradeSemesterComboBox.SelectedValue);
            }
        }

        private void LoadGrades(int semesterId)
        {
            try
            {
                grades.Clear();
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string sql;
                    SqlCommand command;

                    if (GlobalUserState.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                    {
                        sql = @"
        WITH LatestGrades AS (
            SELECT g.*,
                   ROW_NUMBER() OVER (PARTITION BY g.StudentID, g.CourseID 
                                    ORDER BY g.AttemptNumber DESC) as rn
            FROM Grade g
            WHERE g.SemesterID = @SemesterID
        )
        SELECT 
            COALESCE(g.GradeID, 0) as GradeID, 
            s.StudentID, 
            s.Name AS StudentName, 
            c.CourseID, 
            c.CourseCode, 
            c.CourseName, 
            c.Credit,
            g.Score, 
            CAST(COALESCE(g.IsRetest, 0) as bit) as IsRetest, 
            g.ModifiedAt, 
            COALESCE(g.ModifiedBy, '') as ModifiedBy,
            g.GradeLevel, 
            g.BaseGradePoint, 
            g.WeightedGradePoint,
            COALESCE(g.AttemptNumber, 0) as AttemptNumber, 
            s.ClassID
        FROM StudentCourse sc
        JOIN Student s ON sc.StudentID = s.StudentID
        JOIN Course c ON sc.CourseID = c.CourseID
        LEFT JOIN LatestGrades g ON sc.StudentID = g.StudentID 
            AND sc.CourseID = g.CourseID
            AND g.rn = 1
        WHERE c.SemesterID = @SemesterID
        AND sc.SelectionType = '已确认'
        AND (@CourseID = 0 OR c.CourseID = @CourseID)
        ORDER BY c.CourseCode, s.StudentID";

                        command = new SqlCommand(sql, conn);
                        command.Parameters.AddWithValue("@SemesterID", semesterId);
                        command.Parameters.AddWithValue("@CourseID", GradeCourseFilter.SelectedValue ?? 0);
                    }
                    else
                    {
                        sql = @"
                    WITH LatestGrades AS (
                        SELECT g.*,
                               ROW_NUMBER() OVER (PARTITION BY g.StudentID, g.CourseID 
                                                ORDER BY g.AttemptNumber DESC) as rn
                        FROM Grade g
                        WHERE g.SemesterID = @SemesterID
                    )
                    SELECT 
                        COALESCE(g.GradeID, 0) as GradeID, 
                        s.StudentID, 
                        s.Name AS StudentName, 
                        c.CourseID, 
                        c.CourseCode, 
                        c.CourseName, 
                        c.Credit,
                        g.Score, 
                        CAST(COALESCE(g.IsRetest, 0) as bit) as IsRetest, 
                        g.ModifiedAt, 
                        COALESCE(g.ModifiedBy, '') as ModifiedBy,
                        g.GradeLevel, 
                        g.BaseGradePoint, 
                        g.WeightedGradePoint,
                        COALESCE(g.AttemptNumber, 0) as AttemptNumber, 
                        s.ClassID
                    FROM StudentCourse sc
                    JOIN Student s ON sc.StudentID = s.StudentID
                    JOIN Course c ON sc.CourseID = c.CourseID
                    JOIN TeacherCourse tc ON c.CourseID = tc.CourseID
                    LEFT JOIN LatestGrades g ON sc.StudentID = g.StudentID 
                        AND sc.CourseID = g.CourseID
                        AND g.rn = 1
                    WHERE c.SemesterID = @SemesterID
                    AND tc.TeacherID = @TeacherID
                    AND sc.SelectionType = '已确认'
                    AND (@CourseID = 0 OR c.CourseID = @CourseID)
                    ORDER BY c.CourseCode, s.StudentID";

                        command = new SqlCommand(sql, conn);
                        command.Parameters.AddWithValue("@SemesterID", semesterId);
                        command.Parameters.AddWithValue("@TeacherID", currentTeacherId);
                        command.Parameters.AddWithValue("@CourseID", GradeCourseFilter.SelectedValue ?? 0);
                    }

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var gradeInfo = new GradeInfo
                            {
                                GradeID = reader.GetInt32(0),
                                StudentID = reader.GetString(1),
                                StudentName = reader.GetString(2),
                                CourseID = reader.GetInt32(3),
                                CourseCode = reader.GetString(4),
                                CourseName = reader.GetString(5),
                                Credit = reader.GetDecimal(6),
                                Score = reader.IsDBNull(7) ? null : (decimal?)reader.GetDecimal(7),
                                IsRetest = reader.GetBoolean(8), // 现在这里应该能正确读取 bit 类型
                                AttemptNumber = reader.GetInt32(14),
                                ClassID = reader.GetString(15)
                            };

                            if (!reader.IsDBNull(9)) // ModifiedAt
                            {
                                gradeInfo.GradeEntryTime = reader.GetDateTime(9);
                                gradeInfo.EnteredBy = reader.GetString(10);
                                gradeInfo.GradeLevel = reader.IsDBNull(11) ? null : reader.GetString(11);
                                gradeInfo.BaseGradePoint = reader.IsDBNull(12) ? null : (decimal?)reader.GetDecimal(12);
                                gradeInfo.WeightedGradePoint = reader.IsDBNull(13) ? null : (decimal?)reader.GetDecimal(13);
                            }

                            grades.Add(gradeInfo);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载成绩信息失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadCourses(int semesterId)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string sql;
                    SqlCommand command;

                    if (GlobalUserState.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                    {
                        // 管理员可以查看所有课程
                        sql = @"
                    SELECT DISTINCT c.CourseID, 
                           c.CourseCode,
                           c.CourseName,
                           c.CourseCode + ' - ' + c.CourseName as DisplayName
                    FROM Course c
                    WHERE c.SemesterID = @SemesterID
                    ORDER BY c.CourseCode, c.CourseName";

                        command = new SqlCommand(sql, conn);
                        command.Parameters.AddWithValue("@SemesterID", semesterId);
                    }
                    else
                    {
                        // 教师只能查看自己的课程
                        sql = @"
                    SELECT DISTINCT c.CourseID, 
                           c.CourseCode,
                           c.CourseName,
                           c.CourseCode + ' - ' + c.CourseName as DisplayName
                    FROM Course c
                    JOIN TeacherCourse tc ON c.CourseID = tc.CourseID
                    WHERE c.SemesterID = @SemesterID 
                    AND tc.TeacherID = @TeacherID
                    ORDER BY c.CourseCode, c.CourseName";

                        command = new SqlCommand(sql, conn);
                        command.Parameters.AddWithValue("@SemesterID", semesterId);
                        command.Parameters.AddWithValue("@TeacherID", currentTeacherId);
                    }

                    var courses = new List<CourseInfo>();
                    courses.Add(new CourseInfo { CourseID = 0, DisplayName = "全部" });

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            courses.Add(new CourseInfo
                            {
                                CourseID = reader.GetInt32(0),
                                DisplayName = reader.GetString(3)
                            });
                        }
                    }

                    GradeCourseFilter.ItemsSource = courses;
                    GradeCourseFilter.DisplayMemberPath = "DisplayName";
                    GradeCourseFilter.SelectedValuePath = "CourseID";
                    GradeCourseFilter.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载课程信息失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region 考试安排事件处理

        private void ExamSemesterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ExamSemesterComboBox.SelectedValue != null)
            {
                LoadExamSchedules((int)ExamSemesterComboBox.SelectedValue);
                ApplyExamFilters(); // 重新应用过滤器
            }
        }

        private void ExamSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            examSchedulesView?.Refresh();
        }

        private void ExamStatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyExamFilters();
        }

        private void AddExam_Click(object sender, RoutedEventArgs e)
        {
            if (ExamSemesterComboBox.SelectedValue == null)
            {
                MessageBox.Show("请先选择学期", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var window = new ExamScheduleEditWindow(currentTeacherId,
                (int)ExamSemesterComboBox.SelectedValue);
            if (window.ShowDialog() == true)
            {
                LoadExamSchedules((int)ExamSemesterComboBox.SelectedValue);
            }
        }

        private void EditExam_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var exam = (ExamScheduleInfo)button.DataContext;

            var window = new ExamScheduleEditWindow(
                currentTeacherId,
                (int)ExamSemesterComboBox.SelectedValue,
                exam.ExamID);

            if (window.ShowDialog() == true)
            {
                LoadExamSchedules((int)ExamSemesterComboBox.SelectedValue);
            }
        }

        private void ViewExamDetails_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var exam = (ExamScheduleInfo)button.DataContext;

            var window = new ExamScheduleEditWindow(
                currentTeacherId,
                (int)ExamSemesterComboBox.SelectedValue,
                exam.ExamID,
                true); // true表示只读模式

            window.ShowDialog();
        }

        private void LoadExamSchedules(int semesterId)
        {
            try
            {
                examSchedules.Clear();
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string sql;

                    if (GlobalUserState.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                    {
                        sql = @"
                    SELECT e.ExamID, c.CourseCode, c.CourseName, 
                           e.ExamDate, e.ExamLocation, e.Duration,
                           e.ExamType, t.Name as InvigilatorName,
                           e.ClassID, e.BatchNumber
                    FROM Exam e
                    JOIN Course c ON e.CourseID = c.CourseID
                    JOIN Teacher t ON e.InvigilatorID = t.TeacherID
                    WHERE c.SemesterID = @SemesterID";

                        var command = new SqlCommand(sql, conn);
                        command.Parameters.AddWithValue("@SemesterID", semesterId);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                // 添加考试信息
                                AddExamScheduleFromReader(reader);
                            }
                        }
                    }
                    else
                    {
                        sql = @"
                    SELECT e.ExamID, c.CourseCode, c.CourseName, 
                           e.ExamDate, e.ExamLocation, e.Duration,
                           e.ExamType, t.Name as InvigilatorName,
                           e.ClassID, e.BatchNumber
                    FROM Exam e
                    JOIN Course c ON e.CourseID = c.CourseID
                    JOIN Teacher t ON e.InvigilatorID = t.TeacherID
                    JOIN TeacherCourse tc ON c.CourseID = tc.CourseID
                    WHERE c.SemesterID = @SemesterID 
                    AND tc.TeacherID = @TeacherID";

                        var command = new SqlCommand(sql, conn);
                        command.Parameters.AddWithValue("@SemesterID", semesterId);
                        command.Parameters.AddWithValue("@TeacherID", currentTeacherId);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                AddExamScheduleFromReader(reader);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载考试安排失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddExamScheduleFromReader(SqlDataReader reader)
        {
            var examDate = reader.GetDateTime(3);
            examSchedules.Add(new ExamScheduleInfo
            {
                ExamID = reader.GetInt32(0),
                CourseCode = reader.GetString(1),
                CourseName = reader.GetString(2),
                ExamDate = examDate.Date,
                StartTime = examDate,
                EndTime = examDate.AddMinutes(reader.GetInt32(5)),
                Location = reader.GetString(4),
                ExamType = reader.GetString(6),
                Invigilators = reader.GetString(7),
                ClassID = reader.IsDBNull(8) ? null : reader.GetString(8),
                BatchNumber = reader.GetInt32(9),
                Status = "已安排"
            });
        }

        private void DeleteExam_Click(object sender, RoutedEventArgs e)
        {
            // 检查权限
            if (!GlobalUserState.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("对不起，只有管理员才能删除考试安排", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var button = (Button)sender;
            var exam = (ExamScheduleInfo)button.DataContext;

            var result = MessageBox.Show(
                $"确定要删除这个考试安排吗？\n\n" +
                $"课程：{exam.CourseCode} - {exam.CourseName}\n" +
                $"时间：{exam.ExamDate:yyyy-MM-dd HH:mm}\n" +
                $"地点：{exam.Location}",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using (var conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        using (var transaction = conn.BeginTransaction())
                        {
                            try
                            {
                                var command = new SqlCommand(
                                    "DELETE FROM Exam WHERE ExamID = @ExamID", conn, transaction);
                                command.Parameters.AddWithValue("@ExamID", exam.ExamID);
                                command.ExecuteNonQuery();

                                transaction.Commit();
                                MessageBox.Show("删除成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);

                                // 刷新考试列表
                                LoadExamSchedules((int)ExamSemesterComboBox.SelectedValue);
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
                    MessageBox.Show($"删除失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }


    }
    #endregion

    #region 模型类

    public class SemesterInfo
    {
        public int SemesterID { get; set; }
        public string SemesterName { get; set; }
    }

    public class CourseInfo
    {
        public int CourseID { get; set; }
        public string DisplayName { get; set; }
    }

    public class GradeInfo : INotifyPropertyChanged
    {
        public int GradeID { get; set; }
        public string StudentID { get; set; }
        public string StudentName { get; set; }
        public string ClassID { get; set; }
        public int CourseID { get; set; }
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public decimal Credit { get; set; }

        private decimal? _score;
        public int AttemptNumber { get; set; }
        public string? GradeLevel { get; set; }
        public decimal? BaseGradePoint { get; set; }
        public decimal? WeightedGradePoint { get; set; }
        public decimal? Score
        {
            get => _score;
            set
            {
                if (_score != value)
                {
                    _score = value;
                    OnPropertyChanged(nameof(Score));
                    OnPropertyChanged(nameof(IsFailingGrade));
                }
            }
        }

        public bool IsFailingGrade => Score.HasValue && Score.Value < 60;
        public bool IsRetest { get; set; }
        public DateTime GradeEntryTime { get; set; }
        public string EnteredBy { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ExamScheduleInfo : INotifyPropertyChanged
    {
        public int ExamID { get; set; }
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public DateTime ExamDate { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Location { get; set; }
        public string ExamType { get; set; }
        public string Invigilators { get; set; }
        public string ClassID { get; set; }
        public int BatchNumber { get; set; }
        public int StudentCount { get; set; }
        public string Status { get; set; }
        public string Remarks { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    #endregion
}