using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;


namespace UniAcamanageWpfApp.Windows
{
    public partial class GradeEntryWindow : Window
    {
        private readonly string connectionString;
        private readonly StudentGradeInfo studentGradeInfo;
        private readonly string currentTeacherId;
        private readonly bool isEdit;

        public class StudentGradeInfo
        {
            public int? GradeID { get; set; }
            public string StudentID { get; set; }
            public string StudentName { get; set; }
            public int CourseID { get; set; }
            public string CourseCode { get; set; }
            public string CourseName { get; set; }
            public string ClassID { get; set; }
            public decimal Credit { get; set; }
            public decimal? Score { get; set; }
            public decimal? ExistingScore { get; set; }
            public bool IsRetest { get; set; }
            public bool ExistingIsRetest { get; set; }
            public int SemesterID { get; set; }
            public string ModifiedBy { get; set; }
            public DateTime ModifiedAt { get; set; }
            public int AttemptNumber { get; set; }
            public string GradeLevel { get; set; }
            public decimal? BaseGradePoint { get; set; }
            public decimal? WeightedGradePoint { get; set; }
        }

        public GradeEntryWindow(StudentGradeInfo info, string teacherId, bool isEdit = false)
        {
            InitializeComponent();
            connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
            studentGradeInfo = info;
            currentTeacherId = teacherId;
            this.isEdit = isEdit;

            LoadStudentInfo();
            if (isEdit)
            {
                LoadExistingGrade();
                TitleText.Text = "修改成绩";
                SubtitleText.Text = "修改学生成绩信息";
            }
        }

        private void LoadStudentInfo()
        {
            StudentIdText.Text = studentGradeInfo.StudentID;
            StudentNameText.Text = studentGradeInfo.StudentName;
            ClassText.Text = studentGradeInfo.ClassID;
            CourseNameText.Text = studentGradeInfo.CourseName;
            CreditText.Text = studentGradeInfo.Credit.ToString("F1");
        }

        private void LoadExistingGrade()
        {
            if (studentGradeInfo.ExistingScore.HasValue)
            {
                ScoreTextBox.Text = studentGradeInfo.ExistingScore.Value.ToString("F2");
            }
            IsRetestCheckBox.IsChecked = studentGradeInfo.IsRetest;
        }

        private async void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateInput())
                    return;

                decimal score = decimal.Parse(ScoreTextBox.Text);
                bool isRetest = IsRetestCheckBox.IsChecked ?? false;
                string remarks = RemarksTextBox.Text;

                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            if (isEdit && studentGradeInfo.GradeID.HasValue)
                            {
                                // 更新现有成绩
                                await UpdateGrade(conn, transaction, score, isRetest, remarks);
                            }
                            else
                            {
                                // 插入新成绩
                                await InsertGrade(conn, transaction, score, isRetest, remarks);
                            }

                            transaction.Commit();
                            MessageBox.Show("成绩保存成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                            DialogResult = true;
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
                MessageBox.Show($"操作失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task UpdateGrade(SqlConnection conn, SqlTransaction transaction, decimal score, bool isRetest, string remarks)
        {
            string updateSql = @"
            UPDATE Grade 
            SET Score = @Score, 
                IsRetest = @IsRetest,
                ModifiedBy = @ModifiedBy,
                ModifiedAt = @ModifiedAt
            WHERE GradeID = @GradeID";

            using (var cmd = new SqlCommand(updateSql, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@Score", score);
                cmd.Parameters.AddWithValue("@IsRetest", isRetest);
                cmd.Parameters.AddWithValue("@ModifiedBy", currentTeacherId);
                cmd.Parameters.AddWithValue("@ModifiedAt", DateTime.Now);
                cmd.Parameters.AddWithValue("@GradeID", studentGradeInfo.GradeID.Value);

                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task InsertGrade(SqlConnection conn, SqlTransaction transaction, decimal score, bool isRetest, string remarks)
        {
            string insertSql = @"
            INSERT INTO Grade (StudentID, CourseID, Score, IsRetest, SemesterID, ModifiedBy, ModifiedAt, AttemptNumber)
            VALUES (@StudentID, @CourseID, @Score, @IsRetest, @SemesterID, @ModifiedBy, @ModifiedAt, 
                    (SELECT ISNULL(MAX(AttemptNumber), 0) + 1 
                     FROM Grade 
                     WHERE StudentID = @StudentID AND CourseID = @CourseID))";

            using (var cmd = new SqlCommand(insertSql, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@StudentID", studentGradeInfo.StudentID);
                cmd.Parameters.AddWithValue("@CourseID", studentGradeInfo.CourseID);
                cmd.Parameters.AddWithValue("@Score", score);
                cmd.Parameters.AddWithValue("@IsRetest", isRetest);
                cmd.Parameters.AddWithValue("@SemesterID", studentGradeInfo.SemesterID);
                cmd.Parameters.AddWithValue("@ModifiedBy", currentTeacherId);
                cmd.Parameters.AddWithValue("@ModifiedAt", DateTime.Now);

                await cmd.ExecuteNonQueryAsync();
            }
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(ScoreTextBox.Text))
            {
                MessageBox.Show("请输入成绩！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!decimal.TryParse(ScoreTextBox.Text, out decimal score))
            {
                MessageBox.Show("成绩必须是数字！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (score < 0 || score > 100)
            {
                MessageBox.Show("成绩必须在0-100之间！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
