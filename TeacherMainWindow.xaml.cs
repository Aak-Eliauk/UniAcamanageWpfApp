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
    public partial class TeacherMainWindow : Window
    {
        // 标记侧边栏是否展开
        private bool isNavExpanded = false;

        private string _currentUserName;
        private string _currentUserRole;
        private string _teacherID;
        private Teacher? _teacherInfo;
        private static readonly string ConnectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
        public Teacher? TeacherInfo { get; set; }

        public TeacherMainWindow(string username, string role, string teacherID)
        {
            InitializeComponent();
            SideNav.Tag = "Collapsed";

            _currentUserName = username;
            _currentUserRole = role;
            _teacherID = teacherID;

            // 查询教师信息
            _teacherInfo = GetTeacherInfo(_teacherID);

            if (_teacherInfo != null)
            {
                // 显示用户信息
                TeacherInfo = GetTeacherInfo(_teacherID);
                UserInfoPopup.DataContext = this;
            }
            else
            {
                MessageBox.Show("未找到教师信息！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 查询教师信息的方法
        private Teacher? GetTeacherInfo(string teacherID)
        {
            try
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();
                    // 修改查询语句，使用LEFT JOIN以防止部门信息缺失
                    string query = @"
                SELECT t.TeacherID, 
                       t.Name, 
                       t.Title, 
                       t.Phone, 
                       t.Email, 
                       t.DepartmentID,
                       d.DepartmentName
                FROM Teacher t
                LEFT JOIN Department d ON t.DepartmentID = d.DepartmentID
                WHERE t.TeacherID = @teacherID";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@teacherID", teacherID);
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new Teacher
                                {
                                    TeacherID = reader["TeacherID"]?.ToString() ?? "",
                                    Name = reader["Name"]?.ToString() ?? "",
                                    Title = reader["Title"]?.ToString() ?? "",
                                    Phone = reader["Phone"]?.ToString() ?? "",
                                    Email = reader["Email"]?.ToString() ?? "",
                                    DepartmentID = reader["DepartmentID"]?.ToString() ?? "",
                                    DepartmentName = reader["DepartmentName"]?.ToString() ?? "未知院系"
                                };
                            }
                        }
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"获取教师信息失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        // 切换到 TeacherInfoView
                        MainContentPresenter.Content = new TeacherInfoView();
                        break;
                    case "BtnCourseManage":
                        // 切换到 CourseManagementView
                        MainContentPresenter.Content = new TextBlock
                        {
                            Text = $"[{btn.Name}] - 占位内容",
                            FontSize = 24,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        break;
                    case "BtnGradeManage":
                        // 切换到 GradeManagementView
                        MainContentPresenter.Content = new TextBlock
                        {
                            Text = $"[{btn.Name}] - 占位内容",
                            FontSize = 24,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        break;
                    case "BtnInfoManage":
                        // 切换到 InfoManagementView
                        MainContentPresenter.Content = new TextBlock
                        {
                            Text = $"[{btn.Name}] - 占位内容",
                            FontSize = 24,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        break;
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