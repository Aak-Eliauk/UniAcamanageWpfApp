using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Linq;
using System.IO;
using Microsoft.Win32;
using ClosedXML.Excel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Threading;

namespace UniAcamanageWpfApp.Windows
{
    public partial class StudentListWindow : System.Windows.Window
    {
        private readonly string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
        private readonly int courseId;
        private readonly string courseName;
        private List<StudentInfo> studentList;
        private ICollectionView studentView;
        private DispatcherTimer searchTimer;

        public class StudentInfo : INotifyPropertyChanged
        {
            private string studentID;
            private string name;
            private string major;
            private string selectionType;
            private DateTime selectionDate;
            private string remarks;
            private string rejectReason;

            public string StudentID
            {
                get => studentID;
                set
                {
                    studentID = value;
                    OnPropertyChanged(nameof(StudentID));
                }
            }

            public string Name
            {
                get => name;
                set
                {
                    name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }

            public string Major
            {
                get => major;
                set
                {
                    major = value;
                    OnPropertyChanged(nameof(Major));
                }
            }

            public string SelectionType
            {
                get => selectionType;
                set
                {
                    selectionType = value;
                    OnPropertyChanged(nameof(SelectionType));
                }
            }

            public DateTime SelectionDate
            {
                get => selectionDate;
                set
                {
                    selectionDate = value;
                    OnPropertyChanged(nameof(SelectionDate));
                }
            }

            public string Remarks
            {
                get => remarks;
                set
                {
                    remarks = value;
                    OnPropertyChanged(nameof(Remarks));
                }
            }

            public string RejectReason
            {
                get => rejectReason;
                set
                {
                    rejectReason = value;
                    OnPropertyChanged(nameof(RejectReason));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            protected void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public StudentListWindow(int courseId, string courseName)
        {
            InitializeComponent();
            this.courseId = courseId;
            this.courseName = courseName;
            this.studentList = new List<StudentInfo>();

            // 初始化实时搜索计时器
            searchTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            searchTimer.Tick += SearchTimer_Tick;

            // 设置标题
            TitleText.Text = $"学生名单 - {courseName}";
            SubtitleText.Text = "查看和管理选课学生信息";
            StatusFilter.SelectedIndex = 0;

            LoadStudents();
        }

        private void LoadStudents()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"
                        SELECT 
                            s.StudentID,
                            s.Name,
                            s.Major,
                            sc.SelectionType,
                            sc.SelectionDate,
                            sc.Remarks,
                            sc.RejectReason
                        FROM StudentCourse sc
                        JOIN Student s ON sc.StudentID = s.StudentID
                        WHERE sc.CourseID = @CourseID
                        ORDER BY sc.SelectionDate DESC";

                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@CourseID", courseId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            studentList.Add(new StudentInfo
                            {
                                StudentID = reader["StudentID"].ToString(),
                                Name = reader["Name"].ToString(),
                                Major = reader["Major"].ToString(),
                                SelectionType = reader["SelectionType"].ToString(),
                                SelectionDate = Convert.ToDateTime(reader["SelectionDate"]),
                                Remarks = reader["Remarks"]?.ToString(),
                                RejectReason = reader["RejectReason"]?.ToString()
                            });
                        }
                    }
                }

                StudentGrid.ItemsSource = studentList;
                studentView = CollectionViewSource.GetDefaultView(studentList);
                studentView.Filter = StudentFilter;
                UpdateStatusCounts();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"加载学生名单失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool StudentFilter(object item)
        {
            if (!(item is StudentInfo student))
                return false;

            string searchText = SearchBox.Text.ToLower();
            bool matchesSearch = string.IsNullOrEmpty(searchText) ||
                               student.StudentID.ToLower().Contains(searchText) ||
                               student.Name.ToLower().Contains(searchText) ||
                               student.Major.ToLower().Contains(searchText);

            string selectedStatus = (StatusFilter.SelectedItem as ComboBoxItem)?.Content.ToString();
            bool matchesStatus = selectedStatus == "全部" ||
                               student.SelectionType == selectedStatus;

            return matchesSearch && matchesStatus;
        }

        private void SearchTimer_Tick(object sender, EventArgs e)
        {
            searchTimer.Stop();
            studentView?.Refresh();
            UpdateStatusCounts();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            searchTimer.Stop();
            searchTimer.Start();
        }

        private void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            studentView?.Refresh();
            UpdateStatusCounts();
        }

        private void UpdateStatusCounts()
        {
            if (studentView == null) return;

            var filteredItems = studentView.Cast<StudentInfo>().ToList();
            int total = filteredItems.Count;
            int approved = filteredItems.Count(s => s.SelectionType == "已确认");
            int pending = filteredItems.Count(s => s.SelectionType == "待审核");
            int rejected = filteredItems.Count(s => s.SelectionType == "未通过");

            TotalCountText.Text = $"总人数: {total}";
            ApprovedCountText.Text = $"已确认: {approved}";
            PendingCountText.Text = $"待审核: {pending}";
            RejectedCountText.Text = $"未通过: {rejected}";
        }

        private void ExportList_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "Excel files (*.xlsx)|*.xlsx",
                    FileName = $"{courseName}_学生名单_{DateTime.Now:yyyyMMdd}"
                };

                if (saveFileDialog.ShowDialog() != true)
                    return;

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("学生名单");

                    // 添加标题行
                    worksheet.Cell(1, 1).Value = "学号";
                    worksheet.Cell(1, 2).Value = "姓名";
                    worksheet.Cell(1, 3).Value = "专业";
                    worksheet.Cell(1, 4).Value = "选课状态";
                    worksheet.Cell(1, 5).Value = "选课时间";
                    worksheet.Cell(1, 6).Value = "备注";
                    worksheet.Cell(1, 7).Value = "退选原因";

                    // 设置标题行样式
                    var titleRow = worksheet.Row(1);
                    titleRow.Style.Font.Bold = true;
                    titleRow.Style.Fill.BackgroundColor = XLColor.LightGray;

                    // 添加数据
                    int row = 2;
                    var filteredStudents = studentList.Where(s => StudentFilter(s));
                    foreach (var student in filteredStudents)
                    {
                        worksheet.Cell(row, 1).Value = student.StudentID;
                        worksheet.Cell(row, 2).Value = student.Name;
                        worksheet.Cell(row, 3).Value = student.Major;
                        worksheet.Cell(row, 4).Value = student.SelectionType;
                        worksheet.Cell(row, 5).Value = student.SelectionDate;
                        worksheet.Cell(row, 6).Value = student.Remarks;
                        worksheet.Cell(row, 7).Value = student.RejectReason;
                        row++;
                    }

                    // 调整列宽
                    worksheet.Columns().AdjustToContents();

                    // 添加表格边框
                    var dataRange = worksheet.Range(1, 1, row - 1, 7);
                    dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                    workbook.SaveAs(saveFileDialog.FileName);
                    System.Windows.MessageBox.Show("导出成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            searchTimer.Stop();
            base.OnClosed(e);
        }
    }
}