using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UniAcamanageWpfApp.Models;

namespace UniAcamanageWpfApp.Windows
{
    public partial class TimeSlotEditWindow : Window
    {
        public TimeSlot EditedTimeSlot { get; private set; }
        private ComboBox weekDayCombo, startSectionCombo, endSectionCombo,
                        startWeekCombo, endWeekCombo, weekTypeCombo;

        public TimeSlotEditWindow(TimeSlot timeSlot)
        {
            InitializeComponent();
            InitializeControls();
            LoadTimeSlot(timeSlot);
        }

        private void InitializeControls()
        {
            var weekDayPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var timePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var weekPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };

            // 周几选择
            weekDayCombo = new ComboBox
            {
                Width = 100,
                Margin = new Thickness(8),
                ItemsSource = Enumerable.Range(1, 7)
                    .Select(i => new { Value = i, Text = $"周{new[] { "一", "二", "三", "四", "五", "六", "日" }[i - 1]}" }),
                DisplayMemberPath = "Text",
                SelectedValuePath = "Value"
            };

            // 节次选择
            startSectionCombo = new ComboBox
            {
                Width = 100,
                Margin = new Thickness(8),
                ItemsSource = Enumerable.Range(1, 11)
                    .Select(i => new { Value = i, Text = $"第{i}节" }),
                DisplayMemberPath = "Text",
                SelectedValuePath = "Value"
            };

            endSectionCombo = new ComboBox
            {
                Width = 100,
                Margin = new Thickness(8),
                ItemsSource = Enumerable.Range(1, 11)
                    .Select(i => new { Value = i, Text = $"第{i}节" }),
                DisplayMemberPath = "Text",
                SelectedValuePath = "Value"
            };

            // 周数选择
            startWeekCombo = new ComboBox
            {
                Width = 100,
                Margin = new Thickness(8),
                ItemsSource = Enumerable.Range(1, 25)
                    .Select(i => new { Value = i, Text = $"第{i}周" }),
                DisplayMemberPath = "Text",
                SelectedValuePath = "Value"
            };

            endWeekCombo = new ComboBox
            {
                Width = 100,
                Margin = new Thickness(8),
                ItemsSource = Enumerable.Range(1, 25)
                    .Select(i => new { Value = i, Text = $"第{i}周" }),
                DisplayMemberPath = "Text",
                SelectedValuePath = "Value"
            };

            // 单双周选择
            weekTypeCombo = new ComboBox
            {
                Width = 100,
                Margin = new Thickness(8),
                ItemsSource = new[] {
                    new { Value = "", Text = "全部周" },
                    new { Value = "A", Text = "单周" },
                    new { Value = "B", Text = "双周" }
                },
                DisplayMemberPath = "Text",
                SelectedValuePath = "Value"
            };

            // 组织布局
            weekDayPanel.Children.Add(CreateComboBoxWithLabel("星期", weekDayCombo));
            timePanel.Children.Add(CreateComboBoxWithLabel("开始节次", startSectionCombo));
            timePanel.Children.Add(CreateComboBoxWithLabel("结束节次", endSectionCombo));
            weekPanel.Children.Add(CreateComboBoxWithLabel("开始周", startWeekCombo));
            weekPanel.Children.Add(CreateComboBoxWithLabel("结束周", endWeekCombo));
            weekPanel.Children.Add(CreateComboBoxWithLabel("周类型", weekTypeCombo));

            TimeSelectionPanel.Children.Add(weekDayPanel);
            TimeSelectionPanel.Children.Add(timePanel);
            TimeSelectionPanel.Children.Add(weekPanel);
        }

        private StackPanel CreateComboBoxWithLabel(string label, ComboBox comboBox)
        {
            var panel = new StackPanel { Orientation = Orientation.Vertical };
            panel.Children.Add(new TextBlock
            {
                Text = label,
                Margin = new Thickness(8, 0, 8, 4)
            });
            panel.Children.Add(comboBox);
            return panel;
        }

        private void LoadTimeSlot(TimeSlot timeSlot)
        {
            if (timeSlot == null) return;

            weekDayCombo.SelectedValue = timeSlot.WeekDay;
            startSectionCombo.SelectedValue = timeSlot.StartSection;
            endSectionCombo.SelectedValue = timeSlot.EndSection;
            startWeekCombo.SelectedValue = timeSlot.StartWeek;
            endWeekCombo.SelectedValue = timeSlot.EndWeek;
            weekTypeCombo.SelectedValue = timeSlot.WeekType;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateTimeSlot())
            {
                return;
            }

            EditedTimeSlot = new TimeSlot
            {
                WeekDay = (int)weekDayCombo.SelectedValue,
                StartSection = (int)startSectionCombo.SelectedValue,
                EndSection = (int)endSectionCombo.SelectedValue,
                StartWeek = (int)startWeekCombo.SelectedValue,
                EndWeek = (int)endWeekCombo.SelectedValue,
                WeekType = weekTypeCombo.SelectedValue?.ToString() ?? ""
            };

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private bool ValidateTimeSlot()
        {
            if (weekDayCombo.SelectedValue == null ||
                startSectionCombo.SelectedValue == null ||
                endSectionCombo.SelectedValue == null ||
                startWeekCombo.SelectedValue == null ||
                endWeekCombo.SelectedValue == null)
            {
                MessageBox.Show("请完整选择时间信息！", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            int start = (int)startSectionCombo.SelectedValue;
            int end = (int)endSectionCombo.SelectedValue;
            if (start > end)
            {
                MessageBox.Show("开始节次不能大于结束节次！", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            int weekStart = (int)startWeekCombo.SelectedValue;
            int weekEnd = (int)endWeekCombo.SelectedValue;
            if (weekStart > weekEnd)
            {
                MessageBox.Show("开始周不能大于结束周！", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }
    }
}