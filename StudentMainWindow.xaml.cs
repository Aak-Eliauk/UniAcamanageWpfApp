using MaterialDesignThemes.Wpf;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UniAcamanageWpfApp.Models;
using UniAcamanageWpfApp.Views;
using Microsoft.Data.SqlClient;
using System.Configuration;

namespace UniAcamanageWpfApp
{
    public partial class StudentMainWindow : Window
    {
        // 标记侧边栏是否展开
        private bool isNavExpanded = false;

        private string _currentUserName;
        private string _currentUserRole;
        private string _studentID;
        private Student? _studentInfo;
        private static readonly string ConnectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
        public Student? StudentInfo { get; set; }
       


        public StudentMainWindow(string username, string role, string studentID)
        {
            InitializeComponent();
            SideNav.Tag = "Collapsed";

            _currentUserName = username;
            _currentUserRole = role;
            _studentID = studentID;

            // 查询学生信息
            _studentInfo = GetStudentInfo(_studentID);
            

            if (_studentInfo != null)
            {
                // 显示用户信息
                StudentInfo = GetStudentInfo(_studentID);
                UserInfoPopup.DataContext = this;
            }
            else
            {
                MessageBox.Show("未找到学生信息！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }

        // 查询学生信息的方法
        private Student? GetStudentInfo(string studentID)
        {
            try
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();
                    string query = @"SELECT s.*, c.ClassName
                                     FROM Student s
                                     INNER JOIN Class c ON s.ClassID = c.ClassID
                                     WHERE s.StudentID = @studentID";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@studentID", studentID);
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                Student student = new Student
                                {
                                    StudentID = reader["StudentID"].ToString() ?? "",
                                    Name = reader["Name"].ToString() ?? "",
                                    Gender = reader["Gender"].ToString() ?? "",
                                    BirthDate = (DateTime)reader["BirthDate"],
                                    ClassID = reader["ClassID"].ToString() ?? "",
                                    Major = reader["Major"].ToString() ?? "",
                                    Status = reader["Status"].ToString() ?? "",
                                    // 添加其他需要的字段
                                };
                                // 获取 ClassName
                                string className = reader["ClassName"].ToString() ?? "";
                                // 如果需要，可以将 className 保存到 Student 类中，或者创建一个新的属性
                                return student;
                            }
                            else
                            {
                                // 未找到对应的学生信息
                                return null;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"数据库访问出错: {ex.Message}");
                return null;
            }
        }

        // 顶部栏拖拽
        private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                this.DragMove();
        }

        // 折叠/展开按钮
        private void ToggleNavBtn_Click(object sender, RoutedEventArgs e)
        {
            isNavExpanded = !isNavExpanded;
            if (isNavExpanded)
            {
                SideNav.Tag = "Expanded";
                NavColumn.Width = new GridLength(220);
            }
            else
            {
                SideNav.Tag = "Collapsed";
                NavColumn.Width = new GridLength(60);
            }
        }

        // 导航按钮点击
        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                switch (btn.Name)
                {
                    case "BtnHome":
                        // 切换到 HomeView
                        MainContentPresenter.Content = new HomeView();
                        break;
                    case "BtnInfoQuery":
                        // 切换到 InfoQueryView
                        MainContentPresenter.Content = new InfoQueryView();
                        break;
                    case "BtnSelectCourse":
                        // 切换到 CourseSelectionView
                        MainContentPresenter.Content = new CourseSelectionView();
                        break;
                    case "BtnAcademic":
                        MainContentPresenter.Content = new AcademicStatusView(_studentID);
                        break;
                    // 其余的按钮
                    default:
                        MainContentPresenter.Content = new TextBlock
                        {
                            Text = $"[{btn.Name}] - 占位内容",
                            FontSize = 24,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        break;
                }
            }
        }

        // 用户头像 => Popup
        private void UserAvatarBtn_Click(object sender, RoutedEventArgs e)
        {
            UserInfoPopup.IsOpen = !UserInfoPopup.IsOpen;
        }

        // 退出登录
        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }

        // 最小化
        private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        // 最大化/还原
        private void MaximizeRestoreBtn_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                if (MaxRestoreButton.Content is PackIcon icon)
                    icon.Kind = PackIconKind.WindowMaximize;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                if (MaxRestoreButton.Content is PackIcon icon)
                    icon.Kind = PackIconKind.WindowRestore;
            }
        }

        // 关闭
        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
