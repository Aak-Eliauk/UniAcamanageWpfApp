using System;
using System.Windows;
using System.Windows.Threading;

namespace UniAcamanageWpfApp.Views
{
    public partial class NotificationWindow : Window
    {
        private readonly SystemNotification _notification;
        private readonly DispatcherTimer _timer;

        // 默认构造函数
        public NotificationWindow()
        {
            InitializeComponent();
        }

        // 带参数的构造函数
        public NotificationWindow(SystemNotification notification) : this()
        {
            _notification = notification;
            DataContext = notification;

            // 设置窗口位置（右下角）
            var workingArea = SystemParameters.WorkArea;
            Left = workingArea.Right - Width - 20;
            Top = workingArea.Bottom - Height - 20;

            // 设置自动关闭计时器
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            _timer.Stop();
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _timer?.Stop();
        }
    }
}