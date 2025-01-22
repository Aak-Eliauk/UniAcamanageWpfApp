using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.IO;
using ClosedXML.Excel;
using System.Windows.Input;

namespace UniAcamanageWpfApp.Windows
{
    public partial class BatchGradeEntryWindow : Window
    {
        private readonly string connectionString;
        private readonly string currentTeacherId;
        private readonly int currentSemesterId;
        private ObservableCollection<StudentGrade> studentGrades;
        private ICollectionView studentGradesView;
        private readonly bool isAdmin;

        public BatchGradeEntryWindow(string teacherId, int semesterId, bool isAdmin = false)
        {
            InitializeComponent();
            connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
            currentTeacherId = teacherId;
            currentSemesterId = semesterId;
            this.isAdmin = isAdmin;
            studentGrades = new ObservableCollection<StudentGrade>();
            StudentGradeGrid.ItemsSource = studentGrades;
            studentGradesView = CollectionViewSource.GetDefaultView(studentGrades);
            LoadTeacherCourses();
        }

        public class CourseInfo
        {
            public int CourseID { get; set; }
            public string CourseCode { get; set; }
            public string CourseName { get; set; }
            public string DisplayName => $"{CourseCode} - {CourseName}";
        }

        public class StudentGrade : INotifyPropertyChanged
        {
            public string StudentID { get; set; }
            public string StudentName { get; set; }
            public string ClassID { get; set; }
            public decimal? ExistingScore { get; set; }
            public bool ExistingIsRetest { get; set; }
            public string NewScore { get; set; }
            public bool IsRetest { get; set; }
            public string Remarks { get; set; }

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private async void LoadTeacherCourses()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    string query;

                    if (isAdmin)
                    {
                        query = @"
                    SELECT c.CourseID, c.CourseCode, c.CourseName
                    FROM Course c
                    WHERE c.SemesterID = @SemesterID
                    ORDER BY c.CourseCode";
                    }
                    else
                    {
                        query = @"
                    SELECT c.CourseID, c.CourseCode, c.CourseName
                    FROM Course c
                    JOIN TeacherCourse tc ON c.CourseID = tc.CourseID
                    WHERE tc.TeacherID = @TeacherID 
                    AND c.SemesterID = @SemesterID
                    ORDER BY c.CourseCode";
                    }

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        if (!isAdmin)
                        {
                            cmd.Parameters.AddWithValue("@TeacherID", currentTeacherId);
                        }
                        cmd.Parameters.AddWithValue("@SemesterID", currentSemesterId);

