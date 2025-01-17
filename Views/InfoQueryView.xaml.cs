using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Linq;
using UniAcamanageWpfApp.Models;

namespace UniAcamanageWpfApp.Views
{
    public partial class InfoQueryView : UserControl
    {
        private static readonly string ConnectionString =
            ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;

        public InfoQueryView()
        {
            InitializeComponent();
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
                LoadSemesterComboBox();

                // 默认加载当前学期的考试信息
                LoadCurrentExams();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"页面加载出错: {ex.Message}");
                MainGrid.Opacity = 1;
            }
            finally
            {
                HideLoading();
            }
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
                ShowLoading();
                string studentID = GlobalUserState.LinkedID;
                if (string.IsNullOrEmpty(studentID))
                {
                    return;
                }

                string query = @"
                    SELECT s.StudentID,
                           s.[Name] AS StudentName,
                           s.Gender,
                           s.YearOfAdmission,
                           s.Major,
                           s.Status,
                           c.ClassName
                    FROM Student s
                    INNER JOIN Class c ON s.ClassID = c.ClassID
                    WHERE s.StudentID = @studentID;
                ";

                var dt = ExecuteQuery(query, new SqlParameter("@studentID", studentID));
                if (dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    StudentIDTextBox.Text = row["StudentID"].ToString();
                    NameTextBox.Text = row["StudentName"].ToString();
                    GenderTextBox.Text = row["Gender"].ToString();
                    YearOfAdmissionTextBox.Text = row["YearOfAdmission"].ToString();
                    MajorTextBox.Text = row["Major"].ToString();
                    ClassTextBox.Text = row["ClassName"].ToString();
                    StatusTextBox.Text = row["Status"].ToString();

                    // 加载当前学期课表
                    LoadCurrentSemesterSchedule();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载学生基本信息失败: {ex.Message}");
            }
            finally
            {
                HideLoading();
            }
        }

        private void LoadSemesterComboBox()
        {
            try
            {
                ShowLoading();
                string query = @"
                    SELECT s.SemesterID,
                           s.SemesterName,
                           ay.YearName
                    FROM Semester s
                    INNER JOIN AcademicYear ay ON s.AcademicYearID = ay.AcademicYearID
                    ORDER BY ay.StartDate DESC, s.StartDate DESC;
                ";

                var dt = ExecuteQuery(query);
                var list = new List<SemesterItem>();
                foreach (DataRow row in dt.Rows)
                {
                    list.Add(new SemesterItem
                    {
                        SemesterID = Convert.ToInt32(row["SemesterID"]),
                        SemesterName = row["SemesterName"].ToString(),
                        YearName = row["YearName"].ToString()
                    });
                }

                SemesterComboBox.ItemsSource = list;
                SemesterComboBox.DisplayMemberPath = "DisplayName";
                SemesterComboBox.SelectedValuePath = "SemesterID";

                // 选择当前学期
                int currentSemesterId = GetCurrentSemesterID();
                if (currentSemesterId != -1)
                {
                    var currentSemester = list.FirstOrDefault(s => s.SemesterID == currentSemesterId);
                    if (currentSemester != null)
                    {
                        SemesterComboBox.SelectedValue = currentSemester.SemesterID;
                    }
                }
                else if (list.Count > 0)
                {
                    SemesterComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载学期列表失败: {ex.Message}");
            }
            finally
            {
                HideLoading();
            }
        }

        private void LoadCurrentSemesterSchedule()
        {
            int currentSemesterId = GetCurrentSemesterID();
            if (currentSemesterId != -1)
            {
                QueryPersonalSchedule(currentSemesterId);
            }
        }
        #endregion

        #region 课表查询
        private int GetCurrentSemesterID()
        {
            string query = @"
                SELECT TOP 1 SemesterID
                FROM Semester
                WHERE GETDATE() BETWEEN StartDate AND EndDate
                ORDER BY StartDate DESC";

            var dt = ExecuteQuery(query);
            if (dt.Rows.Count > 0)
            {
                return Convert.ToInt32(dt.Rows[0]["SemesterID"]);
            }
            return -1;
        }

        private void QueryPersonalSchedule(int semesterID)
        {
            try
            {
                ShowLoading();
                string studentID = StudentIDTextBox.Text.Trim();
                if (string.IsNullOrEmpty(studentID))
                {
                    MessageBox.Show("当前学生ID为空，无法查询个人课表！");
                    return;
                }

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
                    AND c.SemesterID = @semesterID";

                var dt = ExecuteQuery(query,
                    new SqlParameter("@studentID", studentID),
                    new SqlParameter("@semesterID", semesterID));

                ScheduleDataGrid.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"查询课表失败: {ex.Message}");
            }
            finally
            {
                HideLoading();
            }
        }

        private void QueryPersonalSchedule(object sender, RoutedEventArgs e)
        {
            if (SemesterComboBox.SelectedValue == null)
            {
                MessageBox.Show("请选择学期！");
                return;
            }

            int semesterID = Convert.ToInt32(SemesterComboBox.SelectedValue);
            QueryPersonalSchedule(semesterID);
        }
        #endregion

        #region 课程查询
        private void CourseSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string keyword = CourseSearchTextBox.Text.Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                CourseSearchResultListBox.Visibility = Visibility.Collapsed;
                return;
            }

            try
            {
                string query = @"
                    SELECT TOP 10 
                        c.CourseID,
                        c.CourseCode,
                        c.CourseName,
                        t.Name as TeacherName
                    FROM Course c
                    LEFT JOIN TeacherCourse tc ON c.CourseID = tc.CourseID
                    LEFT JOIN Teacher t ON tc.TeacherID = t.TeacherID
                    WHERE c.CourseCode LIKE @kw
                    OR c.CourseName LIKE @kw
                    ORDER BY c.CourseName";

                var dt = ExecuteQuery(query,
                    new SqlParameter("@kw", $"%{keyword}%"));

                var items = new List<string>();
                foreach (DataRow row in dt.Rows)
                {
                    string code = row["CourseCode"].ToString();
                    string name = row["CourseName"].ToString();
                    string teacher = row["TeacherName"]?.ToString() ?? "未安排教师";
                    items.Add($"{code} - {name} (教师: {teacher})");
                }

                CourseSearchResultListBox.ItemsSource = items;
                CourseSearchResultListBox.Visibility = items.Any() ?
                    Visibility.Visible : Visibility.Collapsed;
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
                QueryCourseSchedule(courseCode);
            }
        }

        private void QueryCourseSchedule(string courseCode)
        {
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
                    WHERE c.CourseCode = @courseCode";

                var dt = ExecuteQuery(query,
                    new SqlParameter("@courseCode", courseCode));

                CourseScheduleDataGrid.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"查询课程信息失败: {ex.Message}");
            }
            finally
            {
                HideLoading();
            }
        }

        private void QueryCourseSchedule(object sender, RoutedEventArgs e)
        {
            string courseCode = CourseSearchTextBox.Text.Trim();
            if (string.IsNullOrEmpty(courseCode))
            {
                MessageBox.Show("请输入或选择课程！");
                return;
            }
            QueryCourseSchedule(courseCode);
        }
        #endregion

        #region 教师课表查询
        private void QueryTeacherSchedule(object sender, RoutedEventArgs e)
        {
            string teacherID = TeacherIDTextBox.Text.Trim();
            if (string.IsNullOrEmpty(teacherID))
            {
                MessageBox.Show("请输入教师工号！");
                return;
            }

            try
            {
                ShowLoading();
                string query = @"
            SELECT 
                t.TeacherID,
                t.Name as TeacherName,
                c.CourseCode,
                c.CourseName,
                c.CourseType,
                c.ScheduleTime,
                cr.RoomNumber as Classroom,
                (SELECT COUNT(*) FROM StudentCourse sc WHERE sc.CourseID = c.CourseID) as StudentCount,
                c.Capacity
            FROM TeacherCourse tc
            INNER JOIN Teacher t ON tc.TeacherID = t.TeacherID
            INNER JOIN Course c ON tc.CourseID = c.CourseID
            LEFT JOIN Classroom cr ON c.ClassroomID = cr.ClassroomID
            WHERE t.TeacherID = @teacherID";

                var dt = ExecuteQuery(query,
                    new SqlParameter("@teacherID", teacherID));

                if (dt.Rows.Count == 0)
                {
                    MessageBox.Show("未找到该教师的课表信息！");
                }
                else
                {
                    TeacherScheduleDataGrid.ItemsSource = dt.DefaultView;
                }
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

        #region 教室查询
        private void QueryClassroom(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowLoading();
                int currentSemesterId = GetCurrentSemesterID();

                string query = @"
                    SELECT 
                        cr.ClassroomID,
                        cr.RoomNumber,
                        cr.Floor,
                        cr.Capacity,
                        cr.SpatialLocation,
                        CASE 
                            WHEN c.CourseID IS NULL THEN '空闲'
                            ELSE '占用'
                        END as CurrentStatus,
                        COALESCE(c.CourseName, '') as CurrentCourse,
                        COALESCE(c.ScheduleTime, '') as ScheduleTime,
                        COALESCE(t.Name, '') as TeacherName
                    FROM Classroom cr
                    LEFT JOIN Course c ON cr.ClassroomID = c.ClassroomID 
                        AND c.SemesterID = @semesterID
                    LEFT JOIN TeacherCourse tc ON c.CourseID = tc.CourseID
                    LEFT JOIN Teacher t ON tc.TeacherID = t.TeacherID
                    ORDER BY cr.Floor, cr.RoomNumber";

                var dt = ExecuteQuery(query,
                    new SqlParameter("@semesterID", currentSemesterId));

                ClassroomDataGrid.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"查询教室出错: {ex.Message}");
            }
            finally
            {
                HideLoading();
            }
        }
        #endregion

        #region 考试信息查询
        private void LoadCurrentExams()
        {
            try
            {
                ShowLoading();
                string studentID = GlobalUserState.LinkedID;
                int currentSemesterId = GetCurrentSemesterID();

                if (string.IsNullOrEmpty(studentID) || currentSemesterId == -1)
                {
                    return;
                }

                string query = @"
                    SELECT DISTINCT
                        e.ExamID,
                        c.CourseCode,
                        c.CourseName,
                        e.ExamDate,
                        e.ExamLocation,
                        e.Duration,
                        e.ExamType,
                        t.Name as InvigilatorName
                    FROM Exam e
                    INNER JOIN Course c ON e.CourseID = c.CourseID
                    INNER JOIN StudentCourse sc ON c.CourseID = sc.CourseID
                    INNER JOIN Teacher t ON e.InvigilatorID = t.TeacherID
                    WHERE sc.StudentID = @studentID
                    AND c.SemesterID = @semesterID
                    AND (e.ClassID IS NULL OR e.ClassID = (
                        SELECT ClassID FROM Student WHERE StudentID = @studentID
                    ))
                    ORDER BY e.ExamDate";

                var dt = ExecuteQuery(query,
                    new SqlParameter("@studentID", studentID),
                    new SqlParameter("@semesterID", currentSemesterId));

                ExamDataGrid.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载考试信息失败: {ex.Message}");
            }
            finally
            {
                HideLoading();
            }
        }

        private void QueryExam(object sender, RoutedEventArgs e)
        {
            string studentID = GlobalUserState.LinkedID;
            if (string.IsNullOrEmpty(studentID))
            {
                MessageBox.Show("未获取到学生信息！");
                return;
            }

            try
            {
                ShowLoading();
                string courseCode = ExamCodeTextBox.Text.Trim();
                string query;
                SqlParameter[] parameters;

                if (string.IsNullOrEmpty(courseCode))
                {
                    // 如果未输入课程代码，显示所有考试
                    query = @"
                        SELECT DISTINCT
                            e.ExamID,
                            c.CourseCode,
                            c.CourseName,
                            e.ExamDate,
                            e.ExamLocation,
                            e.Duration,
                            e.ExamType,
                            t.Name as InvigilatorName
                        FROM Exam e
                        INNER JOIN Course c ON e.CourseID = c.CourseID
                        INNER JOIN StudentCourse sc ON c.CourseID = sc.CourseID
                        INNER JOIN Teacher t ON e.InvigilatorID = t.TeacherID
                        WHERE sc.StudentID = @studentID
                        ORDER BY e.ExamDate";

                    parameters = new[] { new SqlParameter("@studentID", studentID) };
                }
                else
                {
                    // 查询特定课程的考试
                    query = @"
                        SELECT DISTINCT
                            e.ExamID,
                            c.CourseCode,
                            c.CourseName,
                            e.ExamDate,
                            e.ExamLocation,
                            e.Duration,
                            e.ExamType,
                            t.Name as InvigilatorName
                        FROM Exam e
                        INNER JOIN Course c ON e.CourseID = c.CourseID
                        INNER JOIN StudentCourse sc ON c.CourseID = sc.CourseID
                        INNER JOIN Teacher t ON e.InvigilatorID = t.TeacherID
                        WHERE sc.StudentID = @studentID
                        AND c.CourseCode LIKE @courseCode
                        ORDER BY e.ExamDate";

                    parameters = new[]
                    {
                        new SqlParameter("@studentID", studentID),
                        new SqlParameter("@courseCode", $"%{courseCode}%")
                    };
                }

                var dt = ExecuteQuery(query, parameters);
                ExamDataGrid.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"查询考试信息失败: {ex.Message}");
            }
            finally
            {
                HideLoading();
            }
        }
        #endregion

        #region 辅助方法
        private DataTable ExecuteQuery(string query, params SqlParameter[] parameters)
        {
            DataTable dt = new DataTable();
            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        if (parameters != null)
                            cmd.Parameters.AddRange(parameters);

                        using (var adapter = new SqlDataAdapter(cmd))
                        {
                            adapter.Fill(dt);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"数据库操作出错: {ex.Message}");
            }
            return dt;
        }
        #endregion
    }
}