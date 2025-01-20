using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.Win32;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics; // 用于 Debug
using System.IO;         // 用于 File 操作
using UniAcamanageWpfApp.Models;      // 用于 Course, Semester 等模型
using UniAcamanageWpfApp.Services;    // 用于 ICourseService
using UniAcamanageWpfApp.ViewModels;
using System.Collections.ObjectModel;  // 用于 CourseSelectionViewModel


namespace UniAcamanageWpfApp.Views
{
    public partial class CourseSelectionView : UserControl
    {
        private readonly CourseSelectionViewModel _viewModel;
        private readonly ICourseService _courseService;
        private string studentID;

        public CourseSelectionView()
        {
            InitializeComponent();
            studentID = GlobalUserState.LinkedID; // 从全局状态获取学号
            _courseService = new CourseService();
            _viewModel = new CourseSelectionViewModel();
            DataContext = _viewModel;

            // 初始化选课结果集合
            _viewModel.SelectedCourses = new ObservableCollection<Course>();

            // 注册事件处理程序
            this.Loaded += CourseSelectionView_Loaded;

            // 学期选择事件
            RecommendSemesterComboBox.SelectionChanged += RecommendSemester_SelectionChanged;
            SelfSelectSemesterComboBox.SelectionChanged += SelfSelectSemester_SelectionChanged;
            SelectionSemesterComboBox.SelectionChanged += SelectionSemester_SelectionChanged;

            // 搜索和筛选事件
            CourseSearchBox.TextChanged += CourseSearchBox_TextChanged;
            CourseTypeFilter.SelectionChanged += Filter_SelectionChanged;
            TimeSlotFilter.SelectionChanged += Filter_SelectionChanged;
            CourseStatusFilter.SelectionChanged += Filter_SelectionChanged;
        }

        private async void FilterCourses_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await LoadFilteredCoursesAsync();
        }
        private async void SearchCourses_Click(object sender, RoutedEventArgs e)
        {
            await LoadAvailableCoursesAsync();
        }
        private async Task LoadFilteredCoursesAsync()
        {
            try
            {
                var selectedSemester = SelfSelectSemesterComboBox.SelectedItem as Semester;
                if (selectedSemester == null) return;

                var courseType = (CourseTypeFilter.SelectedItem as ComboBoxItem)?.Content?.ToString();
                var timeSlot = (TimeSlotFilter.SelectedItem as ComboBoxItem)?.Content?.ToString();

                LoadingIndicator.Visibility = Visibility.Visible;
                var filteredCourses = await _courseService.GetAvailableCoursesAsync(selectedSemester.SemesterId, courseType, timeSlot);

                // 更新 ViewModel 的集合
                _viewModel.AvailableCourses = new ObservableCollection<Course>(filteredCourses);

                // 更新统计信息
                _viewModel.UpdateStatistics();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载筛选课程失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
            }
        }

