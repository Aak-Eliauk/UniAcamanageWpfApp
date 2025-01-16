using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using UniAcamanageWpfApp.Views;

namespace UniAcamanageWpfApp
{
    public partial class StudentMainWindow : Window
    {
        // 标记侧边栏是否展开
        private bool isNavExpanded = false;

        public StudentMainWindow()
        {
            InitializeComponent();
            SideNav.Tag = "Collapsed"; // 默认折叠
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
                        MainContentPresenter.Content = new TextBlock
                        {
                            Text = "首页 - 占位内容",
                            FontSize = 24,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        break;
                    case "BtnInfoQuery":
                        MainContentPresenter.Content = new TextBlock
                        {
                            Text = "信息查询 - 占位内容",
                            FontSize = 24,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        break;
                    case "BtnSelectCourse":
                        MainContentPresenter.Content = new TextBlock
                        {
                            Text = "选课中心 - 占位内容",
                            FontSize = 24,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        break;
                    case "BtnAcademic":
                        MainContentPresenter.Content = new TextBlock
                        {
                            Text = "学业情况 - 占位内容",
                            FontSize = 24,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        break;
                    case "BtnApply":
                        MainContentPresenter.Content = new TextBlock
                        {
                            Text = "报名申请 - 占位内容",
                            FontSize = 24,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        break;
                    case "BtnMap":
                        MainContentPresenter.Content = new TextBlock
                        {
                            Text = "教课通 - 占位内容(地图等)",
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
