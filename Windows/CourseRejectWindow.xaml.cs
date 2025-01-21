using System;
using Microsoft.Data.SqlClient;
using System.Windows;

namespace UniAcamanageWpfApp.Windows
{
    public partial class CourseRejectWindow : Window
    {
        private readonly string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
        private readonly int selectionId;
        private string studentId;
        private int courseId;

        public event EventHandler RejectConfirmed;

        public CourseRejectWindow(int selectionId)
        {
            InitializeComponent();
            this.selectionId = selectionId;
            LoadSelectionDetails();
        }

        private void LoadSelectionDetails()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"
                        SELECT 
                            sc.StudentID,
                            sc.CourseID,
                            s.Name AS StudentName,
                            s.Major,
                            c.CourseCode,
                            c.CourseName
                        FROM StudentCourse sc
                        JOIN Student s ON sc.StudentID = s.StudentID
                        JOIN Course c ON sc.CourseID = c.CourseID
                        WHERE sc.Id = @SelectionId";

                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@SelectionId", selectionId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            studentId = reader["StudentID"].ToString();
                            courseId = Convert.ToInt32(reader["CourseID"]);
                            string studentName = reader["StudentName"].ToString();
                            string major = reader["Major"].ToString();
                            string courseCode = reader["CourseCode"].ToString();
                            string courseName = reader["CourseName"].ToString();

                            StudentInfoText.Text = $"学生: {studentName} ({studentId}) - {major}";
                            CourseInfoText.Text = $"课程: {courseName} ({courseCode})";
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

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(RejectReasonText.Text))
            {
                MessageBox.Show("请输入拒绝原因", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand(@"
                UPDATE StudentCourse 
                SET SelectionType = '未通过',
                    RejectReason = @RejectReason,
                    ApproverID = @ApproverID,
                    ApprovalDate = @ApprovalDate
                WHERE Id = @Id", conn);

                    cmd.Parameters.AddWithValue("@Id", selectionId);
                    cmd.Parameters.AddWithValue("@RejectReason", RejectReasonText.Text.Trim());
                    cmd.Parameters.AddWithValue("@ApproverID", GlobalUserState.LinkedID);
                    cmd.Parameters.AddWithValue("@ApprovalDate", DateTime.Now);
                    cmd.ExecuteNonQuery();

                    RejectConfirmed?.Invoke(this, EventArgs.Empty);
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}