        private async void PreviewSchedule_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedSemester = SelectionSemesterComboBox.SelectedItem as Semester;
                if (selectedSemester == null)
                {
                    MessageBox.Show("请选择学期！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectedCourses = await GetAllSelectedCoursesForSchedule();
                if (selectedCourses.Count == 0)
                {
                    MessageBox.Show("暂无已选课程，无法预览课表！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 获取当前是第几周
                var semesterStartDate = selectedSemester.StartDate;
                var currentWeek = CalculateCurrentWeek(semesterStartDate);

                var previewWindow = new CourseSchedulePreviewWindow(selectedCourses, currentWeek);
                previewWindow.Owner = Window.GetWindow(this);
                previewWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"预览课表失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 计算当前教学周
        private int CalculateCurrentWeek(DateTime semesterStartDate)
        {
            var today = DateTime.Today;
            if (today < semesterStartDate)
                return 1;

            int weekNumber = (int)Math.Ceiling((today - semesterStartDate).TotalDays / 7.0);
            return Math.Min(weekNumber, 25); // 最大25周
        }

        private async Task<List<Course>> GetAllSelectedCoursesForSchedule()
        {
            var selectedSemester = SelectionSemesterComboBox.SelectedItem as Semester;
            if (selectedSemester == null)
                return new List<Course>();

            try
            {
                return await _courseService.GetSelectedCoursesAsync(
                    GlobalUserState.LinkedID,
                    selectedSemester.SemesterId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"获取已选课程失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<Course>();
            }
        }

        private List<string> GetSelectedCourseCodes()
        {
            var selectedCourses = new List<string>();

            // 从基础必修课获取选中的课程代码
            var basicCourses = BasicRequiredGrid.ItemsSource as IEnumerable<Course>;
            if (basicCourses != null)
            {
                selectedCourses.AddRange(basicCourses.Where(c => c.IsSelected).Select(c => c.CourseCode));
            }

            // 从专业必修课获取选中的课程代码
            var majorCourses = MajorRequiredGrid.ItemsSource as IEnumerable<Course>;
            if (majorCourses != null)
            {
                selectedCourses.AddRange(majorCourses.Where(c => c.IsSelected).Select(c => c.CourseCode));
            }

            // 从选修课获取选中的课程代码
            var electiveCourses = ElectiveGrid.ItemsSource as IEnumerable<Course>;
            if (electiveCourses != null)
            {
                selectedCourses.AddRange(electiveCourses.Where(c => c.IsSelected).Select(c => c.CourseCode));
            }

            return selectedCourses;
        }

        // 在 CourseSelectionView.xaml.cs 中添加更新所有课程状态的方法
        private void UpdateAllCoursesStatus()
        {
            var selectedCourseIds = new HashSet<int>(_viewModel.SelectedCourses.Select(c => c.CourseID));

            // 更新推荐课程状态
            foreach (var course in _viewModel.RecommendedBasicCourses)
            {
                course.IsSelected = selectedCourseIds.Contains(course.CourseID);
            }
            foreach (var course in _viewModel.RecommendedMajorCourses)
            {
                course.IsSelected = selectedCourseIds.Contains(course.CourseID);
            }
            foreach (var course in _viewModel.RecommendedElectiveCourses)
            {
                course.IsSelected = selectedCourseIds.Contains(course.CourseID);
            }

            // 更新 DataGrid
            BasicRequiredGrid.Items.Refresh();
            MajorRequiredGrid.Items.Refresh();
            ElectiveGrid.Items.Refresh();
        }

        private async void CourseSelectionView_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 初始化各个ComboBox的选项
                InitializeComboBoxes();

                // 加载当前学期数据
                var semesters = await _courseService.GetCurrentSemestersAsync();
                if (semesters != null && semesters.Count > 0)
                {
                    RecommendSemesterComboBox.ItemsSource = semesters;
                    SelfSelectSemesterComboBox.ItemsSource = semesters;
                    SelectionSemesterComboBox.ItemsSource = semesters;

                    // 默认选择第一个学期
                    RecommendSemesterComboBox.SelectedIndex = 0;
                    SelfSelectSemesterComboBox.SelectedIndex = 0;
                    SelectionSemesterComboBox.SelectedIndex = 0;
                }

                // 加载推荐课程数据
                await LoadRecommendedCoursesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitializeComboBoxes()
        {
            // 初始化课程类型筛选
            CourseTypeFilter.Items.Clear();
            CourseTypeFilter.Items.Add("全部类型");
            CourseTypeFilter.Items.Add("基础必修");
            CourseTypeFilter.Items.Add("专业必修");
            CourseTypeFilter.Items.Add("选修课程");
            CourseTypeFilter.SelectedIndex = 0;

            // 初始化上课时间筛选
            TimeSlotFilter.Items.Clear();
            TimeSlotFilter.Items.Add("全部时间");
            TimeSlotFilter.Items.Add("上午课程");
            TimeSlotFilter.Items.Add("下午课程");
            TimeSlotFilter.Items.Add("晚上课程");
            TimeSlotFilter.SelectedIndex = 0;

            // 初始化课程状态筛选
            CourseStatusFilter.Items.Clear();
            CourseStatusFilter.Items.Add("全部课程");
            CourseStatusFilter.Items.Add("有余量");
            CourseStatusFilter.Items.Add("已满额");
            CourseStatusFilter.SelectedIndex = 0;
        }

        #region 课程加载方法

        private async Task LoadRecommendedCoursesAsync()
        {
            try
            {
                var selectedSemester = RecommendSemesterComboBox.SelectedItem as Semester;
                if (selectedSemester == null) return;

                // 显示加载提示
                LoadingIndicator.Visibility = Visibility.Visible;

                var courseResults = await _courseService.GetRecommendedCoursesAsync(selectedSemester.SemesterId);

                // 更新 ViewModel 中的集合
                _viewModel.RecommendedBasicCourses = new ObservableCollection<Course>(courseResults.Basic);
                _viewModel.RecommendedMajorCourses = new ObservableCollection<Course>(courseResults.Major);
                _viewModel.RecommendedElectiveCourses = new ObservableCollection<Course>(courseResults.Elective);


                // 更新DataGrid数据源
                BasicRequiredGrid.ItemsSource = courseResults.Basic;
                MajorRequiredGrid.ItemsSource = courseResults.Major;
                ElectiveGrid.ItemsSource = courseResults.Elective;

                // 更新选课状态
                _viewModel.UpdateRecommendedCoursesStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载推荐课程失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
            }
        }

        private async Task LoadAvailableCoursesAsync()
        {
            try
            {
                var selectedSemester = SelfSelectSemesterComboBox.SelectedItem as Semester;
                if (selectedSemester == null) return;

                LoadingIndicator.Visibility = Visibility.Visible;

                // 获取筛选条件
                string courseType = CourseTypeFilter.SelectedItem?.ToString();
                if (courseType == "全部类型") courseType = null;
                else if (courseType == "选修课程") courseType = "选修"; // 确保与数据库中的值匹配

                // 获取时间筛选条件
                string timeSlot = null;
                if (TimeSlotFilter.SelectedIndex > 0)
                {
                    var selectedTimeSlot = TimeSlotFilter.SelectedItem?.ToString();
                    switch (selectedTimeSlot)
                    {
                        case "上午课程":
                            timeSlot = "1-4"; // 第1-4节
                            break;
                        case "下午课程":
                            timeSlot = "5-8"; // 第5-8节
                            break;
                        case "晚上课程":
                            timeSlot = "9-11"; // 第9-11节
                            break;
                    }
                }

                var courses = await _courseService.GetAvailableCoursesAsync(
                    selectedSemester.SemesterId,
                    courseType,
                    timeSlot
                );

                // 应用课程状态筛选
                if (CourseStatusFilter.SelectedIndex > 0)
                {
                    courses = FilterCoursesByStatus(courses).ToList();
                }

                // 应用搜索条件
                if (!string.IsNullOrWhiteSpace(CourseSearchBox.Text))
                {
                    string searchText = CourseSearchBox.Text.ToLower();
                    courses = courses.Where(c =>
                        c.CourseCode.ToLower().Contains(searchText) ||
                        c.CourseName.ToLower().Contains(searchText) ||
                        c.TeacherName?.ToLower().Contains(searchText) == true
                    ).ToList();
                }

                // 更新 ViewModel 中的可选课程集合
                _viewModel.AvailableCourses = new ObservableCollection<Course>(courses);
                AvailableCoursesGrid.ItemsSource = _viewModel.AvailableCourses;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载可选课程失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region 事件处理方法

        private async void RecommendSemester_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await LoadCourseSelectionResultsAsync();
        }

        private async void SelfSelectSemester_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await LoadAvailableCoursesAsync();
        }

        private async void SelectionSemester_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SelectionSemesterComboBox.SelectedItem is Semester selectedSemester)
            {
                await LoadSelectedCoursesAsync();
                _viewModel.QueryTime = DateTime.Now; // 更新查询时间
            }
        }
        private void UpdateTotalCredits()
        {
            if (_viewModel.SelectedCourses != null)
            {
                _viewModel.TotalCredits = _viewModel.SelectedCourses.Sum(c => c.Credit);
            }
        }

        private async void CourseSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 使用防抖，避免频繁查询
            if (_searchDebounceTimer != null)
            {
                _searchDebounceTimer.Stop();
            }

            _searchDebounceTimer = new System.Windows.Threading.DispatcherTimer();
            _searchDebounceTimer.Interval = TimeSpan.FromMilliseconds(300);
            _searchDebounceTimer.Tick += async (s, args) =>
            {
                _searchDebounceTimer.Stop();
                await LoadAvailableCoursesAsync();
            };
            _searchDebounceTimer.Start();
        }

        private async void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await LoadAvailableCoursesAsync();
        }

        #endregion

        #region 辅助方法

        private System.Windows.Threading.DispatcherTimer _searchDebounceTimer;

        private IEnumerable<Course> FilterCoursesByTimeSlot(IEnumerable<Course> courses)
        {
            string timeSlot = TimeSlotFilter.SelectedItem?.ToString();
            return courses.Where(c =>
            {
                if (string.IsNullOrEmpty(c.ScheduleTime)) return false;

                int period = GetStartPeriod(c.ScheduleTime);
                return timeSlot switch
                {
                    "上午课程" => period >= 1 && period <= 4,
                    "下午课程" => period >= 5 && period <= 8,
                    "晚上课程" => period >= 9,
                    _ => true
                };
            });
        }

        private IEnumerable<Course> FilterCoursesByStatus(IEnumerable<Course> courses)
        {
            string status = CourseStatusFilter.SelectedItem?.ToString();
            return courses.Where(c =>
            {
                if (string.IsNullOrEmpty(c.Capacity)) return true;

                var parts = c.Capacity.Split('/');
                if (parts.Length != 2) return true;

                if (int.TryParse(parts[0], out int current) &&
                    int.TryParse(parts[1], out int max))
                {
                    return status switch
                    {
                        "有余量" => current < max,
                        "已满额" => current >= max,
                        _ => true
                    };
                }
                return true;
            });
        }

        private int GetStartPeriod(string scheduleTime)
        {
            var match = Regex.Match(scheduleTime, @"第(\d+)-\d+节");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int period))
            {
                return period;
            }
            return 0;
        }

        private void UpdateCourseStatistics()
        {
            var basicCourses = BasicRequiredGrid.ItemsSource as IEnumerable<Course>;
            var majorCourses = MajorRequiredGrid.ItemsSource as IEnumerable<Course>;
            var electiveCourses = ElectiveGrid.ItemsSource as IEnumerable<Course>;

            decimal totalCredits = 0;
            int totalCourses = 0;

            if (basicCourses != null)
            {
                var selected = basicCourses.Where(c => c.IsSelected);
                totalCredits += selected.Sum(c => c.Credit);
                totalCourses += selected.Count();
            }

            if (majorCourses != null)
            {
                var selected = majorCourses.Where(c => c.IsSelected);
                totalCredits += selected.Sum(c => c.Credit);
                totalCourses += selected.Count();
            }

            if (electiveCourses != null)
            {
                var selected = electiveCourses.Where(c => c.IsSelected);
                totalCredits += selected.Sum(c => c.Credit);
                totalCourses += selected.Count();
            }

            TotalCoursesText.Text = totalCourses.ToString();
            TotalCreditsText.Text = totalCredits.ToString("F1");
        }

        private void UpdateAvailableCourseCount()
        {
            var courses = AvailableCoursesGrid.ItemsSource as IEnumerable<Course>;
            AvailableCourseCountText.Text = courses?.Count().ToString() ?? "0";
        }

        #endregion

        #region 选课操作方法

        private async void AddCourse_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var course = button?.DataContext as Course;

            if (course == null) return;

            try
            {
                if (!CheckCourseCapacity(course))
                {
                    MessageBox.Show("该课程已达到选课人数上限！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (HasTimeConflict(course))
                {
                    MessageBox.Show("该课程与已选课程时间冲突！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                bool success = await _courseService.AddCourseSelectionAsync(studentID, course.CourseID);
                if (success)
                {
                    await LoadSelectedCoursesAsync();
                    UpdateAllCoursesStatus(); // 更新所有课程状态
                    UpdateTotalCredits();
                    MessageBox.Show("添加课程成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"添加课程失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RemoveCourse_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var course = button?.DataContext as Course;

            if (course == null) return;

            try
            {
                var result = MessageBox.Show("确定要退选该课程吗？", "确认",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    bool success = await _courseService.RemoveCourseSelectionAsync(studentID, course.CourseID);
                    if (success)
                    {
                        await LoadSelectedCoursesAsync();
                        UpdateAllCoursesStatus(); // 更新所有课程状态
                        UpdateTotalCredits();
                        MessageBox.Show("退选课程成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"退选课程失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private async Task LoadSelectedCoursesAsync()
        {
            try
            {
                var selectedSemester = SelectionSemesterComboBox.SelectedItem as Semester;
                if (selectedSemester == null) return;

                LoadingIndicator.Visibility = Visibility.Visible;
                var selectedCourses = await _courseService.GetSelectedCoursesAsync(studentID, selectedSemester.SemesterId);

                // 更新 ViewModel 的集合
                _viewModel.SelectedCourses = new ObservableCollection<Course>(selectedCourses);

                // 设置 DataGrid 的数据源
                CourseSelectionResultGrid.ItemsSource = _viewModel.SelectedCourses;

                // 更新统计信息
                _viewModel.UpdateStatistics();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载已选课程失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
            }
        }

        private async Task LoadCourseSelectionResultsAsync()
        {
            try
            {
                var selectedSemester = SelectionSemesterComboBox.SelectedItem as Semester;
                if (selectedSemester == null) return;

                LoadingIndicator.Visibility = Visibility.Visible;
                var selectedCourses = await _courseService.GetSelectedCoursesAsync(studentID, selectedSemester.SemesterId);

                // 更新 ViewModel 的集合
                _viewModel.CourseSelectionResults = new ObservableCollection<Course>(selectedCourses);

                // 设置 DataGrid 的数据源
                CourseSelectionResultGrid.ItemsSource = _viewModel.CourseSelectionResults;

                // 更新统计信息
                _viewModel.UpdateStatistics();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载选课结果失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
            }
        }

        private async void SubmitSelection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show("确定要提交选课结果吗？提交后将不能再修改！", "确认",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var selectedSemester = SelectionSemesterComboBox.SelectedItem as Semester;
                    if (selectedSemester == null) return;

                    await _courseService.SubmitCourseSelectionsAsync(selectedSemester.SemesterId,
                        GetSelectedCourseCodes());

                    MessageBox.Show("选课提交成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadSelectedCoursesAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"提交选课失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region 辅助检查方法

        private bool CheckCourseCapacity(Course course)
        {
            if (string.IsNullOrEmpty(course.Capacity)) return true;

            var parts = course.Capacity.Split('/');
            if (parts.Length != 2) return true;

            if (int.TryParse(parts[0], out int current) &&
                int.TryParse(parts[1], out int max))
            {
                return current < max;
            }
            return true;
        }

        private bool HasTimeConflict(Course newCourse)
        {
            var selectedCourses = SelectedCoursesGrid.ItemsSource as IEnumerable<Course>;
            if (selectedCourses == null) return false;

            foreach (var existingCourse in selectedCourses)
            {
                if (DoCoursesConflict(newCourse, existingCourse))
                {
                    return true;
                }
            }
            return false;
        }

        private bool DoCoursesConflict(Course course1, Course course2)
        {
            var schedule1 = ParseScheduleTime(course1.ScheduleTime);
            var schedule2 = ParseScheduleTime(course2.ScheduleTime);

            foreach (var time1 in schedule1)
            {
                foreach (var time2 in schedule2)
                {
                    if (time1.DayOfWeek == time2.DayOfWeek)
                    {
                        // 检查时间段是否重叠
                        if (time1.StartPeriod <= time2.EndPeriod &&
                            time2.StartPeriod <= time1.EndPeriod)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private List<(int DayOfWeek, int StartPeriod, int EndPeriod)> ParseScheduleTime(string scheduleTime)
        {
            var result = new List<(int DayOfWeek, int StartPeriod, int EndPeriod)>();
            var timeSlots = scheduleTime.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var slot in timeSlots)
            {
                var match = Regex.Match(slot.Trim(), @"周([一二三四五六日])第(\d+)-(\d+)节");
                if (match.Success)
                {
                    int dayOfWeek = "一二三四五六日".IndexOf(match.Groups[1].Value) + 1;
                    int startPeriod = int.Parse(match.Groups[2].Value);
                    int endPeriod = int.Parse(match.Groups[3].Value);

                    result.Add((dayOfWeek, startPeriod, endPeriod));
                }
            }
            return result;
        }

        private bool CheckCreditLimit(Course newCourse)
        {
            decimal currentCredits = decimal.Parse(TotalCreditsText.Text);
            decimal maxCredits = 30; // 假设最大学分为30，实际值应从配置或数据库获取
            return currentCredits + newCourse.Credit <= maxCredits;
        }


        #endregion

        #region 系统消息通知

        private async Task CheckSystemNotifications()
        {
            try
            {
                var notifications = await _courseService.GetSystemNotificationsAsync(studentID);
                foreach (var notification in notifications)
                {
                    if (!notification.IsRead)
                    {
                        ShowNotification(notification);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"检查系统通知失败: {ex.Message}");
            }
        }

        private void ShowNotification(SystemNotification notification)
        {
            var notificationWindow = new NotificationWindow(notification)
            {
                Owner = Window.GetWindow(this)
            };
            notificationWindow.Show();
        }

        #endregion

        #region IDisposable 实现

        private bool _disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 释放托管资源
                    if (_searchDebounceTimer != null)
                    {
                        _searchDebounceTimer.Stop();
                        _searchDebounceTimer = null;
                    }
                }

                // 释放非托管资源
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~CourseSelectionView()
        {
            Dispose(false);
        }

        #endregion
    }
}

