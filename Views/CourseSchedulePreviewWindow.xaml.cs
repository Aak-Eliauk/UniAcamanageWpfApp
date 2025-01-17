using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UniAcamanageWpfApp.Models;

namespace UniAcamanageWpfApp.Views
{
    public partial class CourseSchedulePreviewWindow : Window
    {
        private readonly List<CourseScheduleItem> _selectedCourses;
        private readonly Color[] _courseColors = new Color[]
        {
            (Color)ColorConverter.ConvertFromString("#E3F2FD"),  // 浅蓝
            (Color)ColorConverter.ConvertFromString("#F3E5F5"),  // 浅紫
            (Color)ColorConverter.ConvertFromString("#E8F5E9"),  // 浅绿
            (Color)ColorConverter.ConvertFromString("#FFF3E0"),  // 浅橙
            (Color)ColorConverter.ConvertFromString("#FFEBEE"),  // 浅红
            (Color)ColorConverter.ConvertFromString("#F5F5F5"),  // 浅灰
            (Color)ColorConverter.ConvertFromString("#E0F7FA")   // 浅青
        };

        public CourseSchedulePreviewWindow(List<CourseScheduleItem> selectedCourses)
        {
            InitializeComponent();
            _selectedCourses = selectedCourses;
            Title = $"课表预览 - {GlobalUserState.Username}";
            LoadSchedule();
        }

        private void LoadSchedule()
        {
            // 添加时间节次标签
            for (int i = 1; i <= 11; i++)
            {
                var timeLabel = new TextBlock
                {
                    Text = $"第{i}节",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(5),
                    FontSize = 12
                };
                Grid.SetRow(timeLabel, i);
                Grid.SetColumn(timeLabel, 0);
                ScheduleGrid.Children.Add(timeLabel);
            }

            // 添加课程项
            int colorIndex = 0;
            foreach (var course in _selectedCourses)
            {
                var courseCard = CreateCourseCard(course, _courseColors[colorIndex % _courseColors.Length]);
                Grid.SetRow(courseCard, course.StartPeriod);
                Grid.SetColumn(courseCard, course.DayOfWeek);
                Grid.SetRowSpan(courseCard, course.Duration);
                ScheduleGrid.Children.Add(courseCard);
                colorIndex++;
            }
        }

        private Border CreateCourseCard(CourseScheduleItem course, Color backgroundColor)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(backgroundColor),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(2),
                Padding = new Thickness(5),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 0, 0, 0)),
                BorderThickness = new Thickness(1)
            };

            var stackPanel = new StackPanel
            {
                Margin = new Thickness(3)
            };

            // 课程名称
            stackPanel.Children.Add(new TextBlock
            {
                Text = course.CourseName,
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 3)
            });

            // 课程代码
            stackPanel.Children.Add(new TextBlock
            {
                Text = course.CourseCode,
                FontSize = 10,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666")),
                Margin = new Thickness(0, 0, 0, 3)
            });

            // 教室
            if (!string.IsNullOrEmpty(course.Classroom))
            {
                stackPanel.Children.Add(new TextBlock
                {
                    Text = course.Classroom,
                    FontSize = 10,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666"))
                });
            }

            border.Child = stackPanel;

            // 添加工具提示
            border.ToolTip = new ToolTip
            {
                Content = $"课程：{course.CourseName}\n" +
                         $"课程代码：{course.CourseCode}\n" +
                         $"教室：{course.Classroom}\n" +
                         $"节次：第{course.StartPeriod}-{course.StartPeriod + course.Duration - 1}节"
            };

            return border;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}