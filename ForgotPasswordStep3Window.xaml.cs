using System;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using Microsoft.Data.SqlClient;

namespace UniAcamanageWpfApp
{
    public partial class ForgotPasswordStep3Window : Window
    {
        private const string ConnectionString = "Server=LAPTOP-9ALD8MJ8\\SQLSERVER;Database=UniAcademicDB;User Id=sa;Password=Aak995498;TrustServerCertificate=True;";
        private readonly string input; // 用户输入的用户名/邮箱/学号
        private readonly string securityAnswer; // 第二步传递过来的密保答案

        public ForgotPasswordStep3Window(string input, string securityAnswer)
        {
            InitializeComponent();
            this.input = input;
            this.securityAnswer = securityAnswer;
        }

        private void BtnResetPassword_Click(object sender, RoutedEventArgs e)
        {
            string newPassword = txtNewPassword.Password.Trim();
            string confirmPassword = txtConfirmPassword.Password.Trim();

            if (string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmPassword))
            {
                MessageBox.Show("请输入新密码和确认密码！");
                return;
            }

            if (newPassword != confirmPassword)
            {
                MessageBox.Show("两次输入的密码不一致，请重试！");
                return;
            }

            // 生成新的盐值
            string salt = GenerateSalt();
            string passwordHash = ComputeHash(newPassword, salt);
            string securityAnswerHash = ComputeHash(securityAnswer, salt); // 重新计算密保答案哈希

            try
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();
                    string query = @"
                UPDATE Users 
                SET PasswordHash = @PasswordHash, Salt = @Salt, SecurityAnswerHash = @SecurityAnswerHash
                WHERE Username = @Input OR Email = @Input OR LinkedID = @Input";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@PasswordHash", passwordHash);
                        command.Parameters.AddWithValue("@Salt", salt);
                        command.Parameters.AddWithValue("@SecurityAnswerHash", securityAnswerHash); // 更新密保答案哈希
                        command.Parameters.AddWithValue("@Input", input);

                        command.ExecuteNonQuery();
                        MessageBox.Show("密码重置成功！");

                        // 返回登录窗口并填充用户名
                        LoginWindow loginWindow = new LoginWindow(input);
                        loginWindow.Show();
                        this.Close();
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
            var step2Window = new ForgotPasswordStep2Window(input, string.Empty);
            step2Window.Show();
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

        private string GenerateSalt()
        {
            byte[] saltBytes = new byte[16];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(saltBytes);
            }
            return Convert.ToBase64String(saltBytes);
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
