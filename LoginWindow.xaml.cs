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

        public LoginWindow()
        {
            InitializeComponent();
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

            if (username == "admin" && password == "password")
            {
                MessageBox.Show("登录成功！");
            }
            else
            {
                MessageBox.Show("用户名或密码错误！");
            }
        }

        // 注册按钮事件
        private void SignupBtn_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("跳转到注册页面！");
        }

        // 显示/隐藏密码的事件
        private void ShowPassword_Click(object sender, RoutedEventArgs e)
        {
            // 找到触发事件的父级 Grid
            var buttonGrid = (sender as Button)?.Parent as Grid;

            if (buttonGrid != null)
            {
                // 获取 Grid 内的 Image
                var image = buttonGrid.Children[0] as Image;

                if (isPasswordVisible)
                {
                    // 切换到隐藏密码状态
                    txtPassword.Password = txtPasswordVisible.Text;
                    txtPasswordVisible.Visibility = Visibility.Collapsed;
                    txtPassword.Visibility = Visibility.Visible;
                    isPasswordVisible = false;

                    // 更改图标为睁眼图标
                    if (image != null)
                    {
                        image.Source = new System.Windows.Media.Imaging.BitmapImage(new System.Uri("eye_icon.png", System.UriKind.Relative));
                    }
                }
                else
                {
                    // 切换到显示密码状态
                    txtPasswordVisible.Text = txtPassword.Password;
                    txtPasswordVisible.Visibility = Visibility.Visible;
                    txtPassword.Visibility = Visibility.Collapsed;
                    isPasswordVisible = true;

                    // 更改图标为闭眼图标
                    if (image != null)
                    {
                        image.Source = new System.Windows.Media.Imaging.BitmapImage(new System.Uri("eye_icon_off.png", System.UriKind.Relative));
                    }
                }
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
