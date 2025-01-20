using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using UniAcamanageWpfApp.Models;
using UniAcamanageWpfApp.Services;
using UniAcamanageWpfApp.ViewModels;

namespace UniAcamanageWpfApp.Views
{
    public partial class InfoManagementView : UserControl
    {
        private readonly IInfoManagementService _infoService;
        private ObservableCollection<Student> Students { get; set; }
        private ObservableCollection<Department> Departments { get; set; }
        private ObservableCollection<Semester> Semesters { get; set; }
        private int? _selectedSemesterId;

        public InfoManagementView()
        {
            InitializeComponent();
            DataContext = new InfoManagementViewModel(new InfoManagementService());
            _infoService = new InfoManagementService();
            InitializeCollections();
            LoadInitialData();
        }

        private void InitializeCollections()
        {
            Students = new ObservableCollection<Student>();
            Departments = new ObservableCollection<Department>();
            Semesters = new ObservableCollection<Semester>();

            StudentDataGrid.ItemsSource = Students;
            ClassStudentDataGrid.ItemsSource = Students;
            SemesterDataGrid.ItemsSource = Semesters;
        }

        private async void LoadInitialData()
        {
            try
            {
                LoadingIndicator.Visibility = Visibility.Visible;

                // 加载院系数据
                var departments = await _infoService.GetDepartmentsAsync();
                Departments.Clear();
                foreach (var dept in departments)
                {
                    Departments.Add(dept);
                }

                // 初始化下拉框
                InitializeComboBoxes();

                // 加载学生数据
                await LoadStudents();

                // 加载学期数据
                await LoadSemesters();
            }
            catch (Exception ex)
            {
                ShowError("加载数据失败", ex);
            }
            finally
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
            }
        }

        private void InitializeComboBoxes()
        {
            // 初始化院系下拉框
            DepartmentFilterComboBox.ItemsSource = Departments;
            DepartmentFilterComboBox.DisplayMemberPath = "DepartmentName";
            DepartmentComboBox.ItemsSource = Departments;
            DepartmentComboBox.DisplayMemberPath = "DepartmentName";

            // 初始化年级下拉框
            var currentYear = DateTime.Now.Year;
            var years = Enumerable.Range(currentYear - 4, 5).ToList();
            YearComboBox.ItemsSource = years;

            // 初始化学年范围下拉框
            var yearRanges = new List<string>();
            for (int i = 0; i < 5; i++)
            {
                yearRanges.Add($"{currentYear - i}-{currentYear - i + 1}学年");
            }
            YearRangeComboBox.ItemsSource = yearRanges;
            DialogYearComboBox.ItemsSource = yearRanges;
        }

        private async Task LoadStudents(string searchText = null, string major = null, string classId = null)
        {
            try
            {
                var students = await _infoService.SearchStudentsAsync(searchText, major, classId);
                Students.Clear();
                foreach (var student in students)
                {
                    Students.Add(student);
                }
            }
            catch (Exception ex)
            {
                ShowError("加载学生数据失败", ex);
            }
        }

        private async Task LoadSemesters()
        {
            try
            {
                var semesters = await _infoService.GetSemestersAsync();
                Semesters.Clear();
                foreach (var semester in semesters)
                {
                    Semesters.Add(semester);
                }
            }
            catch (Exception ex)
            {
                ShowError("加载学期数据失败", ex);
            }
        }

        // 学生信息管理相关方法
        private async void SearchStudent_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var searchText = StudentSearchBox.Text;
                var selectedMajor = MajorFilterComboBox.SelectedItem?.ToString();
                var selectedClass = ClassFilterComboBox.SelectedItem?.ToString();

