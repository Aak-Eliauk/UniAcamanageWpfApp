using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UniAcamanageWpfApp.Utils.Schedule;

namespace UniAcamanageWpfApp.Windows
{
    public partial class CourseEditWindow : Window
    {
        private readonly string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
        private readonly int? courseId;
        private readonly string teacherId;
        private readonly bool isAdmin;
        private List<TimeSlot> selectedTimeSlots;

        public event EventHandler CourseUpdated;


        public CourseEditWindow(int? courseId, string teacherId)
        {
            InitializeComponent();
            this.courseId = courseId;
            this.teacherId = teacherId;
            this.isAdmin = GlobalUserState.Role == "Admin";
            this.selectedTimeSlots = new List<TimeSlot>();

            Title = courseId.HasValue ? "编辑课程" : "添加课程";
            InitializeControls();
            LoadData();
        }

        private void InitializeControls()
        {
            // 初始化课程类型下拉框
            cmbCourseType.Items.Add("基础必修");
            cmbCourseType.Items.Add("专业必修");
            cmbCourseType.Items.Add("选修");

            // 初始化课程时间选择器
            InitializeTimeSelector();

            // 设置输入验证
            txtCourseCode.TextChanged += ValidateInput;
            txtCourseName.TextChanged += ValidateInput;
            cmbCourseType.SelectionChanged += ValidateInput;
            txtCredit.TextChanged += ValidateNumericInput;
            txtCapacity.TextChanged += ValidateNumericInput;
        }

        private void InitializeTimeSelector()
        {
            var weekDayPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var timePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var weekPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };

            // 周几选择
            var weekDayCombo = CreateComboBox("星期", Enumerable.Range(1, 7)
                .Select(i => new { Value = i, Text = $"周{new[] { "一", "二", "三", "四", "五", "六", "日" }[i - 1]}" }));

            // 节次选择
            var startSectionCombo = CreateComboBox("开始节次", Enumerable.Range(1, 11)
                .Select(i => new { Value = i, Text = $"第{i}节" }));
            var endSectionCombo = CreateComboBox("结束节次", Enumerable.Range(1, 11)
                .Select(i => new { Value = i, Text = $"第{i}节" }));

            // 周数选择
            var startWeekCombo = CreateComboBox("开始周", Enumerable.Range(1, 25)
                .Select(i => new { Value = i, Text = $"第{i}周" }));
            var endWeekCombo = CreateComboBox("结束周", Enumerable.Range(1, 25)
                .Select(i => new { Value = i, Text = $"第{i}周" }));

            // 单双周选择
            var weekTypeCombo = new ComboBox
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

            // 添加按钮
            var addButton = new Button
            {
                Content = "添加时间段",
                Style = FindResource("ActionButtonStyle") as Style,
                Margin = new Thickness(8)
            };
            addButton.Click += (s, e) =>
            {
                if (ValidateTimeSlot(weekDayCombo, startSectionCombo, endSectionCombo,
                    startWeekCombo, endWeekCombo))
                {
                    AddTimeSlot(weekDayCombo, startSectionCombo, endSectionCombo,
                        startWeekCombo, endWeekCombo, weekTypeCombo);
                }
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
            TimeSelectionPanel.Children.Add(addButton);
        }

        private ComboBox CreateComboBox<T>(string label, IEnumerable<T> items)
        {
            return new ComboBox
            {
                Width = 100,
                Margin = new Thickness(8),
                ItemsSource = items,
                DisplayMemberPath = "Text",
                SelectedValuePath = "Value",
                Style = FindResource("CommonComboBoxStyle") as Style
            };
        }

