using System;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using Microsoft.Data.SqlClient;

namespace UniAcamanageWpfApp
{
    public partial class ForgotPasswordStep2Window : Window
    {
        private const string ConnectionString = "Server=LAPTOP-9ALD8MJ8\\SQLSERVER;Database=UniAcademicDB;User Id=sa;Password=Aak995498;TrustServerCertificate=True;";
        private readonly string input; // 用户输入的用户名/邮箱/学号
        private readonly string securityQuestion; // 从数据库获取的密保问题

        public ForgotPasswordStep2Window(string input, string securityQuestion)
        {
            InitializeComponent();
            this.input = input;
            this.securityQuestion = securityQuestion;

            lblSecurityQuestion.Text = securityQuestion;
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            string securityAnswer = txtSecurityAnswer.Text.Trim();

            if (string.IsNullOrEmpty(securityAnswer))
            {
                MessageBox.Show("请输入密保答案！");
                return;
            }

            try
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();
                    string query = @"
                        SELECT SecurityAnswerHash, Salt 
                        FROM Users 
                        WHERE Username = @Input OR Email = @Input OR LinkedID = @Input";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Input", input);

                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string storedHash = reader["SecurityAnswerHash"].ToString();
                                string salt = reader["Salt"].ToString();

                                string computedHash = ComputeHash(securityAnswer, salt);

                                if (storedHash == computedHash)
                                {
                                    // 密保验证成功，跳转到重置密码窗口
                                    var step3Window = new ForgotPasswordStep3Window(input, securityAnswer);
                                    step3Window.Show();
                                    this.Close();
                                }
                                else
                                {
                                    MessageBox.Show("密保答案错误，请重试！");
                                }
                            }
                            else
                            {
                                MessageBox.Show("用户数据丢失，请重试！");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"数据库访问出错: {ex.Message}");
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            var step1Window = new ForgotPasswordStep1Window();
            step1Window.Show();
            this.Close();
        }

        private void ExitApp_Click(object sender, RoutedEventArgs e)
        {
            // 显示登录窗口
            var loginWindow = new LoginWindow();
            loginWindow.Show();

            // 关闭当前窗口
            this.Close();
        }

        private string ComputeHash(string input, string salt)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(input + salt);
                byte[] hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }
    }
}