                await LoadStudents(searchText, selectedMajor, selectedClass);
            }
            catch (Exception ex)
            {
                ShowError("搜索学生失败", ex);
            }
        }

        private async void EditStudent_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var student = button.DataContext as Student;
            if (student != null)
            {
                await ShowEditStudentDialog(student);
            }
        }

        private async Task ShowEditStudentDialog(Student student)
        {
            try
            {
                EditDialogContent.Children.Clear();

                // 创建编辑表单
                var form = new StackPanel();

                // 学号（只读）
                var studentIdBox = CreateTextBox("学号", student.StudentID, true);
                form.Children.Add(studentIdBox);

                // 姓名
                var nameBox = CreateTextBox("姓名", student.Name);
                form.Children.Add(nameBox);

                // 性别
                var genderCombo = new ComboBox
                {
                    Style = FindResource("MaterialDesignOutlinedComboBox") as Style,
                    Margin = new Thickness(0, 0, 0, 16),
                    ItemsSource = new[] { "男", "女" },
                    SelectedItem = student.Gender
                };
                form.Children.Add(genderCombo);

                // 其他字段...

                EditDialogContent.Children.Add(form);
                EditDialog.DialogContent = form;

                var result = await MaterialDesignThemes.Wpf.DialogHost.Show(EditDialog);
                if (result != null && (bool)result)
                {
                    // 更新学生信息的逻辑
                }
            }
            catch (Exception ex)
            {
                ShowError("编辑学生信息失败", ex);
            }
        }

        // 班级与院系管理相关方法
        private async void DepartmentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var selectedDepartment = DepartmentComboBox.SelectedItem as Department;
                if (selectedDepartment != null)
                {
                    var classes = await _infoService.GetClassesByDepartmentAsync(selectedDepartment.DepartmentID);
                    ClassComboBox.ItemsSource = classes;
                    ClassComboBox.DisplayMemberPath = "ClassName";
                }
            }
            catch (Exception ex)
            {
                ShowError("加载班级数据失败", ex);
            }
        }

        private async void YearComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await RefreshClassList();
        }

        private async void QueryClassList_Click(object sender, RoutedEventArgs e)
        {
            await RefreshClassList();
        }

        private async Task RefreshClassList()
        {
            try
            {
                var selectedDepartment = DepartmentComboBox.SelectedItem as Department;
                var selectedYear = YearComboBox.SelectedItem as int?;
                var selectedClass = ClassComboBox.SelectedItem as Class;

                if (selectedDepartment != null && selectedYear.HasValue)
                {
                    var students = await _infoService.GetStudentsByClassAsync(
                        selectedDepartment.DepartmentID,
                        selectedYear.Value,
                        selectedClass?.ClassID
                    );

                    ClassStudentDataGrid.ItemsSource = students;
                }
            }
            catch (Exception ex)
            {
                ShowError("查询班级名单失败", ex);
            }
        }

        // 学期管理相关方法
        private async void AddSemester_Click(object sender, RoutedEventArgs e)
        {
            _selectedSemesterId = null;
            SemesterDialogTitle.Text = "添加新学期";
            ClearSemesterDialog();
            await MaterialDesignThemes.Wpf.DialogHost.Show(SemesterDialog);
        }

        private async void EditSemester_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var semester = button.DataContext as Semester;
            if (semester != null)
            {
                _selectedSemesterId = semester.SemesterID;
                SemesterDialogTitle.Text = "编辑学期";
                LoadSemesterToDialog(semester);
                await MaterialDesignThemes.Wpf.DialogHost.Show(SemesterDialog);
            }
        }

        private async void SaveSemester_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateSemesterInput())
                {
                    return;
                }

                var semester = new Semester
                {
                    SemesterID = _selectedSemesterId ?? 0,
                    SemesterName = DialogSemesterComboBox.Text,
                    AcademicYearID = GetAcademicYearFromComboBox(),
                    StartDate = StartDatePicker.SelectedDate.Value,
                    EndDate = EndDatePicker.SelectedDate.Value
                };

                if (_selectedSemesterId.HasValue)
                {
                    await _infoService.UpdateSemesterAsync(semester);
                }
                else
                {
                    await _infoService.AddSemesterAsync(semester);
                }

                await LoadSemesters();
                MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand.Execute(null, null);
            }
            catch (Exception ex)
            {
                ShowError("保存学期失败", ex);
            }
        }

        private async void DeleteSemester_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var semester = button.DataContext as Semester;
                if (semester != null)
                {
                    var result = MessageBox.Show(
                        "确定要删除这个学期吗？",
                        "确认删除",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        await _infoService.DeleteSemesterAsync(semester.SemesterID);
                        await LoadSemesters();
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError("删除学期失败", ex);
            }
        }

        // 辅助方法
        private void ShowError(string message, Exception ex)
        {
            MessageBox.Show(
                $"{message}: {ex.Message}",
                "错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private TextBox CreateTextBox(string label, string value, bool isReadOnly = false)
        {
            var textBox = new TextBox
            {
                Style = FindResource("MaterialDesignOutlinedTextBox") as Style,
                Text = value,
                IsReadOnly = isReadOnly,
                Margin = new Thickness(0, 0, 0, 16)
            };
            MaterialDesignThemes.Wpf.HintAssist.SetHint(textBox, label);
            return textBox;
        }

        private bool ValidateSemesterInput()
        {
            if (DialogYearComboBox.SelectedItem == null)
            {
                MessageBox.Show("请选择学年", "验证失败");
                return false;
            }

            if (DialogSemesterComboBox.SelectedItem == null)
            {
                MessageBox.Show("请选择学期", "验证失败");
                return false;
            }

            if (!StartDatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("请选择开始日期", "验证失败");
                return false;
            }

            if (!EndDatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("请选择结束日期", "验证失败");
                return false;
            }

            if (StartDatePicker.SelectedDate > EndDatePicker.SelectedDate)
            {
                MessageBox.Show("开始日期不能晚于结束日期", "验证失败");
                return false;
            }

            return true;
        }

        private void ClearSemesterDialog()
        {
            DialogYearComboBox.SelectedItem = null;
            DialogSemesterComboBox.SelectedItem = null;
            StartDatePicker.SelectedDate = null;
            EndDatePicker.SelectedDate = null;
        }

        private void LoadSemesterToDialog(Semester semester)
        {
            DialogYearComboBox.SelectedItem = $"{semester.AcademicYearID}-{semester.AcademicYearID + 1}学年";
            DialogSemesterComboBox.Text = semester.SemesterName;
            StartDatePicker.SelectedDate = semester.StartDate;
            EndDatePicker.SelectedDate = semester.EndDate;
        }

        private int GetAcademicYearFromComboBox()
        {
            var yearRange = DialogYearComboBox.SelectedItem.ToString();
            return int.Parse(yearRange.Split('-')[0]);
        }


    }
}