        private StackPanel CreateComboBoxWithLabel(string label, ComboBox comboBox)
        {
            var panel = new StackPanel { Orientation = Orientation.Vertical };
            panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(8, 0, 8, 4) });
            panel.Children.Add(comboBox);
            return panel;
        }

        private bool ValidateTimeSlot(ComboBox weekDay, ComboBox startSection,
            ComboBox endSection, ComboBox startWeek, ComboBox endWeek)
        {
            if (weekDay.SelectedValue == null || startSection.SelectedValue == null ||
                endSection.SelectedValue == null || startWeek.SelectedValue == null ||
                endWeek.SelectedValue == null)
            {
                MessageBox.Show("请完整选择时间信息！");
                return false;
            }

            int start = (int)startSection.SelectedValue;
            int end = (int)endSection.SelectedValue;
            int weekStart = (int)startWeek.SelectedValue;
            int weekEnd = (int)endWeek.SelectedValue;

            if (start > end)
            {
                MessageBox.Show("开始节次不能大于结束节次！");
                return false;
            }

            if (weekStart > weekEnd)
            {
                MessageBox.Show("开始周不能大于结束周！");
                return false;
            }

            return true;
        }

        private void AddTimeSlot(ComboBox weekDay, ComboBox startSection,
            ComboBox endSection, ComboBox startWeek, ComboBox endWeek, ComboBox weekType)
        {
            var timeSlot = new TimeSlot
            {
                WeekDay = (int)weekDay.SelectedValue,
                StartSection = (int)startSection.SelectedValue,
                EndSection = (int)endSection.SelectedValue,
                StartWeek = (int)startWeek.SelectedValue,
                EndWeek = (int)endWeek.SelectedValue,
                WeekType = weekType.SelectedValue?.ToString() ?? ""
            };

            if (selectedTimeSlots.Any(t => t.Conflicts(timeSlot)))
            {
                MessageBox.Show("时间段存在冲突！");
                return;
            }

            selectedTimeSlots.Add(timeSlot);
            UpdateTimeDisplay();
        }

        private void UpdateTimeDisplay()
        {
            selectedTimesList.ItemsSource = null;
            selectedTimesList.ItemsSource = selectedTimeSlots;
            scheduleTimePreview.Text = string.Join(",", selectedTimeSlots.Select(s => s.ToDbFormat()));
        }

        private void LoadData()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // 加载教室列表
                    var cmdClassroom = new SqlCommand(
                        "SELECT ClassroomID, RoomNumber FROM Classroom ORDER BY RoomNumber", conn);
                    var classroomAdapter = new SqlDataAdapter(cmdClassroom);
                    var dtClassroom = new DataTable();
                    classroomAdapter.Fill(dtClassroom);
                    cmbClassroom.ItemsSource = dtClassroom.DefaultView;
                    cmbClassroom.DisplayMemberPath = "RoomNumber";
                    cmbClassroom.SelectedValuePath = "ClassroomID";

                    // 加载学期列表
                    var cmdSemester = new SqlCommand(
                        "SELECT SemesterID, SemesterName FROM Semester ORDER BY StartDate DESC", conn);
                    var semesterAdapter = new SqlDataAdapter(cmdSemester);
                    var dtSemester = new DataTable();
                    semesterAdapter.Fill(dtSemester);
                    cmbSemester.ItemsSource = dtSemester.DefaultView;
                    cmbSemester.DisplayMemberPath = "SemesterName";
                    cmbSemester.SelectedValuePath = "SemesterID";

                    // 如果是编辑模式，加载现有课程数据
                    if (courseId.HasValue)
                    {
                        LoadExistingCourse(conn);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载数据失败: {ex.Message}");
            }
        }

        private void BtnMapSelect_Click(object sender, RoutedEventArgs e)
        {
            var selectorWindow = new ClassroomSelectorWindow();
            if (selectorWindow.ShowDialog() == true)
            {
                string selectedClassroomNumber = selectorWindow.SelectedClassroomNumber;

                // 从 ComboBox 的 DataTable 数据源中查找对应的教室
                foreach (DataRowView row in cmbClassroom.Items)
                {
                    if (row["RoomNumber"].ToString() == selectedClassroomNumber)
                    {
                        cmbClassroom.SelectedItem = row;
                        break;
                    }
                }
            }
        }

        private void DeleteTimeSlot_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var timeSlot = button.DataContext as TimeSlot;
            if (timeSlot == null) return;

            var result = MessageBox.Show("确定要删除这个时间段吗？", "确认删除",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                selectedTimeSlots.Remove(timeSlot);
                UpdateTimeDisplay();
            }
        }



        private void LoadExistingCourse(SqlConnection conn)
        {
            var cmd = new SqlCommand("SELECT * FROM Course WHERE CourseID = @CourseID", conn);
            cmd.Parameters.AddWithValue("@CourseID", courseId.Value);

            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    txtCourseCode.Text = reader["CourseCode"].ToString();
                    txtCourseName.Text = reader["CourseName"].ToString();
                    cmbCourseType.SelectedItem = reader["CourseType"].ToString();
                    txtCredit.Text = reader["Credit"].ToString();
                    txtCapacity.Text = reader["Capacity"].ToString();
                    cmbClassroom.SelectedValue = reader["ClassroomID"];
                    txtDescription.Text = reader["Description"].ToString();
                    cmbSemester.SelectedValue = reader["SemesterID"];

                    // 解析课程时间
                    string scheduleTime = reader["ScheduleTime"].ToString();
                    if (!string.IsNullOrEmpty(scheduleTime))
                    {
                        selectedTimeSlots = scheduleTime
                            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(t => t.Trim())
                            .Select(TimeSlot.FromDbFormat)
                            .Where(t => t != null)
                            .ToList();

                        if (selectedTimeSlots.Count == 0)
                        {
                            MessageBox.Show("课程时间格式无效，请重新设置！");
                        }
                        else
                        {
                            UpdateTimeDisplay();
                        }
                    }
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm()) return;

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            var cmd = new SqlCommand(courseId.HasValue ?
                                GetUpdateCommand() : GetInsertCommand(), conn, transaction);

                            AddParameters(cmd);

                            if (courseId.HasValue)
                            {
                                cmd.ExecuteNonQuery();
                            }
                            else
                            {
                                // 插入新课程并获取新ID
                                var newCourseId = Convert.ToInt32(cmd.ExecuteScalar());

                                // 如果不是管理员，添加教师课程关联
                                if (!isAdmin && !string.IsNullOrEmpty(teacherId))
                                {
                                    cmd = new SqlCommand(
                                        "INSERT INTO TeacherCourse (TeacherID, CourseID) VALUES (@TeacherID, @CourseID)",
                                        conn, transaction);
                                    cmd.Parameters.AddWithValue("@TeacherID", teacherId);
                                    cmd.Parameters.AddWithValue("@CourseID", newCourseId);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            transaction.Commit();
                            CourseUpdated?.Invoke(this, EventArgs.Empty);
                            MessageBox.Show("保存成功！");
                            Close();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}");
            }
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(txtCourseCode.Text))
            {
                MessageBox.Show("请输入课程代码！");
                txtCourseCode.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtCourseName.Text))
            {
                MessageBox.Show("请输入课程名称！");
                txtCourseName.Focus();
                return false;
            }

            if (cmbCourseType.SelectedItem == null)
            {
                MessageBox.Show("请选择课程类型！");
                cmbCourseType.Focus();
                return false;
            }

            if (!decimal.TryParse(txtCredit.Text, out decimal credit) || credit <= 0)
            {
                MessageBox.Show("请输入有效的学分！");
                txtCredit.Focus();
                return false;
            }

            if (selectedTimeSlots.Count == 0)
            {
                MessageBox.Show("请至少添加一个课程时间！");
                return false;
            }

            // 验证所有时间段格式
            var timeSlots = selectedTimeSlots.Select(s => s.ToDbFormat());
            foreach (var slot in timeSlots)
            {
                if (!TimeSlot.ValidateFormat(slot))
                {
                    MessageBox.Show($"课程时间格式无效：{slot}\n" +
                                  "格式应为：周几-开始节次-结束节次-开始周-结束周[-单双周]\n" +
                                  "周几范围：1-7，节次范围：1-11，单双周可选：A或B");
                    return false;
                }
            }

            // 验证时间段之间是否有冲突
            for (int i = 0; i < selectedTimeSlots.Count; i++)
            {
                for (int j = i + 1; j < selectedTimeSlots.Count; j++)
                {
                    if (selectedTimeSlots[i].Conflicts(selectedTimeSlots[j]))
                    {
                        MessageBox.Show("课程时间存在冲突，请检查！");
                        return false;
                    }
                }
            }

            if (!int.TryParse(txtCapacity.Text, out int capacity) || capacity <= 0)
            {
                MessageBox.Show("请输入有效的课程容量！");
                txtCapacity.Focus();
                return false;
            }

            if (cmbSemester.SelectedValue == null)
            {
                MessageBox.Show("请选择学期！");
                cmbSemester.Focus();
                return false;
            }

            return true;
        }

        // 接上文 GetUpdateCommand() 方法
        private string GetUpdateCommand()
        {
            return @"UPDATE Course 
                    SET CourseCode = @CourseCode,
                        CourseName = @CourseName,
                        CourseType = @CourseType,
                        Credit = @Credit,
                        ScheduleTime = @ScheduleTime,
                        Capacity = @Capacity,
                        ClassroomID = @ClassroomID,
                        Description = @Description,
                        SemesterID = @SemesterID
                    WHERE CourseID = @CourseID";
        }

        private string GetInsertCommand()
        {
            return @"INSERT INTO Course 
                    (CourseCode, CourseName, CourseType, Credit, 
                     ScheduleTime, Capacity, ClassroomID, Description, SemesterID)
                    VALUES 
                    (@CourseCode, @CourseName, @CourseType, @Credit,
                     @ScheduleTime, @Capacity, @ClassroomID, @Description, @SemesterID);
                    SELECT SCOPE_IDENTITY();";
        }

        private void AddParameters(SqlCommand cmd)
        {
            cmd.Parameters.AddWithValue("@CourseCode", txtCourseCode.Text);
            cmd.Parameters.AddWithValue("@CourseName", txtCourseName.Text);
            cmd.Parameters.AddWithValue("@CourseType", cmbCourseType.SelectedItem.ToString());
            cmd.Parameters.AddWithValue("@Credit", decimal.Parse(txtCredit.Text));
            // 格式化课程时间
            string scheduleTime = string.Join(",", selectedTimeSlots
                .Select(s => s.ToDbFormat())
                .Where(s => TimeSlot.ValidateFormat(s)));

            if (string.IsNullOrEmpty(scheduleTime))
            {
                throw new Exception("无有效的课程时间！");
            }
            cmd.Parameters.AddWithValue("@ScheduleTime", scheduleTime);
            cmd.Parameters.AddWithValue("@Capacity", int.Parse(txtCapacity.Text));
            cmd.Parameters.AddWithValue("@ClassroomID", cmbClassroom.SelectedValue ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Description", txtDescription.Text ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@SemesterID", cmbSemester.SelectedValue);

            if (courseId.HasValue)
            {
                cmd.Parameters.AddWithValue("@CourseID", courseId.Value);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ValidateInput(object sender, EventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null && string.IsNullOrWhiteSpace(textBox.Text))
            {
                textBox.Background = new SolidColorBrush(Colors.MistyRose);
            }
            else
            {
                if (textBox != null)
                    textBox.Background = new SolidColorBrush(Colors.White);
            }
        }

        private void ValidateNumericInput(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            if (textBox == txtCredit)
            {
                if (!decimal.TryParse(textBox.Text, out decimal value) || value <= 0)
                {
                    textBox.Background = new SolidColorBrush(Colors.MistyRose);
                }
                else
                {
                    textBox.Background = new SolidColorBrush(Colors.White);
                }
            }
            else if (textBox == txtCapacity)
            {
                if (!int.TryParse(textBox.Text, out int value) || value <= 0)
                {
                    textBox.Background = new SolidColorBrush(Colors.MistyRose);
                }
                else
                {
                    textBox.Background = new SolidColorBrush(Colors.White);
                }
            }
        }

    private void EditTimeSlot_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var timeSlot = button.DataContext as TimeSlot;
            if (timeSlot == null) return;

            var editWindow = new TimeSlotEditWindow(timeSlot);
            if (editWindow.ShowDialog() == true)
            {
                var editedSlot = editWindow.EditedTimeSlot;
                // 检查是否与其他时间段冲突
                var otherSlots = selectedTimeSlots.Where(s => s != timeSlot).ToList();
                if (otherSlots.Any(s => s.Conflicts(editedSlot)))
                {
                    MessageBox.Show("修改后的时间段与现有时间段冲突！");
                    return;
                }

                // 更新时间段
                var index = selectedTimeSlots.IndexOf(timeSlot);
                selectedTimeSlots[index] = editedSlot;
                UpdateTimeDisplay();
            }
        }
    }

    // 课程时间段类
    public class TimeSlot
    {
        public int WeekDay { get; set; }
        public int StartSection { get; set; }
        public int EndSection { get; set; }
        public int StartWeek { get; set; }
        public int EndWeek { get; set; }
        public string WeekType { get; set; }

        public string ToDbFormat()
        {
            return $"{WeekDay}-{StartSection}-{EndSection}-{StartWeek}-{EndWeek}" +
                   (!string.IsNullOrEmpty(WeekType) ? $"-{WeekType}" : "");
        }

        public string DisplayText => ToString();

        public static bool ValidateFormat(string format)
        {
            var parts = format.Trim().Split('-');
            if (parts.Length < 5 || parts.Length > 6)
                return false;

            // 验证数值范围
            if (!int.TryParse(parts[0], out int weekDay) || weekDay < 1 || weekDay > 7)
                return false;

            if (!int.TryParse(parts[1], out int startSection) || startSection < 1 || startSection > 11)
                return false;

            if (!int.TryParse(parts[2], out int endSection) || endSection < 1 || endSection > 11 || endSection < startSection)
                return false;

            if (!int.TryParse(parts[3], out int startWeek) || startWeek < 1 || startWeek > 25)
                return false;

            if (!int.TryParse(parts[4], out int endWeek) || endWeek < 1 || endWeek > 25 || endWeek < startWeek)
                return false;

            // 如果有第六部分，验证是否为 A 或 B
            if (parts.Length == 6 && parts[5] != "A" && parts[5] != "B")
                return false;

            return true;
        }

        public static TimeSlot FromDbFormat(string format)
        {
            if (!ValidateFormat(format))
                return null;

            var parts = format.Trim().Split('-');
            return new TimeSlot
            {
                WeekDay = int.Parse(parts[0]),
                StartSection = int.Parse(parts[1]),
                EndSection = int.Parse(parts[2]),
                StartWeek = int.Parse(parts[3]),
                EndWeek = int.Parse(parts[4]),
                WeekType = parts.Length > 5 ? parts[5] : ""
            };
        }

        public override string ToString()
        {
            string[] weekDays = { "一", "二", "三", "四", "五", "六", "日" };
            string weekDayStr = $"周{weekDays[WeekDay - 1]}";
            string sectionStr = $"第{StartSection}-{EndSection}节";
            string weekStr = $"{StartWeek}-{EndWeek}周";
            string weekTypeStr = WeekType == "A" ? "单周" : WeekType == "B" ? "双周" : "";

            return $"{weekDayStr} {sectionStr} ({weekStr}{weekTypeStr})";
        }

        public bool Conflicts(TimeSlot other)
        {
            // 如果不是同一天，则不冲突
            if (WeekDay != other.WeekDay) return false;

            // 检查周次是否重叠
            bool weeksOverlap = !(EndWeek < other.StartWeek || StartWeek > other.EndWeek);
            if (!weeksOverlap) return false;

            // 检查单双周是否冲突
            if (WeekType != "" && other.WeekType != "" && WeekType != other.WeekType)
                return false;

            // 检查节次是否重叠
            return !(EndSection < other.StartSection || StartSection > other.EndSection);
        }
    }


}