using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Threading.Tasks;
using UniAcamanageWpfApp.Models;
using UniAcamanageWpfApp.Services;
using UniAcamanageWpfApp.Commands;
using System.Windows.Controls;
using System.Windows.Media;
using DocumentFormat.OpenXml.Wordprocessing;
using Style = System.Windows.Style;
using ClosedXML.Excel;
using Microsoft.Win32;
using System.Windows.Documents;
using System.Diagnostics;


namespace UniAcamanageWpfApp.ViewModels
{
    public class InfoManagementViewModel : INotifyPropertyChanged
    {
        private readonly IInfoManagementService _infoService;

        #region Collections
        public ObservableCollection<Teacher> Teachers { get; private set; }
        public ObservableCollection<Student> Students { get; private set; }
        public ObservableCollection<Department> Departments { get; private set; }
        public ObservableCollection<Class> Classes { get; private set; }
        public ObservableCollection<Semester> Semesters { get; private set; }
        public ObservableCollection<string> Majors { get; private set; }
        public ObservableCollection<string> Years { get; private set; }
        public ObservableCollection<string> YearRanges { get; private set; }
        public ObservableCollection<string> SemesterTypes { get; private set; }
        public ObservableCollection<Student> ClassStudents { get; private set; }
        #endregion

        #region Selected Items
        private ObservableCollection<Course> _courseList;
        public ObservableCollection<Course> CourseList
        {
            get => _courseList;
            set
            {
                if (_courseList != value)
                {
                    _courseList = value;
                    OnPropertyChanged(nameof(CourseList));
                }
            }
        }

        private ObservableCollection<TeacherCourse> _teacherCourseList;
        public ObservableCollection<TeacherCourse> TeacherCourseList
        {
            get => _teacherCourseList;
            set
            {
                if (_teacherCourseList != value)
                {
                    _teacherCourseList = value;
                    OnPropertyChanged(nameof(TeacherCourseList));
                }
            }
        }

        private Department _selectedDepartment;
        public Department SelectedDepartment
        {
            get => _selectedDepartment;
            set
            {
                _selectedDepartment = value;
                OnPropertyChanged(nameof(SelectedDepartment));
                LoadClassesForDepartmentAsync().ConfigureAwait(false);
            }
        }

        private Class _selectedClass;
        public Class SelectedClass
        {
            get => _selectedClass;
            set
            {
                _selectedClass = value;
                OnPropertyChanged(nameof(SelectedClass));
            }
        }

        private Student _selectedStudent;
        public Student SelectedStudent
        {
            get => _selectedStudent;
            set
            {
                _selectedStudent = value;
                OnPropertyChanged(nameof(SelectedStudent));
            }
        }

        private Teacher _selectedTeacher;
        public Teacher SelectedTeacher
        {
            get => _selectedTeacher;
            set
            {
                _selectedTeacher = value;
                OnPropertyChanged(nameof(SelectedTeacher));
            }
        }

        private Semester _selectedSemester;
        public Semester SelectedSemester
        {
            get => _selectedSemester;
            set
            {
                _selectedSemester = value;
                OnPropertyChanged(nameof(SelectedSemester));
            }
        }

        private int _selectedYear;
        public int SelectedYear
        {
            get => _selectedYear;
            set
            {
                _selectedYear = value;
                OnPropertyChanged(nameof(SelectedYear));
            }
        }

        private string _selectedMajor;
        public string SelectedMajor
        {
            get => _selectedMajor;
            set
            {
                _selectedMajor = value;
                OnPropertyChanged(nameof(SelectedMajor));
            }
        }

        private string _selectedYearRange;
        public string SelectedYearRange
        {
            get => _selectedYearRange;
            set
            {
                _selectedYearRange = value;
                OnPropertyChanged(nameof(SelectedYearRange));
            }
        }

        private string _selectedSemesterType;
        public string SelectedSemesterType
        {
            get => _selectedSemesterType;
            set
            {
                _selectedSemesterType = value;
                OnPropertyChanged(nameof(SelectedSemesterType));
            }
        }

        // 在现有的 InfoManagementViewModel 类中添加
        private Semester _newSemester = new Semester();
        public Semester NewSemester
        {
            get { return _newSemester; }
            set
            {
                if (_newSemester != value)
                {
                    _newSemester = value;
                    OnPropertyChanged(nameof(NewSemester));
                }
            }
        }

        private Semester _currentSemester;
        public Semester CurrentSemester
        {
            get { return _currentSemester; }
            set
            {
                if (_currentSemester != value)
                {
                    _currentSemester = value;
                    OnPropertyChanged(nameof(CurrentSemester));
                }
            }
        }

        #endregion

        #region Search Parameters
        private string _studentSearchText;
        public string StudentSearchText
        {
            get => _studentSearchText;
            set
            {
                _studentSearchText = value;
                OnPropertyChanged(nameof(StudentSearchText));
            }
        }

        private string _teacherSearchText;
        public string TeacherSearchText
        {
            get => _teacherSearchText;
            set
            {
                _teacherSearchText = value;
                OnPropertyChanged(nameof(TeacherSearchText));
            }
        }
        #endregion

        #region Dialog Properties
        private bool _isSemesterDialogOpen;
        public bool IsSemesterDialogOpen
        {
            get => _isSemesterDialogOpen;
            set
            {
                _isSemesterDialogOpen = value;
                OnPropertyChanged(nameof(IsSemesterDialogOpen));
            }
        }

        private bool _isEditDialogOpen;
        public bool IsEditDialogOpen
        {
            get => _isEditDialogOpen;
            set
            {
                _isEditDialogOpen = value;
                OnPropertyChanged(nameof(IsEditDialogOpen));
            }
        }
        #endregion

        #region Commands
        public ICommand SearchTeacherCommand { get; private set; }
        public ICommand EditTeacherCommand { get; private set; }
        public ICommand ViewTeacherDetailCommand { get; private set; }
        public ICommand SearchStudentCommand { get; private set; }
        public ICommand EditStudentCommand { get; private set; }
        public ICommand ViewStudentDetailCommand { get; private set; }
        public ICommand QueryClassListCommand { get; private set; }
        public ICommand AddSemesterCommand { get; private set; }
        public ICommand EditSemesterCommand { get; private set; }
        public ICommand DeleteSemesterCommand { get; private set; }
        public ICommand SaveSemesterCommand { get; private set; }
        public ICommand ExportStudentListCommand { get; private set; }
        public ICommand PrintStudentListCommand { get; private set; }
        public ICommand QuerySemesterCommand { get; private set; }
        public ICommand SetCurrentSemesterCommand { get; private set; }
        #endregion

