using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;

public partial class CourseSchedulePreviewWindow : Window
{
    public CourseSchedulePreviewWindow(List<CourseScheduleItem> selectedCourses)
    {
        InitializeComponent();
        LoadSchedule(selectedCourses);
    }

    private void LoadSchedule(List<CourseScheduleItem> courses)
    {
        // 清空现有内容
        ScheduleItemsControl.Items.Clear();

        // 添加节次标签
        for (int i = 1; i <= 11; i++)
        {
            var timeLabel = new TextBlock
            {
                Text = $"第{i}节",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(timeLabel, i - 1);
            Grid.SetColumn(timeLabel, 0);
            ScheduleItemsControl.Items.Add(timeLabel);
        }

        // 添加课程项
        foreach (var course in courses)
        {
            var courseItem = CreateCourseItem(course);
            ScheduleItemsControl.Items.Add(courseItem);
        }
    }

    private FrameworkElement CreateCourseItem(CourseScheduleItem course)
    {
        var border = new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E3F2FD")),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(2),
            Padding = new Thickness(5)
        };

        var stackPanel = new StackPanel();
        stackPanel.Children.Add(new TextBlock
        {
            Text = course.CourseName,
            TextWrapping = TextWrapping.Wrap,
            FontWeight = FontWeights.Bold
        });
        stackPanel.Children.Add(new TextBlock
        {
            Text = course.Classroom,
            FontSize = 12,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666"))
        });

        border.Child = stackPanel;

        // 设置课程在网格中的位置
        Grid.SetRow(border, course.StartPeriod - 1);
        Grid.SetColumn(border, course.DayOfWeek);
        Grid.SetRowSpan(border, course.Duration);

        return border;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }
}

public class CourseScheduleItem
{
    public string CourseCode { get; set; }
    public string CourseName { get; set; }
    public string Classroom { get; set; }
    public int DayOfWeek { get; set; }  // 1-7 代表周一到周日
    public int StartPeriod { get; set; } // 起始节次
    public int Duration { get; set; }    // 持续节数
}