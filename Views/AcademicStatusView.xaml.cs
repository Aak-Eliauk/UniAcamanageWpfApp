using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Wpf;
using UniAcamanageWpfApp.ViewModels;
using UniAcamanageWpfApp.Services;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using System.ComponentModel;
using System.Windows.Data;
using UniAcamanageWpfApp.Models;
using HandyControl.Controls;
using System.Collections.ObjectModel;
// 添加以下别名定义
using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using System.Diagnostics;
using System.Net.Http;


namespace UniAcamanageWpfApp.Views
{
    public partial class AcademicStatusView : UserControl
    {
        private readonly AcademicStatusViewModel _viewModel;
        private readonly string _studentId;
        private System.Windows.Threading.DispatcherTimer _searchDebounceTimer;

        // 图表相关字段
        private SeriesCollection _gpaChartSeries;
        private SeriesCollection _gradeDistributionSeries;
        private SeriesCollection _totalProgressSeries;

        // 颜色定义
        private readonly SolidColorBrush _primaryBrush = (SolidColorBrush)Application.Current.Resources["PrimaryBrush"];
        private readonly SolidColorBrush _secondaryBrush = (SolidColorBrush)Application.Current.Resources["SecondaryBrush"];
        private readonly SolidColorBrush _accentBrush = (SolidColorBrush)Application.Current.Resources["AccentBrush"];

        public AcademicStatusView(string studentId)
        {
            InitializeComponent();
            _studentId = studentId;
            _viewModel = new AcademicStatusViewModel(studentId);
            DataContext = _viewModel;

            // 在构造函数中初始化 _searchDebounceTimer
            _searchDebounceTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;


            InitializeCharts();
            InitializeEventHandlers();
            ConfigureDataGrids();
        }

        private void SearchDebounceTimer_Tick(object sender, EventArgs e)
        {
            _searchDebounceTimer.Stop();
            if (SearchBox is TextBox searchBox)
            {
                PerformSearch(searchBox.Text);
            }
        }

        #region Initialization Methods

        private void InitializeCharts()
        {
            // 配置GPA趋势图
            ConfigureGPAChart();

            // 配置成绩分布饼图
            ConfigureGradeDistributionChart();

            // 配置总进度环形图
            ConfigureTotalProgressChart();

            // 设置图表数据绑定
            SetupChartBindings();
        }


        private void ConfigureGPAChart()
        {
            var gpaChart = this.FindName("GpaChart") as CartesianChart;
            if (gpaChart != null)
            {
                gpaChart.DisableAnimations = false;
                gpaChart.Hoverable = true;
                gpaChart.DataTooltip = new DefaultTooltip
                {
                    SelectionMode = TooltipSelectionMode.SharedXValues
                };

                gpaChart.AxisX = new AxesCollection
                {
                    new Axis
                    {
                        Title = "学期",
                        Separator = new LiveCharts.Wpf.Separator
                        {
                            Step = 1,
                            StrokeThickness = 0
                        }
                    }
                };

                gpaChart.AxisY = new AxesCollection
                {
                    new Axis
                    {
                        Title = "GPA",
                        MinValue = 0,
                        MaxValue = 4.0,
                        LabelFormatter = value => value.ToString("F1")
                    }
                };
            }
        }

        private void ConfigureGradeDistributionChart()
        {
            var distributionChart = this.FindName("GradeDistributionChart") as PieChart;
            if (distributionChart != null)
            {
                distributionChart.LegendLocation = LegendLocation.Right;
                distributionChart.ChartLegend = new DefaultLegend
                {
                    BulletSize = 10,
                    Foreground = new SolidColorBrush(Colors.Black),
                    Orientation = Orientation.Vertical
                };
                distributionChart.DataTooltip = new DefaultTooltip
                {
                    SelectionMode = TooltipSelectionMode.OnlySender
                };
            }
        }

        private void ConfigureTotalProgressChart()
        {
            var progressChart = this.FindName("TotalProgressChart") as PieChart;
            if (progressChart != null)
            {
                progressChart.DisableAnimations = true;
                progressChart.StartingRotationAngle = -90;
                progressChart.InnerRadius = 80;
                progressChart.LegendLocation = LegendLocation.None;
                progressChart.DataTooltip = null;
            }
        }

        private void SetupChartBindings()
        {
            // 设置图表数据源绑定
            if (_viewModel != null)
            {
                _gpaChartSeries = _viewModel.GPAChartSeries;
                _gradeDistributionSeries = _viewModel.GradeDistributionSeries;
                _totalProgressSeries = new SeriesCollection
                {
                    new PieSeries
                    {
                        Values = new ChartValues<double> { _viewModel.TotalProgress },
                        Fill = _primaryBrush,
                        StrokeThickness = 0
                    },
                    new PieSeries
                    {
                        Values = new ChartValues<double> { 100 - _viewModel.TotalProgress },
                        Fill = new SolidColorBrush(Color.FromRgb(230, 230, 230)),
                        StrokeThickness = 0
                    }
                };
            }
        }

