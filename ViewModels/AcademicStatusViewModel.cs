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

        private readonly IAcademicStatusService _academicService;
        private readonly string _studentId;

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

        private string _selectedSemester;
        public string SelectedSemester
        {
            get => _selectedSemester;
            set
            {
                _selectedSemester = value;
                OnPropertyChanged();
                LoadGradesAsync();
            }
        }

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

                // 等待所有任务完成
                await Task.WhenAll(studentInfoTask, semestersTask, academicStatsTask, gradesTask);

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

        private async Task LoadAcademicStatsAsync()
        {
            try
            {
                var stats = await _academicService.GetAcademicStatsAsync(_studentId);
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
                var grades = await _academicService.GetGradesAsync(_studentId,
                    SelectedSemester == "全部学期" ? null : SelectedSemester);

                if (grades != null && grades.Any())
                {
                    GradesList = new ObservableCollection<GradeInfo>(grades);
                    await UpdateChartsAsync(); // 更新图表以反映新的成绩数据
                }
                else
                {
                    GradesList.Clear();
                    MessageBox.Show("没有找到相关成绩记录。", "提示",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
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

        private async Task LoadCourseCompletionAsync()
        {
            try
            {
                var courses = await _academicService.GetCourseCompletionAsync(_studentId);
                if (courses != null && courses.Any())
                {
                    CoursesList = new ObservableCollection<CourseCompletionInfo>(courses);

                    // 计算完成比例
                    int totalCourses = courses.Count;
                    int completedCourses = courses.Count(c => c.Status == "已修完成");
                    double completionPercentage = (double)completedCourses / totalCourses * 100;

                    // 更新进度值
                    TotalProgress = Math.Round(completionPercentage, 1);
                    TotalProgressValues = new ChartValues<double> { completionPercentage, 100 - completionPercentage };

                    // 更新学分统计
                    decimal totalCredits = courses.Sum(c => c.Credit);
                    decimal completedCredits = courses.Where(c => c.Status == "已修完成").Sum(c => c.Credit);
                    decimal remainingCredits = totalCredits - completedCredits;

                    // 更新绑定属性
                    CompletedCredits = completedCredits;
                    RequiredCredits = totalCredits;
                    RemainingCredits = remainingCredits;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载课程完成情况时发生错误：{ex.Message}", "错误",
                               MessageBoxButton.OK, MessageBoxImage.Error);
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
                // 更新 GPA 趋势图表
                var semesterGPAs = await _academicService.GetSemesterGPAsAsync(_studentId);
                if (semesterGPAs != null && semesterGPAs.Any())
                {
                    GPAChartSeries = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "学期GPA",
                    Values = new ChartValues<double>(semesterGPAs),
                    PointGeometry = DefaultGeometries.Circle,
                    PointGeometrySize = 8
                }
            };
                }

                // 更新成绩分布图表
                var distribution = await _academicService.GetGradeDistributionAsync(_studentId);
                if (distribution != null && distribution.Any())
                {
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
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"更新图表时发生错误：{ex.Message}", "错误",
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
