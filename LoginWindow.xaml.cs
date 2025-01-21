using System;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;

namespace UniAcamanageWpfApp
{
    public partial class LoginWindow : Window
    {
        private bool isPasswordVisible = false; // 密码是否可见
        public bool IsDarkTheme { get; set; }
        private readonly PaletteHelper paletteHelper = new PaletteHelper();

        private const string ConnectionString = "Server=LAPTOP-9ALD8MJ8\\SQLSERVER;Database=UniAcademicDB;User Id=sa;Password=Aak995498;TrustServerCertificate=True;";

        public string linkedID;
        public LoginWindow()
        {
            InitializeComponent();
        }

        // 新的构造函数，接收用户名参数
        public LoginWindow(string username) : this()
        {
            txtUserName.Text = username; // 将用户名填入登录界面的用户名输入框
        }

        // 切换主题的事件
        private void themeToggle_Click(object sender, RoutedEventArgs e)
        {
            ITheme theme = paletteHelper.GetTheme();
            if (IsDarkTheme)
            {
                theme.SetBaseTheme(Theme.Light);
                IsDarkTheme = false;
            }
            else
            {
                theme.SetBaseTheme(Theme.Dark);
                IsDarkTheme = true;
            }
            paletteHelper.SetTheme(theme);
        }

        // 退出按钮事件
        private void exitApp(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // 登录按钮事件
        private void LoginBtn_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUserName.Text.Trim();
            string password = txtPassword.Password.Trim();
            

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("请输入用户名和密码！");
                return;
            }

            if (AuthenticateUser(username, password, out string role))
            {
                // 登录成功后，将全局状态写入
                GlobalUserState.LinkedID = linkedID; // linkedID 是你的私有字段
                GlobalUserState.Role = role;
                GlobalUserState.Username = username; // 如果需要存用户名

                MessageBox.Show($"登录成功！当前角色：{role}");
                // 根据role跳转不同主界面
                switch (role.ToLower())
                {
                    case "student":
                        var studentWin = new StudentMainWindow(username, role, linkedID);
                        studentWin.Show();
                        this.Close();
                        break;
                    case "teacher":
                        var teacherWin = new TeacherMainWindow(username, role, linkedID);
                        teacherWin.Show();
                        this.Close();
                        break;
                    case "admin":
                        var adminWin = new TeacherMainWindow(username, role, linkedID);
                        adminWin .Show();
                        this.Close();
                        break;
                    default:
                        // 未知角色
                        MessageBox.Show("未知角色，无法跳转");
                        break;
                }
            }
            else
            {
                MessageBox.Show("用户名或密码错误！");
            }
        }

        // 注册按钮事件
        private void SignupBtn_Click(object sender, RoutedEventArgs e)
        {
            SignupWindow signupWindow = new SignupWindow();
            this.Hide(); // 隐藏登录窗口

            bool? result = signupWindow.ShowDialog();
            if (result == true) // 如果注册成功
            {
                txtUserName.Text = signupWindow.RegisteredUsername; // 自动填入注册的用户名
            }

            this.Show(); // 显示登录窗口
        }


        // 忘记密码按钮事件
        private void ForgotPassword_Click(object sender, RoutedEventArgs e)
        {
            // 创建忘记密码窗口实例
            ForgotPasswordStep1Window forgotPasswordWindow = new ForgotPasswordStep1Window();

            // 显示忘记密码窗口
            forgotPasswordWindow.Show();

            // 隐藏当前登录窗口
            this.Hide();
        }


        // 显示/隐藏密码的事件
        private void ShowPassword_Click(object sender, RoutedEventArgs e)
        {
            var buttonGrid = (sender as Button)?.Parent as Grid;

            if (buttonGrid != null)
            {
                var image = buttonGrid.Children[0] as Image;

                if (isPasswordVisible)
                {
                    txtPassword.Password = txtPasswordVisible.Text;
                    txtPasswordVisible.Visibility = Visibility.Collapsed;
                    txtPassword.Visibility = Visibility.Visible;
                    isPasswordVisible = false;

                    if (image != null)
                    {
                        image.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("Resources/Images/eye_icon.png", UriKind.Relative));
                    }
                }
                else
                {
                    txtPasswordVisible.Text = txtPassword.Password;
                    txtPasswordVisible.Visibility = Visibility.Visible;
                    txtPassword.Visibility = Visibility.Collapsed;
                    isPasswordVisible = true;

                    if (image != null)
                    {
                        image.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("Resources/Images/eye_icon_off.png", UriKind.Relative));
                    }
                }
            }
        }

        // 用户认证
        private bool AuthenticateUser(string input, string password, out string role)
        {
            role = string.Empty;

            try
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();
                    string query = @"SELECT PasswordHash, Salt, Role, LinkedID FROM Users WHERE Username = @Input OR Email = @Input OR LinkedID = @Input";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Input", input);
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string storedHash = reader["PasswordHash"].ToString();
                                string salt = reader["Salt"].ToString();
                                role = reader["Role"].ToString();
                                linkedID = reader["LinkedID"].ToString();
                                string computedHash = ComputeHash(password, salt);
                                return storedHash == computedHash;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"数据库访问出错: {ex.Message}");
            }
            return false;
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

        // 窗口拖动支持
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            DragMove();
        }
    }
}