        #region Loading State
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
            }
        }
        #endregion

        #region Statistics
        private int _totalStudents;
        public int TotalStudents
        {
            get => _totalStudents;
            set
            {
                _totalStudents = value;
                OnPropertyChanged(nameof(TotalStudents));
            }
        }
        #endregion

        public InfoManagementViewModel(IInfoManagementService infoService)
        {
            _infoService = infoService ?? throw new ArgumentNullException(nameof(infoService));
            InitializeCollections();
            InitializeCommands();
            LoadInitialDataAsync().ConfigureAwait(false);
            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            try
            {
                IsLoading = true;
                await LoadInitialDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void InitializeCollections()
        {
            Teachers = new ObservableCollection<Teacher>();
            Students = new ObservableCollection<Student>();
            Departments = new ObservableCollection<Department>();
            Classes = new ObservableCollection<Class>();
            Semesters = new ObservableCollection<Semester>();
            Majors = new ObservableCollection<string>();
            Years = new ObservableCollection<string>();
            YearRanges = new ObservableCollection<string>();
            SemesterTypes = new ObservableCollection<string>();
            ClassStudents = new ObservableCollection<Student>();

            // 初始化年份和学期类型
            var currentYear = DateTime.Now.Year;
            for (int i = 0; i < 5; i++)
            {
                Years.Add((currentYear - i).ToString());
                YearRanges.Add($"{currentYear - i}-{currentYear - i + 1}学年");
            }

            SemesterTypes.Add("全部学期");
            SemesterTypes.Add("第一学期");
            SemesterTypes.Add("第二学期");
        }

        private void InitializeCommands()
        {
            SearchTeacherCommand = new RelayCommand(async () => await SearchTeachersAsync());
            EditTeacherCommand = new RelayCommand<Teacher>(async (teacher) => await EditTeacherAsync(teacher));
            ViewTeacherDetailCommand = new RelayCommand<Teacher>(ViewTeacherDetail);
            SearchStudentCommand = new RelayCommand(async () => await SearchStudentsAsync());
            EditStudentCommand = new RelayCommand<Student>(async (student) => await EditStudentAsync(student));
            ViewStudentDetailCommand = new RelayCommand<Student>(ViewStudentDetail);
            QueryClassListCommand = new RelayCommand(async () => await QueryClassListAsync());
            AddSemesterCommand = new RelayCommand(async () => await AddSemesterAsync());
            EditSemesterCommand = new RelayCommand<Semester>(async (semester) => await EditSemesterAsync(semester));
            DeleteSemesterCommand = new RelayCommand<Semester>(async (semester) => await DeleteSemesterAsync(semester));
            SaveSemesterCommand = new RelayCommand(async () => await SaveSemesterAsync());
            ExportStudentListCommand = new RelayCommand(ExportStudentList);
            PrintStudentListCommand = new RelayCommand(PrintStudentList);
            QuerySemesterCommand = new RelayCommand(async () => await QuerySemesterAsync());
            SetCurrentSemesterCommand = new RelayCommand<Semester>(async (semester) => await SetCurrentSemesterAsync(semester));
        }

        private async Task LoadInitialDataAsync()
        {
            try
            {
                IsLoading = true;
                var departments = await _infoService.GetDepartmentsAsync();
                Departments.Clear();
                foreach (var dept in departments)
                {
                    Departments.Add(dept);
                }

                await LoadSemestersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        #region Business Logic Methods
        private async Task SearchTeachersAsync()
        {
            try
            {
                IsLoading = true;
                // 实现搜索教师的逻辑
            }
            catch (Exception ex)
            {
                MessageBox.Show($"搜索教师失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SearchStudentsAsync()
        {
            try
            {
                IsLoading = true;
                var students = await _infoService.SearchStudentsAsync(
                    StudentSearchText,
                    _selectedMajor,
                    _selectedClass?.ClassID);

                Students.Clear();
                foreach (var student in students)
                {
                    Students.Add(student);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"搜索学生失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadClassesForDepartmentAsync()
        {
            if (SelectedDepartment == null) return;

            try
            {
                IsLoading = true;
                var classes = await _infoService.GetClassesByDepartmentAsync(SelectedDepartment.DepartmentID);
                Classes.Clear();
                foreach (var cls in classes)
                {
                    Classes.Add(cls);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载班级数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                IsLoading = true;
                var semesters = await _infoService.GetSemestersAsync();
                Semesters.Clear();
                foreach (var semester in semesters)
                {
                    Semesters.Add(semester);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载学期数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ViewTeacherDetail(Teacher teacher)
        {
            if (teacher == null)
            {
                MessageBox.Show("请先选择要查看的教师", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // 创建详情对话框内容
                var dialogContent = new StackPanel { Margin = new Thickness(16) };
                dialogContent.Children.Add(new TextBlock
                {
                    Text = "教师详细信息",
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 20)
                });

                // 添加教师信息字段
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // 添加字段标签和值
                AddDetailRow(grid, 0, "工号:", teacher.TeacherID);
                AddDetailRow(grid, 1, "姓名:", teacher.Name);
                AddDetailRow(grid, 2, "职称:", teacher.Title);
                AddDetailRow(grid, 3, "所属院系:", teacher.DepartmentName);
                AddDetailRow(grid, 4, "联系电话:", teacher.Phone);
                AddDetailRow(grid, 5, "电子邮箱:", teacher.Email);

                dialogContent.Children.Add(grid);

                // 添加授课信息（如果有）
                if (teacher.TeacherCourses != null && teacher.TeacherCourses.Any())
                {
                    dialogContent.Children.Add(new TextBlock
                    {
                        Text = "授课信息",
                        FontSize = 16,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 20, 0, 10)
                    });

                    var coursesList = new ListView
                    {
                        MaxHeight = 200,
                        Margin = new Thickness(0, 0, 0, 20)
                    };

                    foreach (var course in teacher.TeacherCourses)
                    {
                        coursesList.Items.Add(new TextBlock
                        {
                            Text = $"{course.CourseName} ({course.CourseCode}) - {course.SemesterName}",
                            Margin = new Thickness(0, 5, 0, 5)
                        });
                    }

                    dialogContent.Children.Add(coursesList);
                }

                // 添加确定按钮
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 16, 0, 0)
                };

                var okButton = new Button
                {
                    Content = "确定",
                    Style = Application.Current.Resources["MaterialDesignRaisedButton"] as Style,
                    Margin = new Thickness(8, 0, 0, 0)
                };
                okButton.Click += (s, e) => CloseDetailDialog();

                buttonPanel.Children.Add(okButton);
                dialogContent.Children.Add(buttonPanel);

                // 显示对话框
                var dialog = new MaterialDesignThemes.Wpf.DialogHost
                {
                    DialogContent = dialogContent,
                    IsOpen = true
                };

                _currentDialog = dialog;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"显示教师详情时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 辅助方法：添加详情行
        private void AddDetailRow(Grid grid, int row, string label, string value)
        {
            var labelBlock = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 10, 5),
                FontWeight = FontWeights.SemiBold
            };

            var valueBlock = new TextBlock
            {
                Text = value ?? "暂无",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5),
                TextWrapping = TextWrapping.Wrap
            };

            Grid.SetRow(labelBlock, row);
            Grid.SetColumn(labelBlock, 0);
            Grid.SetRow(valueBlock, row);
            Grid.SetColumn(valueBlock, 1);

            grid.Children.Add(labelBlock);
            grid.Children.Add(valueBlock);
        }

        // 关闭详情对话框
        private void CloseDetailDialog()
        {
            if (_currentDialog != null)
            {
                _currentDialog.IsOpen = false;
                _currentDialog = null;
            }
        }

        // 添加字段用于跟踪当前对话框
        private MaterialDesignThemes.Wpf.DialogHost _currentDialog;

        private void ViewStudentDetail(Student student)
        {
            if (student == null)
            {
                MessageBox.Show("请先选择要查看的学生", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // 创建详情对话框内容
                var dialogContent = new StackPanel { Margin = new Thickness(16) };

                // 添加标题
                dialogContent.Children.Add(new TextBlock
                {
                    Text = "学生详细信息",
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 20)
                });

                // 创建基本信息区域
                var basicInfoGrid = new Grid();
                basicInfoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                basicInfoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // 添加行定义
                for (int i = 0; i < 11; i++)
                {
                    basicInfoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                }

                // 添加基本信息字段
                int row = 0;
                AddDetailRow(basicInfoGrid, row++, "学号:", student.StudentID);
                AddDetailRow(basicInfoGrid, row++, "姓名:", student.Name);
                AddDetailRow(basicInfoGrid, row++, "性别:", student.Gender);
                AddDetailRow(basicInfoGrid, row++, "出生日期:", student.BirthDate.ToString("yyyy-MM-dd"));
                AddDetailRow(basicInfoGrid, row++, "专业:", student.Major);
                AddDetailRow(basicInfoGrid, row++, "班级:", student.ClassID); // 这里可能需要从Class表获取实际的班级名称
                AddDetailRow(basicInfoGrid, row++, "入学年份:", student.YearOfAdmission.ToString());
                AddDetailRow(basicInfoGrid, row++, "学籍状态:", student.Status);
                AddDetailRow(basicInfoGrid, row++, "联系电话:", student.Phone ?? "暂无");
                AddDetailRow(basicInfoGrid, row++, "电子邮箱:", student.Email ?? "暂无");
                AddDetailRow(basicInfoGrid, row++, "通讯地址:", student.Address ?? "暂无");

                dialogContent.Children.Add(basicInfoGrid);

                // 添加学习情况信息
                dialogContent.Children.Add(new TextBlock
                {
                    Text = "学习情况",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 20, 0, 10)
                });

                var academicGrid = new Grid();
                academicGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                academicGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                academicGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // 添加GPA信息
                var gpaText = student.GPA.HasValue ? student.GPA.Value.ToString("F2") : "暂无";
                AddDetailRow(academicGrid, 0, "平均绩点:", gpaText);

                dialogContent.Children.Add(academicGrid);

                // 添加分隔线
                dialogContent.Children.Add(new Separator
                {
                    Margin = new Thickness(0, 20, 0, 20)
                });

                // 添加按钮面板
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 16, 0, 0)
                };

                // 添加编辑按钮
                var editButton = new Button
                {
                    Content = "编辑",
                    Style = Application.Current.Resources["MaterialDesignOutlinedButton"] as Style,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                editButton.Click += async (s, e) =>
                {
                    CloseDetailDialog();
                    await EditStudentAsync(student);
                };

                // 添加确定按钮
                var okButton = new Button
                {
                    Content = "确定",
                    Style = Application.Current.Resources["MaterialDesignRaisedButton"] as Style
                };
                okButton.Click += (s, e) => CloseDetailDialog();

                buttonPanel.Children.Add(editButton);
                buttonPanel.Children.Add(okButton);
                dialogContent.Children.Add(buttonPanel);

                // 显示对话框
                var dialog = new MaterialDesignThemes.Wpf.DialogHost
                {
                    DialogContent = dialogContent,
                    IsOpen = true,
                    CloseOnClickAway = true
                };

                _currentDialog = dialog;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"显示学生详情时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task EditTeacherAsync(Teacher teacher)
        {
            if (teacher == null)
            {
                MessageBox.Show("请先选择要编辑的教师", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // 创建编辑对话框内容
                var dialogContent = new StackPanel { Margin = new Thickness(16), MinWidth = 400 };

                // 添加标题
                dialogContent.Children.Add(new TextBlock
                {
                    Text = "编辑教师信息",
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 20)
                });

                // 创建表单Grid
                var formGrid = new Grid();
                formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // 添加行定义
                for (int i = 0; i < 6; i++)
                {
                    formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                }

                // 工号（只读）
                var teacherIdTextBox = new TextBox
                {
                    Text = teacher.TeacherID,
                    Style = Application.Current.Resources["MaterialDesignOutlinedTextBox"] as Style,
                    Margin = new Thickness(0, 8, 0, 8),
                    IsReadOnly = true
                };
                AddFormField(formGrid, 0, "工号:", teacherIdTextBox);

                // 姓名
                var nameTextBox = new TextBox
                {
                    Text = teacher.Name,
                    Style = Application.Current.Resources["MaterialDesignOutlinedTextBox"] as Style,
                    Margin = new Thickness(0, 8, 0, 8)
                };
                AddFormField(formGrid, 1, "姓名:", nameTextBox);

                // 职称
                var titleComboBox = new ComboBox
                {
                    Style = Application.Current.Resources["MaterialDesignOutlinedComboBox"] as Style,
                    Margin = new Thickness(0, 8, 0, 8)
                };
                // 添加常见职称选项
                var titles = new[] { "教授", "副教授", "讲师", "助教" };
                foreach (var title in titles)
                {
                    titleComboBox.Items.Add(title);
                }
                titleComboBox.SelectedItem = teacher.Title;
                AddFormField(formGrid, 2, "职称:", titleComboBox);

                // 所属院系
                var departmentComboBox = new ComboBox
                {
                    Style = Application.Current.Resources["MaterialDesignOutlinedComboBox"] as Style,
                    Margin = new Thickness(0, 8, 0, 8),
                    ItemsSource = Departments,
                    DisplayMemberPath = "DepartmentName",
                    SelectedValuePath = "DepartmentID",
                    SelectedValue = teacher.DepartmentID
                };
                AddFormField(formGrid, 3, "所属院系:", departmentComboBox);

                // 联系电话
                var phoneTextBox = new TextBox
                {
                    Text = teacher.Phone,
                    Style = Application.Current.Resources["MaterialDesignOutlinedTextBox"] as Style,
                    Margin = new Thickness(0, 8, 0, 8)
                };
                AddFormField(formGrid, 4, "联系电话:", phoneTextBox);

                // 电子邮箱
                var emailTextBox = new TextBox
                {
                    Text = teacher.Email,
                    Style = Application.Current.Resources["MaterialDesignOutlinedTextBox"] as Style,
                    Margin = new Thickness(0, 8, 0, 8)
                };
                AddFormField(formGrid, 5, "电子邮箱:", emailTextBox);

                dialogContent.Children.Add(formGrid);

                // 添加验证信息显示
                var validationMessage = new TextBlock
                {
                    Foreground = Brushes.Red,
                    Margin = new Thickness(0, 8, 0, 0),
                    Visibility = Visibility.Collapsed
                };
                dialogContent.Children.Add(validationMessage);

                // 添加按钮面板
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 16, 0, 0)
                };

                var cancelButton = new Button
                {
                    Content = "取消",
                    Style = Application.Current.Resources["MaterialDesignOutlinedButton"] as Style,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                cancelButton.Click += (s, e) => CloseEditDialog();

                var saveButton = new Button
                {
                    Content = "保存",
                    Style = Application.Current.Resources["MaterialDesignRaisedButton"] as Style
                };
                saveButton.Click += async (s, e) =>
                {
                    // 验证输入
                    if (string.IsNullOrWhiteSpace(nameTextBox.Text))
                    {
                        validationMessage.Text = "请输入教师姓名";
                        validationMessage.Visibility = Visibility.Visible;
                        return;
                    }

                    if (titleComboBox.SelectedItem == null)
                    {
                        validationMessage.Text = "请选择教师职称";
                        validationMessage.Visibility = Visibility.Visible;
                        return;
                    }

                    if (departmentComboBox.SelectedItem == null)
                    {
                        validationMessage.Text = "请选择所属院系";
                        validationMessage.Visibility = Visibility.Visible;
                        return;
                    }

                    try
                    {
                        IsLoading = true;

                        // 更新教师信息
                        var updatedTeacher = new Teacher
                        {
                            TeacherID = teacher.TeacherID,
                            Name = nameTextBox.Text.Trim(),
                            Title = titleComboBox.SelectedItem.ToString(),
                            DepartmentID = departmentComboBox.SelectedValue.ToString(),
                            Phone = phoneTextBox.Text.Trim(),
                            Email = emailTextBox.Text.Trim()
                        };

                        // 调用服务更新教师信息
                        await _infoService.UpdateTeacherAsync(updatedTeacher);

                        // 更新列表中的教师信息
                        var index = Teachers.IndexOf(teacher);
                        if (index != -1)
                        {
                            Teachers[index] = updatedTeacher;
                        }

                        MessageBox.Show("教师信息已更新", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        CloseEditDialog();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"更新教师信息时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        IsLoading = false;
                    }
                };

                buttonPanel.Children.Add(cancelButton);
                buttonPanel.Children.Add(saveButton);
                dialogContent.Children.Add(buttonPanel);

                // 显示对话框
                var dialog = new MaterialDesignThemes.Wpf.DialogHost
                {
                    DialogContent = dialogContent,
                    IsOpen = true
                };

                _currentDialog = dialog;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开编辑对话框时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 添加表单字段的辅助方法
        private void AddFormField(Grid grid, int row, string label, FrameworkElement element)
        {
            var labelBlock = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 16, 0)
            };

            Grid.SetRow(labelBlock, row);
            Grid.SetColumn(labelBlock, 0);
            Grid.SetRow(element, row);
            Grid.SetColumn(element, 1);

            grid.Children.Add(labelBlock);
            grid.Children.Add(element);
        }

        // 关闭编辑对话框
        private void CloseEditDialog()
        {
            if (_currentDialog != null)
            {
                _currentDialog.IsOpen = false;
                _currentDialog = null;
            }
        }

        private async Task EditStudentAsync(Student student)
        {
            if (student == null)
            {
                MessageBox.Show("请先选择要编辑的学生", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // 创建编辑对话框内容
                var dialogContent = new StackPanel { Margin = new Thickness(16), MinWidth = 450 };

                // 添加标题
                dialogContent.Children.Add(new TextBlock
                {
                    Text = "编辑学生信息",
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 20)
                });

                // 创建表单Grid
                var formGrid = new Grid();
                formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // 添加行定义
                for (int i = 0; i < 11; i++)
                {
                    formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                }

                // 学号（只读）
                var studentIdTextBox = new TextBox
                {
                    Text = student.StudentID,
                    Style = Application.Current.Resources["MaterialDesignOutlinedTextBox"] as Style,
                    Margin = new Thickness(0, 8, 0, 8),
                    IsReadOnly = true
                };
                AddFormField(formGrid, 0, "学号:", studentIdTextBox);

                // 姓名
                var nameTextBox = new TextBox
                {
                    Text = student.Name,
                    Style = Application.Current.Resources["MaterialDesignOutlinedTextBox"] as Style,
                    Margin = new Thickness(0, 8, 0, 8)
                };
                AddFormField(formGrid, 1, "姓名:", nameTextBox);

                // 性别
                var genderComboBox = new ComboBox
                {
                    Style = Application.Current.Resources["MaterialDesignOutlinedComboBox"] as Style,
                    Margin = new Thickness(0, 8, 0, 8)
                };
                genderComboBox.Items.Add("男");
                genderComboBox.Items.Add("女");
                genderComboBox.SelectedItem = student.Gender;
                AddFormField(formGrid, 2, "性别:", genderComboBox);

                // 出生日期
                var birthDatePicker = new DatePicker
                {
                    Style = Application.Current.Resources["MaterialDesignOutlinedDatePicker"] as Style,
                    Margin = new Thickness(0, 8, 0, 8),
                    SelectedDate = student.BirthDate
                };
                AddFormField(formGrid, 3, "出生日期:", birthDatePicker);

                // 专业
                var majorComboBox = new ComboBox
                {
                    Style = Application.Current.Resources["MaterialDesignOutlinedComboBox"] as Style,
                    Margin = new Thickness(0, 8, 0, 8),
                    ItemsSource = Majors,
                    Text = student.Major
                };
                AddFormField(formGrid, 4, "专业:", majorComboBox);

                // 班级
                var classComboBox = new ComboBox
                {
                    Style = Application.Current.Resources["MaterialDesignOutlinedComboBox"] as Style,
                    Margin = new Thickness(0, 8, 0, 8),
                    ItemsSource = Classes,
                    DisplayMemberPath = "ClassName",
                    SelectedValuePath = "ClassID",
                    SelectedValue = student.ClassID
                };
                AddFormField(formGrid, 5, "班级:", classComboBox);

                // 入学年份
                var yearComboBox = new ComboBox
                {
                    Style = Application.Current.Resources["MaterialDesignOutlinedComboBox"] as Style,
                    Margin = new Thickness(0, 8, 0, 8)
                };
                var currentYear = DateTime.Now.Year;
                for (int i = currentYear - 4; i <= currentYear; i++)
                {
                    yearComboBox.Items.Add(i);
                }
                yearComboBox.SelectedItem = student.YearOfAdmission;
                AddFormField(formGrid, 6, "入学年份:", yearComboBox);

                // 学籍状态
                var statusComboBox = new ComboBox
                {
                    Style = Application.Current.Resources["MaterialDesignOutlinedComboBox"] as Style,
                    Margin = new Thickness(0, 8, 0, 8)
                };
                var statuses = new[] { "在读", "休学", "退学", "毕业" };
                foreach (var status in statuses)
                {
                    statusComboBox.Items.Add(status);
                }
                statusComboBox.SelectedItem = student.Status;
                AddFormField(formGrid, 7, "学籍状态:", statusComboBox);

                // 联系电话
                var phoneTextBox = new TextBox
                {
                    Text = student.Phone,
                    Style = Application.Current.Resources["MaterialDesignOutlinedTextBox"] as Style,
                    Margin = new Thickness(0, 8, 0, 8)
                };
                AddFormField(formGrid, 8, "联系电话:", phoneTextBox);

                // 电子邮箱
                var emailTextBox = new TextBox
                {
                    Text = student.Email,
                    Style = Application.Current.Resources["MaterialDesignOutlinedTextBox"] as Style,
                    Margin = new Thickness(0, 8, 0, 8)
                };
                AddFormField(formGrid, 9, "电子邮箱:", emailTextBox);

                // 通讯地址
                var addressTextBox = new TextBox
                {
                    Text = student.Address,
                    Style = Application.Current.Resources["MaterialDesignOutlinedTextBox"] as Style,
                    Margin = new Thickness(0, 8, 0, 8)
                };
                AddFormField(formGrid, 10, "通讯地址:", addressTextBox);

                dialogContent.Children.Add(formGrid);

                // 添加验证信息显示
                var validationMessage = new TextBlock
                {
                    Foreground = Brushes.Red,
                    Margin = new Thickness(0, 8, 0, 0),
                    Visibility = Visibility.Collapsed
                };
                dialogContent.Children.Add(validationMessage);

                // 添加按钮面板
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 16, 0, 0)
                };

                var cancelButton = new Button
                {
                    Content = "取消",
                    Style = Application.Current.Resources["MaterialDesignOutlinedButton"] as Style,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                cancelButton.Click += (s, e) => CloseEditDialog();

                var saveButton = new Button
                {
                    Content = "保存",
                    Style = Application.Current.Resources["MaterialDesignRaisedButton"] as Style
                };
                saveButton.Click += async (s, e) =>
                {
                    // 验证输入
                    if (string.IsNullOrWhiteSpace(nameTextBox.Text))
                    {
                        validationMessage.Text = "请输入学生姓名";
                        validationMessage.Visibility = Visibility.Visible;
                        return;
                    }

                    if (genderComboBox.SelectedItem == null)
                    {
                        validationMessage.Text = "请选择性别";
                        validationMessage.Visibility = Visibility.Visible;
                        return;
                    }

                    if (birthDatePicker.SelectedDate == null)
                    {
                        validationMessage.Text = "请选择出生日期";
                        validationMessage.Visibility = Visibility.Visible;
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(majorComboBox.Text))
                    {
                        validationMessage.Text = "请选择专业";
                        validationMessage.Visibility = Visibility.Visible;
                        return;
                    }

                    if (classComboBox.SelectedValue == null)
                    {
                        validationMessage.Text = "请选择班级";
                        validationMessage.Visibility = Visibility.Visible;
                        return;
                    }

                    try
                    {
                        IsLoading = true;

                        // 更新学生信息
                        var updatedStudent = new Student
                        {
                            StudentID = student.StudentID,
                            Name = nameTextBox.Text.Trim(),
                            Gender = genderComboBox.SelectedItem.ToString(),
                            BirthDate = birthDatePicker.SelectedDate.Value,
                            Major = majorComboBox.Text.Trim(),
                            ClassID = classComboBox.SelectedValue.ToString(),
                            YearOfAdmission = (int)yearComboBox.SelectedItem,
                            Status = statusComboBox.SelectedItem.ToString(),
                            Phone = phoneTextBox.Text.Trim(),
                            Email = emailTextBox.Text.Trim(),
                            Address = addressTextBox.Text.Trim()
                        };

                        // 调用服务更新学生信息
                        await _infoService.UpdateStudentAsync(updatedStudent);

                        // 更新列表中的学生信息
                        var index = Students.IndexOf(student);
                        if (index != -1)
                        {
                            Students[index] = updatedStudent;
                        }

                        MessageBox.Show("学生信息已更新", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        CloseEditDialog();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"更新学生信息时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        IsLoading = false;
                    }
                };

                buttonPanel.Children.Add(cancelButton);
                buttonPanel.Children.Add(saveButton);
                dialogContent.Children.Add(buttonPanel);

                // 显示对话框
                var dialog = new MaterialDesignThemes.Wpf.DialogHost
                {
                    DialogContent = dialogContent,
                    IsOpen = true
                };

                _currentDialog = dialog;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开编辑对话框时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task QueryClassListAsync()
        {
            if (SelectedDepartment == null)
            {
                MessageBox.Show("请先选择院系", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (SelectedClass == null)
            {
                MessageBox.Show("请选择班级", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                IsLoading = true;

                // 清空当前列表
                ClassStudents.Clear();

                // 获取班级学生列表
                var students = await _infoService.GetClassStudentsAsync(SelectedClass.ClassID);

                // 更新学生列表
                foreach (var student in students)
                {
                    ClassStudents.Add(student);
                }

                // 更新统计信息
                TotalStudents = ClassStudents.Count;

                // 按性别统计
                var maleCount = ClassStudents.Count(s => s.Gender == "男");
                var femaleCount = ClassStudents.Count(s => s.Gender == "女");

                // 按学籍状态统计
                var activeCount = ClassStudents.Count(s => s.Status == "在读");
                var suspendedCount = ClassStudents.Count(s => s.Status == "休学");
                var withdrawnCount = ClassStudents.Count(s => s.Status == "退学");
                var graduatedCount = ClassStudents.Count(s => s.Status == "毕业");

                // 显示统计信息对话框
                var statisticsContent = new StackPanel { Margin = new Thickness(16) };

                // 添加标题
                statisticsContent.Children.Add(new TextBlock
                {
                    Text = $"{SelectedClass.ClassName}班级统计信息",
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 20)
                });

                // 创建统计信息网格
                var statsGrid = new Grid();
                statsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                statsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // 添加统计行
                int row = 0;
                AddStatRow(statsGrid, row++, "总人数:", $"{TotalStudents}人");
                AddStatRow(statsGrid, row++, "男生人数:", $"{maleCount}人");
                AddStatRow(statsGrid, row++, "女生人数:", $"{femaleCount}人");
                AddStatRow(statsGrid, row++, "在读人数:", $"{activeCount}人");
                AddStatRow(statsGrid, row++, "休学人数:", $"{suspendedCount}人");
                AddStatRow(statsGrid, row++, "退学人数:", $"{withdrawnCount}人");
                AddStatRow(statsGrid, row++, "毕业人数:", $"{graduatedCount}人");

                statisticsContent.Children.Add(statsGrid);

                // 添加导出和打印按钮
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 20, 0, 0)
                };

                var exportButton = new Button
                {
                    Content = "导出名单",
                    Style = Application.Current.Resources["MaterialDesignOutlinedButton"] as Style,
                    Margin = new Thickness(0, 0, 8, 0),
                    Command = ExportStudentListCommand
                };

                var printButton = new Button
                {
                    Content = "打印名单",
                    Style = Application.Current.Resources["MaterialDesignOutlinedButton"] as Style,
                    Margin = new Thickness(0, 0, 8, 0),
                    Command = PrintStudentListCommand
                };

                var closeButton = new Button
                {
                    Content = "关闭",
                    Style = Application.Current.Resources["MaterialDesignRaisedButton"] as Style
                };
                closeButton.Click += (s, e) => CloseStatisticsDialog();

                buttonPanel.Children.Add(exportButton);
                buttonPanel.Children.Add(printButton);
                buttonPanel.Children.Add(closeButton);
                statisticsContent.Children.Add(buttonPanel);

                // 显示统计对话框
                var dialog = new MaterialDesignThemes.Wpf.DialogHost
                {
                    DialogContent = statisticsContent,
                    IsOpen = true
                };

                _statisticsDialog = dialog;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"查询班级名单时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // 添加统计行的辅助方法
        private void AddStatRow(Grid grid, int row, string label, string value)
        {
            var labelBlock = new TextBlock
            {
                Text = label,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 5, 16, 5)
            };

            var valueBlock = new TextBlock
            {
                Text = value,
                Margin = new Thickness(0, 5, 0, 5)
            };

            Grid.SetRow(labelBlock, row);
            Grid.SetColumn(labelBlock, 0);
            Grid.SetRow(valueBlock, row);
            Grid.SetColumn(valueBlock, 1);

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.Children.Add(labelBlock);
            grid.Children.Add(valueBlock);
        }

        // 关闭统计对话框
        private void CloseStatisticsDialog()
        {
            if (_statisticsDialog != null)
            {
                _statisticsDialog.IsOpen = false;
                _statisticsDialog = null;
            }
        }

        // 添加字段用于跟踪统计对话框
        private MaterialDesignThemes.Wpf.DialogHost _statisticsDialog;

        private async Task AddSemesterAsync()
        {
            try
            {
                // 创建添加学期对话框内容
                var dialogContent = new StackPanel { Margin = new Thickness(16), MinWidth = 400 };

                // 添加标题
                dialogContent.Children.Add(new TextBlock
                {
                    Text = "添加新学期",
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 20)
                });

                // 创建表单Grid
                var formGrid = new Grid();
                formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // 添加行定义
                for (int i = 0; i < 4; i++)
                {
                    formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                }

                // 学年选择
                var yearComboBox = new ComboBox
                {
                    Style = Application.Current.Resources["MaterialDesignOutlinedComboBox"] as Style,
                    Margin = new Thickness(0, 8, 0, 8)
                };

                // 生成学年选项（当前年份及后两年）
                var currentYear = DateTime.Now.Year;
                for (int i = 0; i < 3; i++)
                {
                    var year = currentYear + i;
                    yearComboBox.Items.Add($"{year}-{year + 1}学年");
                }
                AddFormField(formGrid, 0, "学年:", yearComboBox);

                // 学期选择
                var semesterComboBox = new ComboBox
                {
                    Style = Application.Current.Resources["MaterialDesignOutlinedComboBox"] as Style,
                    Margin = new Thickness(0, 8, 0, 8)
                };
                semesterComboBox.Items.Add("第一学期");
                semesterComboBox.Items.Add("第二学期");
                AddFormField(formGrid, 1, "学期:", semesterComboBox);

                // 开始日期
                var startDatePicker = new DatePicker
                {
                    Style = Application.Current.Resources["MaterialDesignOutlinedDatePicker"] as Style,
                    Margin = new Thickness(0, 8, 0, 8)
                };
                AddFormField(formGrid, 2, "开始日期:", startDatePicker);

                // 结束日期
                var endDatePicker = new DatePicker
                {
                    Style = Application.Current.Resources["MaterialDesignOutlinedDatePicker"] as Style,
                    Margin = new Thickness(0, 8, 0, 8)
                };
                AddFormField(formGrid, 3, "结束日期:", endDatePicker);

                dialogContent.Children.Add(formGrid);

                // 添加验证信息显示
                var validationMessage = new TextBlock
                {
                    Foreground = Brushes.Red,
                    Margin = new Thickness(0, 8, 0, 0),
                    Visibility = Visibility.Collapsed
                };
                dialogContent.Children.Add(validationMessage);

                // 添加按钮面板
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 16, 0, 0)
                };

                var cancelButton = new Button
                {
                    Content = "取消",
                    Style = Application.Current.Resources["MaterialDesignOutlinedButton"] as Style,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                cancelButton.Click += (s, e) => CloseDialog();

                var saveButton = new Button
                {
                    Content = "保存",
                    Style = Application.Current.Resources["MaterialDesignRaisedButton"] as Style
                };
                saveButton.Click += async (s, e) =>
                {
                    // 验证输入
                    if (yearComboBox.SelectedItem == null)
                    {
                        validationMessage.Text = "请选择学年";
                        validationMessage.Visibility = Visibility.Visible;
                        return;
                    }

                    if (semesterComboBox.SelectedItem == null)
                    {
                        validationMessage.Text = "请选择学期";
                        validationMessage.Visibility = Visibility.Visible;
                        return;
                    }

                    if (startDatePicker.SelectedDate == null)
                    {
                        validationMessage.Text = "请选择开始日期";
                        validationMessage.Visibility = Visibility.Visible;
                        return;
                    }

                    if (endDatePicker.SelectedDate == null)
                    {
                        validationMessage.Text = "请选择结束日期";
                        validationMessage.Visibility = Visibility.Visible;
                        return;
                    }

                    if (startDatePicker.SelectedDate >= endDatePicker.SelectedDate)
                    {
                        validationMessage.Text = "结束日期必须晚于开始日期";
                        validationMessage.Visibility = Visibility.Visible;
                        return;
                    }

                    try
                    {
                        IsLoading = true;

                        // 从选择的学年中提取年份
                        var yearRange = yearComboBox.SelectedItem.ToString();
                        var yearStr = yearRange.Split('-')[0];
                        var academicYear = int.Parse(yearStr);

                        // 创建新学期
                        var newSemester = new Semester
                        {
                            SemesterName = semesterComboBox.SelectedItem.ToString(),
                            AcademicYearID = academicYear,
                            StartDate = startDatePicker.SelectedDate.Value,
                            EndDate = endDatePicker.SelectedDate.Value
                        };

                        // 检查是否存在时间重叠的学期
                        var hasOverlap = await _infoService.CheckSemesterOverlapAsync(
                            newSemester.StartDate,
                            newSemester.EndDate);

                        if (hasOverlap)
                        {
                            MessageBox.Show("该时间段与已有学期重叠，请重新选择时间", "提示",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        // 保存新学期并获取ID
                        var newSemesterId = await _infoService.AddSemesterAsync(newSemester);

                        // 设置返回的ID
                        newSemester.SemesterID = newSemesterId;

                        // 添加到集合
                        Semesters.Add(newSemester);

                        MessageBox.Show("学期添加成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        CloseDialog();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"添加学期时发生错误: {ex.Message}", "错误",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        IsLoading = false;
                    }
                };

                buttonPanel.Children.Add(cancelButton);
                buttonPanel.Children.Add(saveButton);
                dialogContent.Children.Add(buttonPanel);

                // 显示对话框
                var dialog = new MaterialDesignThemes.Wpf.DialogHost
                {
                    DialogContent = dialogContent,
                    IsOpen = true
                };

                _currentDialog = dialog;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开添加学期对话框时发生错误: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 关闭对话框方法
        private void CloseDialog()
        {
            if (_currentDialog != null)
            {
                _currentDialog.IsOpen = false;
                _currentDialog = null;
            }
        }

        private async Task EditSemesterAsync(Semester semester)
        {
            if (semester == null)
            {
                MessageBox.Show("请先选择要编辑的学期", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // 创建编辑学期对话框内容
                var dialogContent = new StackPanel { Margin = new Thickness(16), MinWidth = 400 };

                // 添加标题
                dialogContent.Children.Add(new TextBlock
                {
                    Text = "编辑学期信息",
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 20)
                });

                // 创建表单Grid
                var formGrid = new Grid();
                formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // 添加行定义
                for (int i = 0; i < 4; i++)
                {
                    formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                }

                // 学年选择
                var yearComboBox = new ComboBox
                {
                    Style = Application.Current.Resources["MaterialDesignOutlinedComboBox"] as Style,
                    Margin = new Thickness(0, 8, 0, 8)
                };

                // 生成学年选项（前一年到后两年）
                var currentYear = semester.AcademicYearID;
                for (int i = -1; i < 3; i++)
                {
                    var year = currentYear + i;
                    yearComboBox.Items.Add($"{year}-{year + 1}学年");
                }
                yearComboBox.SelectedItem = $"{semester.AcademicYearID}-{semester.AcademicYearID + 1}学年";
                AddFormField(formGrid, 0, "学年:", yearComboBox);

                // 学期选择
                var semesterComboBox = new ComboBox
                {
                    Style = Application.Current.Resources["MaterialDesignOutlinedComboBox"] as Style,
                    Margin = new Thickness(0, 8, 0, 8)
                };
                semesterComboBox.Items.Add("第一学期");
                semesterComboBox.Items.Add("第二学期");
                semesterComboBox.SelectedItem = semester.SemesterName;
                AddFormField(formGrid, 1, "学期:", semesterComboBox);

                // 开始日期
                var startDatePicker = new DatePicker
                {
                    Style = Application.Current.Resources["MaterialDesignOutlinedDatePicker"] as Style,
                    Margin = new Thickness(0, 8, 0, 8),
                    SelectedDate = semester.StartDate
                };
                AddFormField(formGrid, 2, "开始日期:", startDatePicker);

                // 结束日期
                var endDatePicker = new DatePicker
                {
                    Style = Application.Current.Resources["MaterialDesignOutlinedDatePicker"] as Style,
                    Margin = new Thickness(0, 8, 0, 8),
                    SelectedDate = semester.EndDate
                };
                AddFormField(formGrid, 3, "结束日期:", endDatePicker);

                dialogContent.Children.Add(formGrid);

                // 添加验证信息显示
                var validationMessage = new TextBlock
                {
                    Foreground = Brushes.Red,
                    Margin = new Thickness(0, 8, 0, 0),
                    Visibility = Visibility.Collapsed
                };
                dialogContent.Children.Add(validationMessage);

                // 添加按钮面板
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 16, 0, 0)
                };

                var cancelButton = new Button
                {
                    Content = "取消",
                    Style = Application.Current.Resources["MaterialDesignOutlinedButton"] as Style,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                cancelButton.Click += (s, e) => CloseDialog();

                var saveButton = new Button
                {
                    Content = "保存",
                    Style = Application.Current.Resources["MaterialDesignRaisedButton"] as Style
                };
                saveButton.Click += async (s, e) =>
                {
                    // 验证输入
                    if (yearComboBox.SelectedItem == null)
                    {
                        validationMessage.Text = "请选择学年";
                        validationMessage.Visibility = Visibility.Visible;
                        return;
                    }

                    if (semesterComboBox.SelectedItem == null)
                    {
                        validationMessage.Text = "请选择学期";
                        validationMessage.Visibility = Visibility.Visible;
                        return;
                    }

                    if (startDatePicker.SelectedDate == null)
                    {
                        validationMessage.Text = "请选择开始日期";
                        validationMessage.Visibility = Visibility.Visible;
                        return;
                    }

                    if (endDatePicker.SelectedDate == null)
                    {
                        validationMessage.Text = "请选择结束日期";
                        validationMessage.Visibility = Visibility.Visible;
                        return;
                    }

                    if (startDatePicker.SelectedDate >= endDatePicker.SelectedDate)
                    {
                        validationMessage.Text = "结束日期必须晚于开始日期";
                        validationMessage.Visibility = Visibility.Visible;
                        return;
                    }

                    try
                    {
                        IsLoading = true;

                        // 从选择的学年中提取年份
                        var yearRange = yearComboBox.SelectedItem.ToString();
                        var yearStr = yearRange.Split('-')[0];
                        var academicYear = int.Parse(yearStr);

                        // 更新学期信息
                        var updatedSemester = new Semester
                        {
                            SemesterID = semester.SemesterID,
                            SemesterName = semesterComboBox.SelectedItem.ToString(),
                            AcademicYearID = academicYear,
                            StartDate = startDatePicker.SelectedDate.Value,
                            EndDate = endDatePicker.SelectedDate.Value
                        };

                        // 检查是否与其他学期时间重叠（排除当前学期）
                        var hasOverlap = await _infoService.CheckSemesterOverlapExceptCurrentAsync(
                            updatedSemester.SemesterID,
                            updatedSemester.StartDate,
                            updatedSemester.EndDate);

                        if (hasOverlap)
                        {
                            MessageBox.Show("该时间段与其他学期重叠，请重新选择时间", "提示",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        // 保存更新
                        var success = await _infoService.UpdateSemesterAsync(updatedSemester);
                        if (success)
                        {
                            // 更新列表中的学期信息
                            var index = Semesters.IndexOf(semester);
                            if (index != -1)
                            {
                                Semesters[index] = updatedSemester;
                            }

                            MessageBox.Show("学期信息已更新", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                            CloseDialog();
                        }
                        else
                        {
                            MessageBox.Show("更新学期信息失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"更新学期信息时发生错误: {ex.Message}", "错误",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        IsLoading = false;
                    }
                };

                buttonPanel.Children.Add(cancelButton);
                buttonPanel.Children.Add(saveButton);
                dialogContent.Children.Add(buttonPanel);

                // 显示对话框
                var dialog = new MaterialDesignThemes.Wpf.DialogHost
                {
                    DialogContent = dialogContent,
                    IsOpen = true
                };

                _currentDialog = dialog;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开编辑对话框时发生错误: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DeleteSemesterAsync(Semester semester)
        {
            if (semester == null)
            {
                MessageBox.Show("请先选择要删除的学期", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // 确认删除
                var result = MessageBox.Show(
                    $"确定要删除{semester.AcademicYearID}-{semester.AcademicYearID + 1}学年 {semester.SemesterName}吗？\n删除后数据将无法恢复。",
                    "删除确认",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No); // 默认选择"否"

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }

                IsLoading = true;

                // 检查是否有关联的课程数据
                var hasRelatedData = await _infoService.CheckSemesterHasRelatedDataAsync(semester.SemesterID);
                if (hasRelatedData)
                {
                    MessageBox.Show(
                        "此学期已有关联的课程数据，无法删除。\n如需删除，请先删除相关的课程安排。",
                        "无法删除",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // 执行删除操作
                var success = await _infoService.DeleteSemesterAsync(semester.SemesterID);
                if (success)
                {
                    // 从列表中移除
                    Semesters.Remove(semester);
                    MessageBox.Show("学期已成功删除", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("删除学期失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除学期时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SaveSemesterAsync()
        {
            try
            {
                // 输入验证
                if (string.IsNullOrWhiteSpace(NewSemester.SemesterName))
                {
                    MessageBox.Show("请输入学期名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (NewSemester.AcademicYearID <= 0)
                {
                    MessageBox.Show("请选择正确的学年", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (NewSemester.StartDate >= NewSemester.EndDate)
                {
                    MessageBox.Show("结束日期必须晚于开始日期", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                IsLoading = true;

                // 检查时间重叠
                bool hasOverlap;
                if (NewSemester.SemesterID > 0)
                {
                    // 编辑模式：检查与其他学期的重叠（排除当前学期）
                    hasOverlap = await _infoService.CheckSemesterOverlapExceptCurrentAsync(
                        NewSemester.SemesterID,
                        NewSemester.StartDate,
                        NewSemester.EndDate);
                }
                else
                {
                    // 新增模式：检查与所有学期的重叠
                    hasOverlap = await _infoService.CheckSemesterOverlapAsync(
                        NewSemester.StartDate,
                        NewSemester.EndDate);
                }

                if (hasOverlap)
                {
                    MessageBox.Show("该时间段与其他学期重叠，请重新选择时间", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (NewSemester.SemesterID > 0)
                {
                    // 更新现有学期
                    var success = await _infoService.UpdateSemesterAsync(NewSemester);
                    if (success)
                    {
                        // 更新列表中的学期信息
                        var index = Semesters.IndexOf(CurrentSemester);
                        if (index != -1)
                        {
                            Semesters[index] = NewSemester;
                        }
                        MessageBox.Show("学期信息已更新", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("更新学期信息失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                else
                {
                    // 添加新学期
                    var newSemesterId = await _infoService.AddSemesterAsync(NewSemester);
                    if (newSemesterId > 0)
                    {
                        NewSemester.SemesterID = newSemesterId;
                        Semesters.Add(NewSemester);
                        MessageBox.Show("新学期已添加", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("添加学期失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // 清空输入并关闭对话框
                NewSemester = new Semester();
                IsSemesterDialogOpen = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存学期信息时发生错误: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ExportStudentList()
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                    FilterIndex = 1,
                    DefaultExt = "xlsx",
                    FileName = $"学生名单_{DateTime.Now:yyyyMMdd}"
                };

                if (saveFileDialog.ShowDialog() != true)
                {
                    return;
                }

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("学生名单");

                    // 添加表头
                    worksheet.Cell(1, 1).Value = "学号";
                    worksheet.Cell(1, 2).Value = "姓名";
                    worksheet.Cell(1, 3).Value = "性别";
                    worksheet.Cell(1, 4).Value = "班级编号";
                    worksheet.Cell(1, 5).Value = "专业";
                    worksheet.Cell(1, 6).Value = "入学年份";
                    worksheet.Cell(1, 7).Value = "联系电话";
                    worksheet.Cell(1, 8).Value = "邮箱";
                    worksheet.Cell(1, 9).Value = "状态";

                    // 添加数据
                    int row = 2;
                    foreach (var student in Students)
                    {
                        worksheet.Cell(row, 1).Value = student.StudentID;
                        worksheet.Cell(row, 2).Value = student.Name;
                        worksheet.Cell(row, 3).Value = student.Gender;
                        worksheet.Cell(row, 4).Value = student.ClassID;  // 改用 ClassID
                        worksheet.Cell(row, 5).Value = student.Major;
                        worksheet.Cell(row, 6).Value = student.YearOfAdmission;
                        worksheet.Cell(row, 7).Value = student.Phone;
                        worksheet.Cell(row, 8).Value = student.Email;
                        worksheet.Cell(row, 9).Value = student.Status;
                        row++;
                    }

                    // 设置表格样式
                    var headerRange = worksheet.Range(1, 1, 1, 9);
                    headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                    headerRange.Style.Font.Bold = true;

                    // 自动调整列宽
                    worksheet.Columns().AdjustToContents();

                    // 保存文件
                    workbook.SaveAs(saveFileDialog.FileName);

                    MessageBox.Show("学生名单导出成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrintStudentList()
        {
            try
            {
                PrintDialog printDialog = new PrintDialog();
                if (printDialog.ShowDialog() != true)
                {
                    return;
                }

                // 创建打印文档
                System.Windows.Documents.FlowDocument document = new System.Windows.Documents.FlowDocument();
                document.PagePadding = new Thickness(50);

                // 添加标题
                System.Windows.Documents.Paragraph title = new System.Windows.Documents.Paragraph(
                    new System.Windows.Documents.Run("学生名单"));
                title.TextAlignment = System.Windows.TextAlignment.Center;
                title.FontSize = 20;
                title.FontWeight = FontWeights.Bold;
                title.Margin = new Thickness(0, 0, 0, 20);
                document.Blocks.Add(title);

                // 创建表格
                System.Windows.Documents.Table table = new System.Windows.Documents.Table();
                table.CellSpacing = 0;
                table.BorderBrush = Brushes.Black;
                table.BorderThickness = new Thickness(1);

                // 添加列
                for (int i = 0; i < 9; i++)
                {
                    table.Columns.Add(new System.Windows.Documents.TableColumn());
                }

                // 添加表头
                System.Windows.Documents.TableRow headerRow = new System.Windows.Documents.TableRow();
                headerRow.Background = Brushes.LightGray;
                headerRow.Cells.Add(CreateTableCell("学号"));
                headerRow.Cells.Add(CreateTableCell("姓名"));
                headerRow.Cells.Add(CreateTableCell("性别"));
                headerRow.Cells.Add(CreateTableCell("班级编号"));
                headerRow.Cells.Add(CreateTableCell("专业"));
                headerRow.Cells.Add(CreateTableCell("入学年份"));
                headerRow.Cells.Add(CreateTableCell("联系电话"));
                headerRow.Cells.Add(CreateTableCell("邮箱"));
                headerRow.Cells.Add(CreateTableCell("状态"));
                table.RowGroups.Add(new System.Windows.Documents.TableRowGroup());
                table.RowGroups[0].Rows.Add(headerRow);

                // 添加数据行
                foreach (var student in Students)
                {
                    System.Windows.Documents.TableRow dataRow = new System.Windows.Documents.TableRow();
                    dataRow.Cells.Add(CreateTableCell(student.StudentID));
                    dataRow.Cells.Add(CreateTableCell(student.Name));
                    dataRow.Cells.Add(CreateTableCell(student.Gender));
                    dataRow.Cells.Add(CreateTableCell(student.ClassID));
                    dataRow.Cells.Add(CreateTableCell(student.Major));
                    dataRow.Cells.Add(CreateTableCell(student.YearOfAdmission));
                    dataRow.Cells.Add(CreateTableCell(student.Phone));
                    dataRow.Cells.Add(CreateTableCell(student.Email));
                    dataRow.Cells.Add(CreateTableCell(student.Status));
                    table.RowGroups[0].Rows.Add(dataRow);
                }

                document.Blocks.Add(table);

                // 添加打印时间
                System.Windows.Documents.Paragraph footer = new System.Windows.Documents.Paragraph(
                    new System.Windows.Documents.Run($"打印时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}"));
                footer.TextAlignment = System.Windows.TextAlignment.Right;
                footer.FontSize = 10;
                footer.Margin = new Thickness(0, 20, 0, 0);
                document.Blocks.Add(footer);

                // 创建 DocumentPaginator
                IDocumentPaginatorSource paginatorSource = document;
                printDialog.PrintDocument(paginatorSource.DocumentPaginator, "学生名单");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打印失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 辅助方法：创建表格单元格
        private System.Windows.Documents.TableCell CreateTableCell(object content)
        {
            System.Windows.Documents.TableCell cell = new System.Windows.Documents.TableCell(
                new System.Windows.Documents.Paragraph(
                    new System.Windows.Documents.Run(content?.ToString() ?? "")));
            cell.BorderBrush = Brushes.Black;
            cell.BorderThickness = new Thickness(1);
            cell.Padding = new Thickness(5);
            return cell;
        }

        // 先添加 SearchText 属性
        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged(nameof(SearchText));
                }
            }
        }

        private async Task QuerySemesterAsync()
        {
            try
            {
                IsLoading = true;
                OnPropertyChanged(nameof(IsLoading));

                if (string.IsNullOrWhiteSpace(SearchText))
                {
                    // 获取所有学期
                    var allSemesters = await _infoService.GetSemestersAsync();
                    Semesters = new ObservableCollection<Semester>(allSemesters);
                    OnPropertyChanged(nameof(Semesters));
                }
                else
                {
                    // 根据搜索条件筛选学期
                    var searchTerm = SearchText.Trim().ToLower();
                    var filteredSemesters = await _infoService.GetSemestersBySearchTermAsync(searchTerm);
                    Semesters = new ObservableCollection<Semester>(filteredSemesters);
                    OnPropertyChanged(nameof(Semesters));
                }

                // 更新当前学期信息（假设为最近的一个学期）
                CurrentSemester = Semesters
                    .OrderByDescending(s => s.EndDate)
                    .FirstOrDefault(s => s.EndDate >= DateTime.Today) ?? Semesters.LastOrDefault();

                if (CurrentSemester != null)
                {
                    OnPropertyChanged(nameof(CurrentSemester));
                }

                // 如果没有找到任何学期数据
                if (!Semesters.Any())
                {
                    MessageBox.Show("未找到符合条件的学期信息。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"查询学期信息失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(IsLoading));
            }
        }

        private async Task SetCurrentSemesterAsync(Semester semester)
        {
            try
            {
                if (semester == null)
                {
                    MessageBox.Show("请选择要设置的学期。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                IsLoading = true;
                OnPropertyChanged(nameof(IsLoading));

                // 更新当前学期
                CurrentSemester = semester;
                OnPropertyChanged(nameof(CurrentSemester));

                // 刷新与当前学期相关的数据
                await RefreshSemesterRelatedDataAsync(semester);

                MessageBox.Show($"已成功设置当前学期为：{semester.SemesterName}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"设置当前学期失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(IsLoading));
            }
        }

        // 辅助方法：刷新与学期相关的数据
        private async Task RefreshSemesterRelatedDataAsync(Semester semester)
        {
            try
            {
                // 获取该学期的教师课程安排
                var teacherCourseList = await _infoService.GetTeacherCoursesBySemesterAsync(semester.SemesterID);
                TeacherCourseList = new ObservableCollection<TeacherCourse>(teacherCourseList);
                OnPropertyChanged(nameof(TeacherCourseList));

                // 获取所有相关课程信息
                var courses = await _infoService.GetCoursesBySemesterAsync(semester.SemesterID);
                CourseList = new ObservableCollection<Course>(courses);
                OnPropertyChanged(nameof(CourseList));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"刷新学期相关数据时出错：{ex.Message}");
            }
        }

        #endregion

        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}