                        var courses = new List<CourseInfo>();
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                courses.Add(new CourseInfo
                                {
                                    CourseID = reader.GetInt32(0),
                                    CourseCode = reader.GetString(1),
                                    CourseName = reader.GetString(2)
                                });
                            }
                        }
                        CourseComboBox.ItemsSource = courses;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载课程信息失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CourseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CourseComboBox.SelectedItem is CourseInfo selectedCourse)
            {
                await LoadStudentGrades(selectedCourse.CourseID);
            }
        }

        private async Task LoadStudentGrades(int courseId)
        {
            try
            {
                studentGrades.Clear();
                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    string query = @"
                WITH LatestGrades AS (
                    SELECT 
                        StudentID,
                        CourseID,
                        Score,
                        IsRetest,
                        ROW_NUMBER() OVER (PARTITION BY StudentID, CourseID ORDER BY AttemptNumber DESC) as rn
                    FROM Grade
                    WHERE CourseID = @CourseID AND SemesterID = @SemesterID
                )
                SELECT 
                    s.StudentID,
                    s.Name AS StudentName,
                    s.ClassID,
                    lg.Score AS ExistingScore,
                    lg.IsRetest AS ExistingIsRetest
                FROM Student s
                JOIN StudentCourse sc ON s.StudentID = sc.StudentID
                LEFT JOIN LatestGrades lg ON s.StudentID = lg.StudentID 
                    AND lg.CourseID = sc.CourseID
                    AND lg.rn = 1
                WHERE sc.CourseID = @CourseID
                AND sc.SelectionType = '已确认'
                ORDER BY s.StudentID";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@CourseID", courseId);
                        cmd.Parameters.AddWithValue("@SemesterID", currentSemesterId);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                studentGrades.Add(new StudentGrade
                                {
                                    StudentID = reader.GetString(0),
                                    StudentName = reader.GetString(1),
                                    ClassID = reader.GetString(2),
                                    ExistingScore = reader.IsDBNull(3) ? null : reader.GetDecimal(3),
                                    ExistingIsRetest = reader.IsDBNull(4) ? false : reader.GetBoolean(4)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载学生成绩信息失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StudentGradeGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Column.Header.ToString() == "新成绩")
            {
                var textBox = e.EditingElement as TextBox;
                if (textBox != null)
                {
                    string newValue = textBox.Text;
                    if (!ValidateScore(newValue))
                    {
                        e.Cancel = true;
                    }
                }
            }
        }

        private bool ValidateScore(string scoreText)
        {
            if (string.IsNullOrWhiteSpace(scoreText))
                return true;

            if (!decimal.TryParse(scoreText, out decimal score))
            {
                ValidationMessageText.Text = "请输入有效的数字";
                return false;
            }

            if (score < 0 || score > 100)
            {
                ValidationMessageText.Text = "成绩必须在0-100之间";
                return false;
            }

            if (scoreText.Contains("."))
            {
                string[] parts = scoreText.Split('.');
                if (parts[1].Length > 1)
                {
                    ValidationMessageText.Text = "成绩最多保留一位小数";
                    return false;
                }
            }

            ValidationMessageText.Text = string.Empty;
            return true;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchBox.Text.ToLower();
            studentGradesView.Filter = item =>
            {
                if (item is StudentGrade grade)
                {
                    bool matchesSearch = string.IsNullOrWhiteSpace(searchText) ||
                        grade.StudentID.ToLower().Contains(searchText) ||
                        grade.StudentName.ToLower().Contains(searchText);

                    bool matchesUngraded = !ShowUngraded.IsChecked.Value ||
                        string.IsNullOrWhiteSpace(grade.NewScore);

                    return matchesSearch && matchesUngraded;
                }
                return false;
            };
        }

        private void ShowUngraded_CheckedChanged(object sender, RoutedEventArgs e)
        {
            SearchBox_TextChanged(null, null);
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateAllGrades())
                return;

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            var selectedCourse = CourseComboBox.SelectedItem as CourseInfo;
                            if (selectedCourse == null)
                            {
                                MessageBox.Show("请先选择课程", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }

                            foreach (var grade in studentGrades)
                            {
                                if (!string.IsNullOrWhiteSpace(grade.NewScore))
                                {
                                    await SaveStudentGrade(conn, transaction, selectedCourse.CourseID,
                                        grade.StudentID, decimal.Parse(grade.NewScore), grade.IsRetest);
                                }
                            }

                            transaction.Commit();
                            MessageBox.Show("成绩保存成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                            DialogResult = true;
                            Close();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            throw new Exception("保存成绩失败：" + ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SaveStudentGrade(SqlConnection conn, SqlTransaction transaction,
    int courseId, string studentId, decimal score, bool isRetest)
        {
            // 首先检查是否存在成绩记录
            string checkSql = @"
        SELECT MAX(AttemptNumber) 
        FROM Grade 
        WHERE StudentID = @StudentID 
        AND CourseID = @CourseID";

            int attemptNumber;
            using (var checkCmd = new SqlCommand(checkSql, conn, transaction))
            {
                checkCmd.Parameters.AddWithValue("@StudentID", studentId);
                checkCmd.Parameters.AddWithValue("@CourseID", courseId);

                var result = await checkCmd.ExecuteScalarAsync();
                attemptNumber = result == DBNull.Value ? 1 : Convert.ToInt32(result) + 1;
            }

            // 插入新的成绩记录
            string insertSql = @"
        INSERT INTO Grade (
            StudentID, CourseID, SemesterID, Score, IsRetest,
            ModifiedBy, ModifiedAt, AttemptNumber
        )
        VALUES (
            @StudentID, @CourseID, @SemesterID, @Score, @IsRetest,
            @ModifiedBy, @ModifiedAt, @AttemptNumber
        )";

            using (var cmd = new SqlCommand(insertSql, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@StudentID", studentId);
                cmd.Parameters.AddWithValue("@CourseID", courseId);
                cmd.Parameters.AddWithValue("@SemesterID", currentSemesterId);
                cmd.Parameters.AddWithValue("@Score", score);
                cmd.Parameters.AddWithValue("@IsRetest", isRetest);
                cmd.Parameters.AddWithValue("@ModifiedBy", currentTeacherId);
                cmd.Parameters.AddWithValue("@ModifiedAt", DateTime.Now);
                cmd.Parameters.AddWithValue("@AttemptNumber", attemptNumber);

                await cmd.ExecuteNonQueryAsync();
            }
        }

        private bool ValidateAllGrades()
        {
            bool isValid = true;
            foreach (var grade in studentGrades)
            {
                if (!string.IsNullOrWhiteSpace(grade.NewScore) && !ValidateScore(grade.NewScore))
                {
                    isValid = false;
                    break;
                }
            }
            return isValid;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ExportToExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 显示等待提示
                Mouse.OverrideCursor = Cursors.Wait;
                var selectedCourse = CourseComboBox.SelectedItem as CourseInfo;
                if (selectedCourse == null)
                {
                    MessageBox.Show("请先选择课程", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "Excel文件|*.xlsx",
                    FileName = $"{selectedCourse.CourseCode}_{selectedCourse.CourseName}_成绩表_{DateTime.Now:yyyyMMdd}"
                };

                if (saveFileDialog.ShowDialog() != true)
                    return;

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("成绩表");

                    // 设置标题
                    worksheet.Cell(1, 1).Value = $"{selectedCourse.CourseCode} - {selectedCourse.CourseName} 成绩表";
                    worksheet.Range(1, 1, 1, 8).Merge();
                    worksheet.Cell(1, 1).Style
                        .Font.SetBold(true)
                        .Font.SetFontSize(14)
                        .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                    // 设置表头
                    string[] headers = new[] { "学号", "姓名", "班级", "原有成绩", "是否补考", "新成绩", "补考", "备注" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = worksheet.Cell(2, i + 1);
                        cell.Value = headers[i];
                        cell.Style
                            .Font.SetBold(true)
                            .Fill.SetBackgroundColor(XLColor.LightGray);
                    }

                    // 填充数据
                    int row = 3;
                    foreach (var grade in studentGrades)
                    {
                        worksheet.Cell(row, 1).Value = grade.StudentID;
                        worksheet.Cell(row, 2).Value = grade.StudentName;
                        worksheet.Cell(row, 3).Value = grade.ClassID;
                        worksheet.Cell(row, 4).Value = grade.ExistingScore;
                        worksheet.Cell(row, 5).Value = grade.ExistingIsRetest ? "是" : "否";
                        worksheet.Cell(row, 6).Value = grade.NewScore;
                        worksheet.Cell(row, 7).Value = grade.IsRetest ? "是" : "否";
                        worksheet.Cell(row, 8).Value = grade.Remarks;
                        row++;
                    }

                    // 设置列宽
                    worksheet.Column(1).Width = 15;
                    worksheet.Column(2).Width = 12;
                    worksheet.Column(3).Width = 15;
                    worksheet.Column(4).Width = 12;
                    worksheet.Column(5).Width = 10;
                    worksheet.Column(6).Width = 12;
                    worksheet.Column(7).Width = 10;
                    worksheet.Column(8).Width = 20;

                    // 保存文件
                    workbook.SaveAs(saveFileDialog.FileName);
                }

                MessageBox.Show("导出成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportFromExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedCourse = CourseComboBox.SelectedItem as CourseInfo;
                if (selectedCourse == null)
                {
                    MessageBox.Show("请先选择课程", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var openFileDialog = new OpenFileDialog
                {
                    Filter = "Excel文件|*.xlsx",
                    Title = "选择成绩Excel文件"
                };

                if (openFileDialog.ShowDialog() != true)
                    return;

                using (var workbook = new XLWorkbook(openFileDialog.FileName))
                {
                    var worksheet = workbook.Worksheet(1);
                    var rows = worksheet.RowsUsed().Skip(2); // 跳过标题和表头

                    Dictionary<string, StudentGrade> gradeDictionary = studentGrades.ToDictionary(g => g.StudentID);
                    List<string> errorMessages = new List<string>();

                    foreach (var row in rows)
                    {
                        string studentId = row.Cell(1).Value.ToString().Trim();
                        if (string.IsNullOrEmpty(studentId))
                            continue;

                        if (gradeDictionary.TryGetValue(studentId, out StudentGrade grade))
                        {
                            // 读取新成绩
                            string newScore = row.Cell(6).Value.ToString().Trim();
                            if (!string.IsNullOrEmpty(newScore))
                            {
                                if (ValidateScore(newScore))
                                {
                                    grade.NewScore = newScore;
                                }
                                else
                                {
                                    errorMessages.Add($"第{row.RowNumber()}行学号{studentId}的成绩格式不正确");
                                    continue;
                                }
                            }

                            // 读取补考标记
                            string isRetest = row.Cell(7).Value.ToString().Trim().ToLower();
                            grade.IsRetest = isRetest == "是" || isRetest == "true" || isRetest == "1";

                            // 读取备注
                            grade.Remarks = row.Cell(8).Value.ToString().Trim();
                        }
                        else
                        {
                            errorMessages.Add($"第{row.RowNumber()}行的学号{studentId}不在当前课程名单中");
                        }
                    }

                    // 显示错误信息
                    if (errorMessages.Any())
                    {
                        string errorMsg = string.Join("\n", errorMessages);
                        MessageBox.Show($"导入过程中发现以下问题：\n{errorMsg}", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }

                // 恢复鼠标
                Mouse.OverrideCursor = null;
                MessageBox.Show("导出成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ValidateScoreWithMessage(string score, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(score))
                return true;

            if (!decimal.TryParse(score, out decimal scoreValue))
            {
                errorMessage = "成绩必须是有效的数字";
                return false;
            }

            if (scoreValue < 0 || scoreValue > 100)
            {
                errorMessage = "成绩必须在0-100之间";
                return false;
            }

            if (score.Contains("."))
            {
                string[] parts = score.Split('.');
                if (parts[1].Length > 1)
                {
                    errorMessage = "成绩最多保留一位小数";
                    return false;
                }
            }

            return true;
        }

        private void DownloadTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                var selectedCourse = CourseComboBox.SelectedItem as CourseInfo;
                if (selectedCourse == null)
                {
                    MessageBox.Show("请先选择课程", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "Excel文件|*.xlsx",
                    FileName = $"{selectedCourse.CourseCode}_{selectedCourse.CourseName}_成绩模板_{DateTime.Now:yyyyMMdd}"
                };

                if (saveFileDialog.ShowDialog() != true)
                    return;

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("成绩模板");

                    // 设置标题行
                    worksheet.Cell(1, 1).Value = $"{selectedCourse.CourseCode} - {selectedCourse.CourseName} 成绩录入模板";
                    worksheet.Range(1, 1, 1, 8).Merge();
                    worksheet.Cell(1, 1).Style
                        .Font.SetBold(true)
                        .Font.SetFontSize(14)
                        .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                    // 设置表头
                    string[] headers = new[] { "学号*", "姓名", "班级", "原有成绩", "原有补考记录", "新成绩*", "补考", "备注" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = worksheet.Cell(2, i + 1);
                        cell.Value = headers[i];
                        cell.Style
                            .Font.SetBold(true)
                            .Fill.SetBackgroundColor(XLColor.LightGray)
                            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                        if (headers[i].EndsWith("*"))
                        {
                            cell.Style.Font.SetFontColor(XLColor.Red);
                        }
                    }

                    // 填充学生数据
                    int row = 3;
                    foreach (var grade in studentGrades)
                    {
                        worksheet.Cell(row, 1).Value = grade.StudentID;
                        worksheet.Cell(row, 2).Value = grade.StudentName;
                        worksheet.Cell(row, 3).Value = grade.ClassID;
                        worksheet.Cell(row, 4).Value = grade.ExistingScore;
                        worksheet.Cell(row, 5).Value = grade.ExistingIsRetest ? "是" : "否";
                        row++;
                    }

                    // 设置列宽
                    worksheet.Column(1).Width = 15;  // 学号
                    worksheet.Column(2).Width = 12;  // 姓名
                    worksheet.Column(3).Width = 15;  // 班级
                    worksheet.Column(4).Width = 12;  // 原有成绩
                    worksheet.Column(5).Width = 12;  // 原有补考记录
                    worksheet.Column(6).Width = 12;  // 新成绩
                    worksheet.Column(7).Width = 10;  // 补考
                    worksheet.Column(8).Width = 25;  // 备注

                    // 添加说明信息
                    int noteRow = row + 2;
                    worksheet.Cell(noteRow, 1).Value = "注意事项：";
                    worksheet.Cell(noteRow + 1, 1).Value = "1. 带*的列为必填项";
                    worksheet.Cell(noteRow + 2, 1).Value = "2. 新成绩必须在0-100之间，最多保留一位小数";
                    worksheet.Cell(noteRow + 3, 1).Value = "3. 补考列只能填写'是'或'否'";
                    worksheet.Cell(noteRow + 4, 1).Value = "4. 请勿修改学号、姓名、班级等基本信息";
                    worksheet.Cell(noteRow + 5, 1).Value = "5. 原有成绩和原有补考记录列仅供参考，请勿修改";

                    // 设置注意事项样式
                    var noteRange = worksheet.Range(noteRow, 1, noteRow + 5, 1);
                    foreach (var cell in noteRange.Cells())
                    {
                        cell.Style.Font.SetFontColor(XLColor.Red).Font.SetFontSize(11);
                    }

                    // 保存文件
                    workbook.SaveAs(saveFileDialog.FileName);

                    MessageBox.Show(
                        "模板下载成功！\n请按模板格式填写成绩后使用'从Excel导入'功能导入成绩。",
                        "提示",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"模板下载失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }
    }
}