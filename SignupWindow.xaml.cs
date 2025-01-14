using System;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;

namespace UniAcamanageWpfApp
{
    public partial class SignupWindow : Window
    {
        private const string ConnectionString = "Server=LAPTOP-9ALD8MJ8\\SQLSERVER;Database=UniAcademicDB;User Id=sa;Password=Aak995498;TrustServerCertificate=True;";

        // 添加 RegisteredUsername 属性，用于存储注册成功后的用户名
        public string RegisteredUsername { get; private set; }

        public SignupWindow()
        {
            InitializeComponent();
        }

        // 创建账户按钮点击事件
        private void BtnCreateAccount_Click(object sender, RoutedEventArgs e)
        {
            string role = (cbRole.SelectedItem as ComboBoxItem)?.Content.ToString();
            if (role == "学生") role = "Student";
            if (role == "教师") role = "Teacher";

            string linkedID = txtLinkedID.Text.Trim();
            string username = txtUsername.Text.Trim();
            string email = txtEmail.Text.Trim();
            string password = txtPassword.Password.Trim();
            string securityQuestion = (cbSecurityQuestion.SelectedItem as ComboBoxItem)?.Content.ToString();
            string securityAnswer = txtSecurityAnswer.Text.Trim();

            // 检查必填项
            if (string.IsNullOrEmpty(role) || string.IsNullOrEmpty(linkedID) || string.IsNullOrEmpty(username) ||
                string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(securityQuestion) ||
                string.IsNullOrEmpty(securityAnswer))
            {
                MessageBox.Show("请填写所有信息！");
                return;
            }

            if (!IsValidEmail(email))
            {
                MessageBox.Show("请输入有效的邮箱地址！");
                return;
            }

            if (IsUsernameExists(username))
            {
                MessageBox.Show("用户名已存在，请更换用户名！");
                return;
            }

            if (IsLinkedIDExists(linkedID))
            {
                MessageBox.Show("学号/教师号已被注册，请检查后重试！");
                return;
            }

            if (!ValidateLinkedID(linkedID, role))
            {
                MessageBox.Show("学号/教师号无效，请检查后重试！");
                return;
            }

            string salt = GenerateSalt();
            string passwordHash = ComputeHash(password, salt);
            string securityAnswerHash = ComputeHash(securityAnswer, salt);

            try
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();
                    string query = @"
                    INSERT INTO Users (Username, PasswordHash, Salt, Role, Email, SecurityQuestion, SecurityAnswerHash, LinkedID)
                    VALUES (@Username, @PasswordHash, @Salt, @Role, @Email, @SecurityQuestion, @SecurityAnswerHash, @LinkedID)";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Username", username);
                        command.Parameters.AddWithValue("@PasswordHash", passwordHash);
                        command.Parameters.AddWithValue("@Salt", salt);
                        command.Parameters.AddWithValue("@Role", role);
                        command.Parameters.AddWithValue("@Email", email);
                        command.Parameters.AddWithValue("@SecurityQuestion", securityQuestion);
                        command.Parameters.AddWithValue("@SecurityAnswerHash", securityAnswerHash);
                        command.Parameters.AddWithValue("@LinkedID", linkedID);

                        command.ExecuteNonQuery();
                        MessageBox.Show("账户创建成功！");

                        // 设置 RegisteredUsername 属性
                        RegisteredUsername = username;

                        // 设置 DialogResult 并关闭窗口
                        this.DialogResult = true;
                        this.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建账户失败：{ex.Message}");
            }
        }

        // 验证邮箱地址是否有效
        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
        // 验证学号/教师号是否有效
        private bool ValidateLinkedID(string linkedID, string role)
        {
            if (role != "Student" && role != "Teacher") return false;

            string query = role == "Student"
                ? "SELECT COUNT(*) FROM Student WHERE StudentID = @LinkedID"
                : "SELECT COUNT(*) FROM Teacher WHERE TeacherID = @LinkedID";

            try
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@LinkedID", linkedID);
                        int count = (int)command.ExecuteScalar();
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"校验学号/教师号时出错: {ex.Message}");
                return false;
            }
        }
        // 检查用户名是否存在
        private bool IsUsernameExists(string username)
        {
            string query = "SELECT COUNT(*) FROM Users WHERE Username = @Username";

            try
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Username", username);
                        int count = (int)command.ExecuteScalar();
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"检查用户名是否存在时出错: {ex.Message}");
                return true;
            }
        }
        // 检查学号/教师号是否存在
        private bool IsLinkedIDExists(string linkedID)
        {
            string query = "SELECT COUNT(*) FROM Users WHERE LinkedID = @LinkedID";

            try
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@LinkedID", linkedID);
                        int count = (int)command.ExecuteScalar();
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"检查学号/教师号是否存在时出错: {ex.Message}");
                return true;
            }
        }

        // 生成盐
        private string GenerateSalt()
        {
            byte[] saltBytes = new byte[16];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(saltBytes);
            }
            return Convert.ToBase64String(saltBytes);
        }
        // 计算哈希
        private string ComputeHash(string input, string salt)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(input + salt);
                byte[] hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        // 退出按钮点击事件
        private void ExitApp_Click(object sender, RoutedEventArgs e)
        {
            this.Close(); // 关闭窗口
        }

        // 返回按钮点击事件
        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
