using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UniAcamanageWpfApp.Models;

namespace UniAcamanageWpfApp.Views
{
    public class CourseScheduleItem
    {
        public string CourseName { get; set; }
        public string CourseCode { get; set; }
        public string Classroom { get; set; }
        public int DayOfWeek { get; set; }
        public int StartPeriod { get; set; }
        public int EndPeriod { get; set; }
        public int StartWeek { get; set; }
        public int EndWeek { get; set; }
        public string WeekType { get; set; } // A为单周，B为双周，null为每周

        // 计算持续节数
        public int Duration => EndPeriod - StartPeriod + 1;
    }

    public partial class CourseSchedulePreviewWindow : Window
    {
        private readonly List<Course> _originalCourses;
        private int _currentWeek;
        private bool _isCurrentWeekOdd;

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

        private class ScheduleTimeSlot
        {
            public int DayOfWeek { get; set; }
            public int StartPeriod { get; set; }
            public int EndPeriod { get; set; }
            public int StartWeek { get; set; }
            public int EndWeek { get; set; }
            public string WeekType { get; set; }
        }

        public CourseSchedulePreviewWindow(List<Course> courses, int currentWeek = 1)
        {
            InitializeComponent();
            _originalCourses = courses;
            _currentWeek = currentWeek;
            _isCurrentWeekOdd = currentWeek % 2 == 1;

            // 初始化周次选择器
            for (int i = 1; i <= 25; i++)
            {
                WeekSelector.Items.Add($"第{i}周");
            }
            WeekSelector.SelectedIndex = currentWeek - 1;
            WeekSelector.SelectionChanged += WeekSelector_SelectionChanged;

            UpdateTitle();
            LoadSchedule();
        }

        private void LoadSchedule()
        {
            // 清除现有课程显示
            var coursesToRemove = new List<UIElement>();
            foreach (UIElement element in ScheduleGrid.Children)
            {
                if (element is Border && Grid.GetColumn(element) > 0)
                {
                    coursesToRemove.Add(element);
                }
            }
            foreach (var element in coursesToRemove)
            {
                ScheduleGrid.Children.Remove(element);
            }

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
            foreach (var course in _originalCourses)
            {
                if (string.IsNullOrEmpty(course.ScheduleTime))
                    continue;

                var timeSlots = ParseScheduleTime(course.ScheduleTime);
                foreach (var slot in timeSlots)
                {
                    if (ShouldShowCourse(slot))
                    {
                        var courseCard = CreateCourseCard(course, slot, _courseColors[colorIndex % _courseColors.Length]);
                        Grid.SetRow(courseCard, slot.StartPeriod);
                        Grid.SetColumn(courseCard, slot.DayOfWeek);
                        Grid.SetRowSpan(courseCard, slot.EndPeriod - slot.StartPeriod + 1);
                        ScheduleGrid.Children.Add(courseCard);
                    }
                }
                colorIndex++;
            }
        }
        private List<ScheduleTimeSlot> ParseScheduleTime(string scheduleTime)
        {
            var result = new List<ScheduleTimeSlot>();
            if (string.IsNullOrEmpty(scheduleTime))
                return result;

            var timeSlots = scheduleTime.Split(',');
            foreach (var slot in timeSlots)
            {
                var parts = slot.Split('-');
                if (parts.Length < 5) continue;

                try
                {
                    result.Add(new ScheduleTimeSlot
                    {
                        DayOfWeek = int.Parse(parts[0]),
                        StartPeriod = int.Parse(parts[1]),
                        EndPeriod = int.Parse(parts[2]),
                        StartWeek = int.Parse(parts[3]),
                        EndWeek = int.Parse(parts[4]),
                        WeekType = parts.Length > 5 ? parts[5] : null
                    });
                }
                catch (FormatException)
                {
                    // 如果解析失败，跳过这个时间段
                    continue;
                }
            }

            return result;
        }
        private bool ShouldShowCourse(ScheduleTimeSlot slot)
        {
            // 检查是否在周次范围内
            if (_currentWeek < slot.StartWeek || _currentWeek > slot.EndWeek)
                return false;

            // 检查单双周
            if (slot.WeekType == "A" && !_isCurrentWeekOdd) // 单周课程在双周不显示
                return false;
            if (slot.WeekType == "B" && _isCurrentWeekOdd) // 双周课程在单周不显示
                return false;

            return true;
        }

        private Border CreateCourseCard(Course course, ScheduleTimeSlot slot, Color backgroundColor)
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

            // 添加工具提示，包含周次信息
            var weekTypeText = slot.WeekType == null ? "每周" :
                              slot.WeekType == "A" ? "单周" : "双周";

            border.ToolTip = new ToolTip
            {
                Content = $"课程：{course.CourseName}\n" +
                         $"课程代码：{course.CourseCode}\n" +
                         $"教室：{course.Classroom}\n" +
                         $"节次：第{slot.StartPeriod}-{slot.EndPeriod}节\n" +
                         $"周次：第{slot.StartWeek}-{slot.EndWeek}周 ({weekTypeText})"
            };

            return border;
        }

        private void WeekSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                _currentWeek = comboBox.SelectedIndex + 1;
                _isCurrentWeekOdd = _currentWeek % 2 == 1;
                LoadSchedule();
                UpdateTitle();
            }
        }

        private void UpdateTitle()
        {
            Title = $"课表预览 - {GlobalUserState.Username} (第{_currentWeek}周)";
            if (CurrentWeekText != null)
            {
                CurrentWeekText.Text = $"当前周次：第{_currentWeek}周 ({(_isCurrentWeekOdd ? "单" : "双")}周)";
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}

