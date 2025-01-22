using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Configuration;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UniAcamanageWpfApp.Windows
{
    public partial class ExamScheduleEditWindow : Window
    {
        private readonly string connectionString;
        private ObservableCollection<ExamSchedule> examSchedules;

        public ExamScheduleEditWindow(string teacherId, int semesterId, int? examId = null, bool isReadOnly = false)
        {
            InitializeComponent();
            connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
            examSchedules = new ObservableCollection<ExamSchedule>();
            ExamScheduleGrid.ItemsSource = examSchedules;

            LoadSemesters();
            SetupExamTypeColumn();
            LoadInvigilators();
        }

        private void LoadSemesters()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var command = new SqlCommand(
                        "SELECT SemesterID, SemesterName FROM Semester ORDER BY StartDate DESC", conn);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            SemesterComboBox.Items.Add(new
                            {
                                ID = reader.GetInt32(0),
                                Name = reader.GetString(1)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载学期信息失败：{ex.Message}");
            }
        }

        private void SetupExamTypeColumn()
        {
            var examTypes = new[]
            {
        new { Name = "期末考试" },
        new { Name = "期中考试" },
        new { Name = "补考" },
        new { Name = "重修考试" }
    };
            ((DataGridComboBoxColumn)ExamScheduleGrid.Columns[3]).ItemsSource = examTypes;
        }

        private void LoadInvigilators()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var command = new SqlCommand(
                        "SELECT TeacherID, Name FROM Teacher ORDER BY Name", conn);

                    var teachers = new ObservableCollection<TeacherInfo>();
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            teachers.Add(new TeacherInfo
                            {
                                TeacherID = reader.GetString(0),
                                Name = reader.GetString(1)
                            });
                        }
                    }
                    ((DataGridComboBoxColumn)ExamScheduleGrid.Columns[4]).ItemsSource = teachers;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载教师信息失败：{ex.Message}");
            }
        }

        private void LoadCourses(int semesterId)
        {
            try
            {
                CourseComboBox.Items.Clear();
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var command = new SqlCommand(
                        @"SELECT CourseID, CourseCode + ' - ' + CourseName as DisplayName 
                          FROM Course 
                          WHERE SemesterID = @SemesterID", conn);
                    command.Parameters.AddWithValue("@SemesterID", semesterId);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            CourseComboBox.Items.Add(new
                            {
                                ID = reader.GetInt32(0),
                                Name = reader.GetString(1)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载课程信息失败：{ex.Message}");
            }
        }

        private void LoadExamSchedules(int courseId)
        {
            try
            {
                examSchedules.Clear();
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var command = new SqlCommand(
                        "SELECT * FROM Exam WHERE CourseID = @CourseID", conn);
                    command.Parameters.AddWithValue("@CourseID", courseId);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            examSchedules.Add(new ExamSchedule
                            {
                                ExamID = reader.GetInt32(0),
                                CourseID = reader.GetInt32(1),
                                ExamDate = reader.GetDateTime(2),
                                ExamLocation = reader.GetString(3),
                                Duration = reader.GetInt32(4),
                                ExamType = reader.GetString(5),
                                InvigilatorID = reader.GetString(6),
                                ClassID = reader.IsDBNull(7) ? null : reader.GetString(7),
                                BatchNumber = reader.GetInt32(8)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载考试安排失败：{ex.Message}");
            }
        }

        private void SemesterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SemesterComboBox.SelectedItem != null)
            {
                var selectedSemester = (dynamic)SemesterComboBox.SelectedItem;
                LoadCourses(selectedSemester.ID);
            }
        }

        private void CourseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CourseComboBox.SelectedItem != null)
            {
                var selectedCourse = (dynamic)CourseComboBox.SelectedItem;
                LoadExamSchedules(selectedCourse.ID);
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (CourseComboBox.SelectedItem != null)
            {
                var selectedCourse = (dynamic)CourseComboBox.SelectedItem;
                LoadExamSchedules(selectedCourse.ID);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CourseComboBox.SelectedItem == null)
                {
                    MessageBox.Show("请先选择课程");
                    return;
                }

                var selectedCourse = (dynamic)CourseComboBox.SelectedItem;

                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            foreach (ExamSchedule exam in examSchedules)
                            {
                                if (exam.ExamID == 0) // 新增
                                {
                                    var insertCmd = new SqlCommand(
                                        @"INSERT INTO Exam (CourseID, ExamDate, ExamLocation, Duration, 
                                          ExamType, InvigilatorID, ClassID, BatchNumber)
                                          VALUES (@CourseID, @ExamDate, @ExamLocation, @Duration,
                                          @ExamType, @InvigilatorID, @ClassID, @BatchNumber)", conn, transaction);

                                    insertCmd.Parameters.AddWithValue("@CourseID", selectedCourse.ID);
                                    insertCmd.Parameters.AddWithValue("@ExamDate", exam.ExamDate);
                                    insertCmd.Parameters.AddWithValue("@ExamLocation", exam.ExamLocation);
                                    insertCmd.Parameters.AddWithValue("@Duration", exam.Duration);
                                    insertCmd.Parameters.AddWithValue("@ExamType", exam.ExamType);
                                    insertCmd.Parameters.AddWithValue("@InvigilatorID", exam.InvigilatorID);
                                    insertCmd.Parameters.AddWithValue("@ClassID",
                                        (object)exam.ClassID ?? DBNull.Value);
                                    insertCmd.Parameters.AddWithValue("@BatchNumber", exam.BatchNumber);

                                    insertCmd.ExecuteNonQuery();
                                }
                                else // 更新
                                {
                                    var updateCmd = new SqlCommand(
                                        @"UPDATE Exam 
                                          SET ExamDate = @ExamDate,
                                              ExamLocation = @ExamLocation,
                                              Duration = @Duration,
                                              ExamType = @ExamType,
                                              InvigilatorID = @InvigilatorID,
                                              ClassID = @ClassID,
                                              BatchNumber = @BatchNumber
                                          WHERE ExamID = @ExamID", conn, transaction);

                                    updateCmd.Parameters.AddWithValue("@ExamID", exam.ExamID);
                                    updateCmd.Parameters.AddWithValue("@ExamDate", exam.ExamDate);
                                    updateCmd.Parameters.AddWithValue("@ExamLocation", exam.ExamLocation);
                                    updateCmd.Parameters.AddWithValue("@Duration", exam.Duration);
                                    updateCmd.Parameters.AddWithValue("@ExamType", exam.ExamType);
                                    updateCmd.Parameters.AddWithValue("@InvigilatorID", exam.InvigilatorID);
                                    updateCmd.Parameters.AddWithValue("@ClassID",
                                        (object)exam.ClassID ?? DBNull.Value);
                                    updateCmd.Parameters.AddWithValue("@BatchNumber", exam.BatchNumber);

                                    updateCmd.ExecuteNonQuery();
                                }
                            }

                            transaction.Commit();
                            MessageBox.Show("保存成功！");
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            throw new Exception("保存失败：" + ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败：{ex.Message}");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    public class ExamSchedule : INotifyPropertyChanged
    {
        private int _examID;
        private int _courseID;
        private DateTime _examDate;
        private string _examLocation;
        private int _duration;
        private string _examType;
        private string _invigilatorID;
        private string _classID;
        private int _batchNumber;

        public int ExamID
        {
            get => _examID;
            set
            {
                if (_examID != value)
                {
                    _examID = value;
                    OnPropertyChanged();
                }
            }
        }

        public int CourseID
        {
            get => _courseID;
            set
            {
                if (_courseID != value)
                {
                    _courseID = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime ExamDate
        {
            get => _examDate;
            set
            {
                if (_examDate != value)
                {
                    _examDate = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ExamLocation
        {
            get => _examLocation;
            set
            {
                if (_examLocation != value)
                {
                    _examLocation = value;
                    OnPropertyChanged();
                }
            }
        }

        public int Duration
        {
            get => _duration;
            set
            {
                if (_duration != value)
                {
                    _duration = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ExamType
        {
            get => _examType;
            set
            {
                if (_examType != value)
                {
                    _examType = value;
                    OnPropertyChanged();
                }
            }
        }

        public string InvigilatorID
        {
            get => _invigilatorID;
            set
            {
                if (_invigilatorID != value)
                {
                    _invigilatorID = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ClassID
        {
            get => _classID;
            set
            {
                if (_classID != value)
                {
                    _classID = value;
                    OnPropertyChanged();
                }
            }
        }

        public int BatchNumber
        {
            get => _batchNumber;
            set
            {
                if (_batchNumber != value)
                {
                    _batchNumber = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class TeacherInfo
    {
        public string TeacherID { get; set; }
        public string Name { get; set; }
        public override string ToString() => Name;
    }
}