        private void InitializeEventHandlers()
        {
            // 注册学期选择改变事件
            var semesterComboBox = this.FindName("SemesterComboBox") as ComboBox;
            if (semesterComboBox != null)
            {
                semesterComboBox.SelectionChanged += SemesterComboBox_SelectionChanged;
            }

            // 注册课程类型筛选事件
            var courseTypeComboBox = this.FindName("CourseTypeComboBox") as ComboBox;
            if (courseTypeComboBox != null)
            {
                courseTypeComboBox.SelectionChanged += CourseTypeComboBox_SelectionChanged;
            }

            // 注册课程状态筛选事件
            var courseStatusComboBox = this.FindName("CourseStatusComboBox") as ComboBox;
            if (courseStatusComboBox != null)
            {
                courseStatusComboBox.SelectionChanged += CourseStatusComboBox_SelectionChanged;
            }

            // 注册搜索框事件
            var searchBox = this.FindName("SearchBox") as TextBox;
            if (searchBox != null)
            {
                searchBox.TextChanged += SearchBox_TextChanged;
                searchBox.KeyDown += SearchBox_KeyDown;
            }
        }

        private void ConfigureDataGrids()
        {
            // 配置成绩明细表格
            if (GradesDataGrid != null)
            {
                GradesDataGrid.LoadingRow += DataGrid_LoadingRow;
                GradesDataGrid.Sorting += DataGrid_Sorting;
            }

            // 配置课程完成情况表格
            if (CourseCompletionGrid != null)
            {
                CourseCompletionGrid.LoadingRow += DataGrid_LoadingRow;
                CourseCompletionGrid.Sorting += DataGrid_Sorting;
            }
        }

        #endregion

        #region Event Handlers

        private void SemesterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem != null)
            {
                _viewModel.SelectedSemester = comboBox.SelectedItem.ToString();
            }
        }

        private void CourseTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string courseType = (selectedItem.Content as string) ?? "全部课程";
                FilterCourseCompletionGrid(courseType: courseType);
            }
        }

        private void CourseStatusComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string status = (selectedItem.Content as string) ?? "全部状态";
                FilterCourseCompletionGrid(status: status);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox searchBox)
            {
                // 使用防抖动处理，避免频繁搜索
                if (_searchDebounceTimer != null)
                {
                    _searchDebounceTimer.Stop();
                }
                else
                {
                    _searchDebounceTimer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(300)
                    };
                    _searchDebounceTimer.Tick += (s, args) =>
                    {
                        _searchDebounceTimer.Stop();
                        PerformSearch(searchBox.Text);
                    };
                }
                _searchDebounceTimer.Start();
            }
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox searchBox)
            {
                PerformSearch(searchBox.Text);
                e.Handled = true;
            }
        }

        private void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }

        private void DataGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            // 处理自定义排序逻辑
            if (sender is DataGrid grid)
            {
                string sortMemberPath = e.Column.SortMemberPath;
                ListSortDirection direction = e.Column.SortDirection != ListSortDirection.Ascending
                    ? ListSortDirection.Ascending
                    : ListSortDirection.Descending;

                // 更新排序指示器
                e.Column.SortDirection = direction;

                // 执行排序
                ICollectionView view = CollectionViewSource.GetDefaultView(grid.ItemsSource);
                view.SortDescriptions.Clear();
                view.SortDescriptions.Add(new SortDescription(sortMemberPath, direction));

                e.Handled = true;
            }
        }

        private void ExportGrades_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                _viewModel.ExportGradesCommand.Execute(null);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void ExportProgress_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                _viewModel.ExportProgressCommand.Execute(null);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        #endregion

        #region Private Helper Methods

        private void PerformSearch(string searchText)
                {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                // 重置筛选
                _viewModel.SearchText = string.Empty;
                return;
            }

            _viewModel.SearchText = searchText;
        }

        private void FilterCourseCompletionGrid(string courseType = null, string status = null)
        {
            if (CourseCompletionGrid?.ItemsSource == null) return;

            ICollectionView view = CollectionViewSource.GetDefaultView(CourseCompletionGrid.ItemsSource);
            view.Filter = item =>
            {
                if (item is CourseCompletionInfo course)
                {
                    bool matchesCourseType = string.IsNullOrEmpty(courseType) ||
                                           courseType == "全部课程" ||
                                           course.CourseType == courseType;

                    bool matchesStatus = string.IsNullOrEmpty(status) ||
                                       status == "全部状态" ||
                                       course.Status == status;

                    return matchesCourseType && matchesStatus;
                }
                return false;
            };
        }

        private void UpdateCharts()
        {
            // 更新GPA趋势图
            if (_viewModel.GPAChartSeries != null && GpaChart != null)
            {
                GpaChart.Series = _viewModel.GPAChartSeries;
            }

            // 更新成绩分布饼图
            if (_viewModel.GradeDistributionSeries != null && GradeDistributionChart != null)
            {
                GradeDistributionChart.Series = _viewModel.GradeDistributionSeries;
            }

            // 更新总进度环形图
            if (_totalProgressSeries != null && TotalProgressChart != null)
            {
                TotalProgressChart.Series = _totalProgressSeries;
            }
        }

        // 用于处理进度数值格式化
        private Func<ChartPoint, string> ProgressLabelPoint => chartPoint =>
            $"{Math.Round(chartPoint.Participation * 100, 1)}%";

        #endregion
    }
}