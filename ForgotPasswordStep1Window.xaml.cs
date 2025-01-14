using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;

namespace UniAcamanageWpfApp
{
    public partial class ForgotPasswordStep1Window : Window
    {
        private const string ConnectionString = "Server=LAPTOP-9ALD8MJ8\\SQLSERVER;Database=UniAcademicDB;User Id=sa;Password=Aak995498;TrustServerCertificate=True;";

        public ForgotPasswordStep1Window()
        {
            InitializeComponent();
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            string input = txtInput.Text.Trim();

            if (string.IsNullOrEmpty(input))
            {
                MessageBox.Show("请输入用户名、邮箱或学号/教师号！");
                return;
            }

            try
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();
                    string query = "SELECT SecurityQuestion FROM Users WHERE Username = @Input OR Email = @Input OR LinkedID = @Input";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Input", input);
                        var securityQuestion = command.ExecuteScalar();

                        if (securityQuestion != null)
                        {
                            var step2Window = new ForgotPasswordStep2Window(input, securityQuestion.ToString());
                            step2Window.Show();
                            this.Close();
                        }
                        else
                        {
                            MessageBox.Show("未找到该账号，请确认输入信息！");
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
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }

        private void ExitApp_Click(object sender, RoutedEventArgs e)
        {
            var loginWindow = new LoginWindow();
            loginWindow.Show();

            // 关闭当前找回密码窗口
            this.Close();
        }
    }
}
