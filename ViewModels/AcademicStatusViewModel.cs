// ViewModels/AcademicStatusViewModel.cs
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.Win32;
using UniAcamanageWpfApp.Services;
using System.Windows;
using UniAcamanageWpfApp.Models;
using Wpf.Ui.Input;

namespace UniAcamanageWpfApp.ViewModels
{
    public class AcademicStatusViewModel : INotifyPropertyChanged
    {
        private readonly IAcademicStatusService _academicService;
        private readonly string _studentId;

        // 使用自动属性和字段定义的通用方法
        private T SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                OnPropertyChanged(propertyName);
            }
            return value;
        }

        // 进度值
        private decimal _requiredProgress;
        public decimal RequiredProgress
        {
            get => _requiredProgress;
            set => SetProperty(ref _requiredProgress, value);
        }

        // 学分状态
        private string _requiredCreditsStatus;
        public string RequiredCreditsStatus
        {
            get => _requiredCreditsStatus;
            set => SetProperty(ref _requiredCreditsStatus, value);
        }

        // 专业课程进度
        private decimal _majorProgress;
        public decimal MajorProgress
        {
            get => _majorProgress;
            set => SetProperty(ref _majorProgress, value);
        }

        // 专业课程学分状态
        private string _majorCreditsStatus;
        public string MajorCreditsStatus
        {
            get => _majorCreditsStatus;
            set => SetProperty(ref _majorCreditsStatus, value);
        }

        // 选修课程进度
        private decimal _electiveProgress;
        public decimal ElectiveProgress
        {
            get => _electiveProgress;
            set => SetProperty(ref _electiveProgress, value);
        }

        // 选修课程学分状态
        private string _electiveCreditsStatus;
        public string ElectiveCreditsStatus
        {
            get => _electiveCreditsStatus;
            set => SetProperty(ref _electiveCreditsStatus, value);
        }

        // 课程统计
        private int _completedCourses;
        public int CompletedCourses
        {
            get => _completedCourses;
            set => SetProperty(ref _completedCourses, value);
        }

        private int _ongoingCourses;
        public int OngoingCourses
        {
            get => _ongoingCourses;
            set => SetProperty(ref _ongoingCourses, value);
        }

        private int _failedCourses;
        public int FailedCourses
        {
            get => _failedCourses;
            set => SetProperty(ref _failedCourses, value);
        }

        private int _remainingCourses;
        public int RemainingCourses
        {
            get => _remainingCourses;
            set
            {
                if (_remainingCourses != value)
                {
                    _remainingCourses = value;
                    OnPropertyChanged();
                }
            }
        }

        // 删除重复的字段定义，只保留一个
        private ObservableCollection<string> _semesters;
        public ObservableCollection<string> Semesters
        {
            get => _semesters;
            set
            {
                _semesters = value;
                OnPropertyChanged();
            }
        }

        private string _selectedSemester;
        public string SelectedSemester
        {
            get => _selectedSemester;
            set
            {
                if (_selectedSemester != value)
                {
                    _selectedSemester = value;
                    OnPropertyChanged();
                    // 当学期选择改变时，重新加载成绩数据
                    LoadGradesAsync().ConfigureAwait(false);
                }
            }
        }

        private ChartValues<double> _totalProgressValues;
        public ChartValues<double> TotalProgressValues
        {
            get => _totalProgressValues;
            set
            {
                _totalProgressValues = value;
                OnPropertyChanged();
            }
        }

        private double _totalProgress;
        public double TotalProgress
        {
            get => _totalProgress;
            set
            {
                _totalProgress = value;
                OnPropertyChanged();
            }
        }

        // 添加标签点格式化器
        public Func<ChartPoint, string> ProgressLabelPoint => point => $"{point.Participation:P0}";
        private SeriesCollection _courseCompletionSeries;
        public SeriesCollection CourseCompletionSeries
        {
            get => _courseCompletionSeries;
            set
            {
                _courseCompletionSeries = value;
                OnPropertyChanged();
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        #region Properties

        private ObservableCollection<GradeInfo> _gradesList;
        public ObservableCollection<GradeInfo> GradesList
        {
            get => _gradesList;
            set
            {
                _gradesList = value;
                OnPropertyChanged();
            }
        }

        private ObservableCollection<CourseCompletionInfo> _coursesList;
        public ObservableCollection<CourseCompletionInfo> CoursesList
        {
            get => _coursesList;
            set
            {
                _coursesList = value;
                OnPropertyChanged();
            }
        }

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                FilterGradesList();
            }
        }

        private decimal _overallGPA;
        public decimal OverallGPA
        {
            get => _overallGPA;
            set
            {
                _overallGPA = value;
                OnPropertyChanged();
            }
        }

        private decimal _baseRequiredGPA;
        public decimal BaseRequiredGPA
        {
            get => _baseRequiredGPA;
            set
            {
                _baseRequiredGPA = value;
                OnPropertyChanged();
            }
        }

        private decimal _majorRequiredGPA;
        public decimal MajorRequiredGPA
        {
            get => _majorRequiredGPA;
            set
            {
                _majorRequiredGPA = value;
                OnPropertyChanged();
            }
        }

        private decimal _electiveGPA;
        public decimal ElectiveGPA
        {
            get => _electiveGPA;
            set
            {
                _electiveGPA = value;
                OnPropertyChanged();
            }
        }

        private int _classRanking;
        public int ClassRanking
        {
            get => _classRanking;
            set
            {
                _classRanking = value;
                OnPropertyChanged();
            }
        }

        private SeriesCollection _gpaChartSeries;
        public SeriesCollection GPAChartSeries
        {
            get => _gpaChartSeries;
            set
            {
                _gpaChartSeries = value;
                OnPropertyChanged();
            }
        }

        private SeriesCollection _gradeDistributionSeries;
        public SeriesCollection GradeDistributionSeries
        {
            get => _gradeDistributionSeries;
            set
            {
                _gradeDistributionSeries = value;
                OnPropertyChanged();
            }
        }

        private string _currentMajor;
        public string CurrentMajor
        {
            get => _currentMajor;
            set
            {
                _currentMajor = value;
                OnPropertyChanged();
            }
        }

        private string _currentGrade;
        public string CurrentGrade
        {
            get => _currentGrade;
            set
            {
                _currentGrade = value;
                OnPropertyChanged();
            }
        }

        public ICommand ExportGradesCommand { get; }
        public ICommand ExportProgressCommand { get; }
        public ICommand SearchCommand { get; }

        #endregion

        public AcademicStatusViewModel(string studentId)
        {
            _studentId = studentId;
            _academicService = new AcademicStatusService();

            // Initialize collections
            GradesList = new ObservableCollection<GradeInfo>();
            CoursesList = new ObservableCollection<CourseCompletionInfo>();
            Semesters = new ObservableCollection<string>();
            TotalProgressValues = new ChartValues<double> { 0, 100 };
            GPATrendValues = new ChartValues<double>();
            SemesterLabels = new List<string>();


            // Initialize commands
            ExportGradesCommand = new RelayCommand(ExportGrades);
            ExportProgressCommand = new RelayCommand(ExportProgress);
            SearchCommand = new RelayCommand(FilterGradesList);

            // Load initial data
            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            try
            {
                IsLoading = true;

                // 并行加载不相关的数据以提高性能
                var studentInfoTask = _academicService.GetStudentInfoAsync(_studentId);
                var semestersTask = _academicService.GetSemestersAsync(_studentId);
                var academicStatsTask = _academicService.GetAcademicStatsAsync(_studentId);
                var gradesTask = _academicService.GetGradesAsync(_studentId, null);
                var programProgressTask = _academicService.GetProgramProgressAsync(_studentId);  // 添加这一行

                // 等待所有任务完成
                await Task.WhenAll(studentInfoTask, semestersTask, academicStatsTask, gradesTask, programProgressTask);

                // 更新学生信息
                var (major, grade) = await studentInfoTask;
                CurrentMajor = major;
                CurrentGrade = grade;

                // 更新学期列表
                var semesterList = await semestersTask;
                Semesters.Clear();
                Semesters.Add("全部学期");
                foreach (var semester in semesterList)
                {
                    Semesters.Add(semester);
                }

                // 更新学业统计
                var stats = await academicStatsTask;
                OverallGPA = stats.OverallGPA;
                BaseRequiredGPA = stats.BaseRequiredGPA;
                MajorRequiredGPA = stats.MajorRequiredGPA;
                ElectiveGPA = stats.ElectiveGPA;
                ClassRanking = stats.ClassRanking;

                // 更新成绩列表
                var grades = await gradesTask;
                GradesList = new ObservableCollection<GradeInfo>(grades);

                // 加载课程完成情况
                await LoadCourseCompletionAsync();

                // 更新图表
                await UpdateChartsAsync();

                // 加载学业进度
                await LoadProgramProgressAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载数据时发生错误：{ex.Message}", "错误",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadProgramProgressAsync()
        {
            try
            {
                IsLoading = true;
                var progress = await _academicService.GetProgramProgressAsync(_studentId);

                if (progress != null)
                {
                    // 更新UI属性
                    TotalProgress = Convert.ToDouble(progress.CompletionPercentage);  // 这个保持double类型
                    CompletedCredits = progress.CompletedCredits;
                    RequiredCredits = progress.TotalCredits;
                    RemainingCredits = progress.TotalCredits - progress.CompletedCredits;

                    // 更新各类课程进度 - 修改这三处，使用decimal
                    RequiredProgress = progress.BaseRequiredProgress.Percentage;  // 直接赋值，因为都是decimal
                    RequiredCreditsStatus = $"{progress.BaseRequiredProgress.CompletedCredits}/{progress.BaseRequiredProgress.TotalCredits}";

                    MajorProgress = progress.MajorRequiredProgress.Percentage;   // 直接赋值，因为都是decimal
                    MajorCreditsStatus = $"{progress.MajorRequiredProgress.CompletedCredits}/{progress.MajorRequiredProgress.TotalCredits}";

                    ElectiveProgress = progress.ElectiveProgress.Percentage;     // 直接赋值，因为都是decimal
                    ElectiveCreditsStatus = $"{progress.ElectiveProgress.CompletedCredits}/{progress.ElectiveProgress.TotalCredits}";

                    // 更新课程统计
                    CompletedCourses = progress.CompletedCourses;
                    OngoingCourses = progress.OngoingCourses;
                    FailedCourses = progress.FailedCourses;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载学业进度数据时发生错误：{ex.Message}", "错误",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadAcademicStatsAsync(string semester = null)
{
    try
    {
        // 修改 GetAcademicStatsAsync 方法，添加 semester 参数
        var stats = await _academicService.GetAcademicStatsAsync(_studentId, semester);
        if (stats != null)
        {
            OverallGPA = stats.OverallGPA;
            BaseRequiredGPA = stats.BaseRequiredGPA;
            MajorRequiredGPA = stats.MajorRequiredGPA;
            ElectiveGPA = stats.ElectiveGPA;
            ClassRanking = stats.ClassRanking;
        }
        else
        {
            MessageBox.Show("无法获取学业统计信息。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"加载学业统计信息时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}

private async Task LoadGradesAsync()
{
    try
    {
        IsLoading = true;
        string selectedSemester = SelectedSemester == "全部学期" ? null : SelectedSemester;

        // 加载成绩数据
        var grades = await _academicService.GetGradesAsync(_studentId, selectedSemester);
        GradesList = new ObservableCollection<GradeInfo>(grades ?? new List<GradeInfo>());

        // 加载对应学期的学业统计数据
        await LoadAcademicStatsAsync(selectedSemester);

        // 更新图表显示
        await UpdateChartsAsync();
    }
    catch (Exception ex)
    {
        MessageBox.Show($"加载成绩数据时发生错误：{ex.Message}", "错误",
                       MessageBoxButton.OK, MessageBoxImage.Error);
    }
    finally
    {
        IsLoading = false;
    }
}

        private async Task LoadSemestersAsync()
        {
            try
            {
                var semesterList = await _academicService.GetSemestersAsync(_studentId);
                Semesters = new ObservableCollection<string>();
                Semesters.Add("全部学期"); // 添加"全部学期"选项
                foreach (var semester in semesterList)
                {
                    Semesters.Add(semester);
                }

                // 默认选择"全部学期"
                SelectedSemester = "全部学期";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载学期数据时发生错误：{ex.Message}", "错误",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private ChartValues<double> _gpaTrendValues;
        public ChartValues<double> GPATrendValues
        {
            get => _gpaTrendValues;
            set
            {
                _gpaTrendValues = value;
                OnPropertyChanged();
            }
        }

        private List<string> _semesterLabels;
        public List<string> SemesterLabels
        {
            get => _semesterLabels;
            set
            {
                _semesterLabels = value;
                OnPropertyChanged();
            }
        }

        private async Task LoadCourseCompletionAsync()
        {
            try
            {
                IsLoading = true;
                var courses = await _academicService.GetCourseCompletionAsync(_studentId);

                if (courses != null && courses.Any())
                {
                    CoursesList = new ObservableCollection<CourseCompletionInfo>(courses);

                    // 更新课程状态统计
                    CompletedCourses = courses.Count(c => c.Status == "已修完成");
                    OngoingCourses = courses.Count(c => c.Status == "正在修读");
                    FailedCourses = courses.Count(c => c.Status == "未通过");
                    RemainingCourses = courses.Count(c => c.Status == "未修");

                    // 更新课程完成进度
                    var requiredCourses = courses.Where(c => c.IsRequired).ToList();
                    if (requiredCourses.Any())
                    {
                        var completedRequired = requiredCourses.Count(c => c.Status == "已修完成");
                        TotalProgress = Math.Round((double)completedRequired / requiredCourses.Count * 100, 1);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载课程完成情况时发生错误：{ex.Message}", "错误",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // 添加所需的属性
        private decimal _completedCredits;
        public decimal CompletedCredits
        {
            get => _completedCredits;
            set
            {
                _completedCredits = value;
                OnPropertyChanged();
            }
        }

        private decimal _requiredCredits;
        public decimal RequiredCredits
        {
            get => _requiredCredits;
            set
            {
                _requiredCredits = value;
                OnPropertyChanged();
            }
        }

        private decimal _remainingCredits;
        public decimal RemainingCredits
        {
            get => _remainingCredits;
            set
            {
                _remainingCredits = value;
                OnPropertyChanged();
            }
        }

        private async Task UpdateChartsAsync()
        {
            try
            {
                // 更新 GPA 趋势图
                var semesterGPAs = await _academicService.GetSemesterGPAsAsync(_studentId);
                GPATrendValues = new ChartValues<double>(semesterGPAs);

                GPAChartSeries = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "学期GPA",
                    Values = GPATrendValues,
                    PointGeometry = DefaultGeometries.Circle,
                    PointGeometrySize = 8
                }
            };

                // 更新成绩分布图
                var distribution = await _academicService.GetGradeDistributionAsync(_studentId);
                GradeDistributionSeries = new SeriesCollection();
                foreach (var item in distribution)
                {
                    GradeDistributionSeries.Add(new PieSeries
                    {
                        Title = item.Key,
                        Values = new ChartValues<double> { item.Value },
                        DataLabels = true,
                        LabelPoint = point => $"{item.Key}: {point.Y:F1}%"
                    });
                }

                OnPropertyChanged(nameof(GPATrendValues));
                OnPropertyChanged(nameof(GPAChartSeries));
                OnPropertyChanged(nameof(GradeDistributionSeries));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"更新图表时发生错误：{ex.Message}", "错误",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task UpdateProgramProgressAsync()
        {
            try
            {
                var progress = await _academicService.GetProgramProgressAsync(_studentId);

                // 更新总体进度 - 添加显式转换
                TotalProgress = Convert.ToDouble(progress.CompletionPercentage);
                CompletedCredits = progress.CompletedCredits;
                RequiredCredits = progress.TotalCredits;
                RemainingCredits = progress.TotalCredits - progress.CompletedCredits;

                // 更新各类课程进度 - 添加显式转换
                RequiredProgress = Convert.ToDecimal(progress.BaseRequiredProgress.Percentage);
                RequiredCreditsStatus = $"{progress.BaseRequiredProgress.CompletedCredits}/{progress.BaseRequiredProgress.TotalCredits}";

                MajorProgress = Convert.ToDecimal(progress.MajorRequiredProgress.Percentage);
                MajorCreditsStatus = $"{progress.MajorRequiredProgress.CompletedCredits}/{progress.MajorRequiredProgress.TotalCredits}";

                ElectiveProgress = Convert.ToDecimal(progress.ElectiveProgress.Percentage);
                ElectiveCreditsStatus = $"{progress.ElectiveProgress.CompletedCredits}/{progress.ElectiveProgress.TotalCredits}";

                // 更新课程统计
                CompletedCourses = progress.CompletedCourses;
                OngoingCourses = progress.OngoingCourses;
                FailedCourses = progress.FailedCourses;

                // 更新进度环图数据 - 使用显式转换
                var completionPercentage = Convert.ToDouble(progress.CompletionPercentage);
                TotalProgressValues = new ChartValues<double>
        {
            completionPercentage,
            100 - completionPercentage
        };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"更新培养方案进度时发生错误：{ex.Message}", "错误",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FilterGradesList()
        {
            if (GradesList == null) return;

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                LoadGradesAsync();
                return;
            }

            var filteredList = GradesList.Where(g =>
                (g.CourseCode?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (g.CourseName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();

            GradesList = new ObservableCollection<GradeInfo>(filteredList);
        }

        private async void ExportGrades()
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "Excel文件|*.xlsx",
                    DefaultExt = ".xlsx",
                    FileName = $"成绩单_{_studentId}_{DateTime.Now:yyyyMMdd}"
                };

                if (dialog.ShowDialog() == true)
                {
                    using var workbook = new ClosedXML.Excel.XLWorkbook();
                    var worksheet = workbook.Worksheets.Add("成绩单");

                    // Add headers
                    var headers = new[] { "学期", "课程代码", "课程名称", "课程类型", "学分",
                        "成绩", "等级", "绩点", "加权绩点", "修读状态", "备注" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        worksheet.Cell(1, i + 1).Value = headers[i];
                    }

                    // Add data
                    int row = 2;
                    foreach (var grade in GradesList)
                    {
                        worksheet.Cell(row, 1).Value = grade.Semester;
                        worksheet.Cell(row, 2).Value = grade.CourseCode;
                        worksheet.Cell(row, 3).Value = grade.CourseName;
                        worksheet.Cell(row, 4).Value = grade.CourseType;
                        worksheet.Cell(row, 5).Value = grade.Credit;
                        worksheet.Cell(row, 6).Value = grade.Score;
                        worksheet.Cell(row, 7).Value = grade.GradeLevel;
                        worksheet.Cell(row, 8).Value = grade.BaseGradePoint;
                        worksheet.Cell(row, 9).Value = grade.WeightedGradePoint;
                        worksheet.Cell(row, 10).Value = grade.CourseStatus;
                        worksheet.Cell(row, 11).Value = grade.Remarks;
                        row++;
                    }

                    // Add summary
                    row += 2;
                    worksheet.Cell(row, 1).Value = "总体GPA";
                    worksheet.Cell(row, 2).Value = OverallGPA;
                    worksheet.Cell(row + 1, 1).Value = "基础必修GPA";
                    worksheet.Cell(row + 1, 2).Value = BaseRequiredGPA;
                    worksheet.Cell(row + 2, 1).Value = "专业必修GPA";
                    worksheet.Cell(row + 2, 2).Value = MajorRequiredGPA;
                    worksheet.Cell(row + 3, 1).Value = "选修GPA";
                    worksheet.Cell(row + 3, 2).Value = ElectiveGPA;

                    workbook.SaveAs(dialog.FileName);
                    MessageBox.Show("成绩单导出成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出成绩单时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ExportProgress()
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "Excel文件|*.xlsx",
                    DefaultExt = ".xlsx",
                    FileName = $"学业完成情况_{_studentId}_{DateTime.Now:yyyyMMdd}"
                };

                if (dialog.ShowDialog() == true)
                {
                    using var workbook = new ClosedXML.Excel.XLWorkbook();
                    var worksheet = workbook.Worksheets.Add("学业完成情况");

                    // Add headers
                    var headers = new[] { "课程代码", "课程名称", "课程类型", "学分",
                        "状态", "修读学期", "成绩", "备注" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        worksheet.Cell(1, i + 1).Value = headers[i];
                    }

                    // Add data
                    int row = 2;
                    foreach (var course in CoursesList)
                    {
                        worksheet.Cell(row, 1).Value = course.CourseCode;
                        worksheet.Cell(row, 2).Value = course.CourseName;
                        worksheet.Cell(row, 3).Value = course.CourseType;
                        worksheet.Cell(row, 4).Value = course.Credit;
                        worksheet.Cell(row, 5).Value = course.Status;
                        worksheet.Cell(row, 6).Value = course.Semester;
                        worksheet.Cell(row, 7).Value = course.Score;
                        worksheet.Cell(row, 8).Value = course.Remarks;
                        row++;
                    }

                    workbook.SaveAs(dialog.FileName);
                    MessageBox.Show("学业完成情况报告导出成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出报告时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    // 实现 RelayCommand
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute();
        }

        public void Execute(object parameter)
        {
            _execute();
        }
    }
}
