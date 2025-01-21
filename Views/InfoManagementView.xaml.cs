using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using System.Configuration;
using System.Data;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Excel = Microsoft.Office.Interop.Excel;
using UniAcamanageWpfApp.Views;
using System;
using MaterialDesignThemes.Wpf;
using System.Threading.Tasks;
using ClosedXML.Excel;

namespace UniAcamanageWpfApp.Views
{
    public partial class InfoManagementView : UserControl
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
        private DataTable teachersTable = new DataTable();
        private DataTable studentsTable = new DataTable();
        private DataTable classStudentsTable = new DataTable();
        private DataTable semestersTable = new DataTable();
        private DataTable departmentsTable = new DataTable();
        private DataTable classesTable = new DataTable();
        private string currentEditType;
        private object currentEditItem;
        private int? currentEditingSemesterId;

        public InfoManagementView()
        {
            InitializeComponent();
            LoadInitialData();

            // 初始化时加载所有学生数据
            ClearFilters_Click(null, null);
            InitializeSemesterManagement();
        }

        private void LoadInitialData()
        {
            try
            {
                // 按顺序初始化各个下拉框
                InitializeYearComboBox();
                InitializeDepartmentComboBoxes();
                InitializeMajorComboBox();
                InitializeClassComboBox();

                // 加载其他数据
                LoadTeachers();
                LoadStudents();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化数据时出错：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitializeYearComboBox()
        {
            var currentYear = DateTime.Now.Year;
            var years = Enumerable.Range(currentYear - 5, 6)
                .Select(year => year.ToString())
                .ToList();

            // 初始化所有年份相关的下拉框
            if (YearComboBox != null)
            {
                YearComboBox.ItemsSource = null;
                YearComboBox.Items.Clear();
                YearComboBox.ItemsSource = years;
            }
        }

        private void InitializeDepartmentComboBoxes()
        {
            var dt = new DataTable();
            dt.Columns.Add("DepartmentID", typeof(string));
            dt.Columns.Add("DepartmentName", typeof(string));
            dt.Rows.Add("ALL", "全部院系"); // 修改为更明确的 ALL 标识符

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT DepartmentID, DepartmentName FROM Department ORDER BY DepartmentName", conn);
                var adapter = new SqlDataAdapter(cmd);
                var deptDt = new DataTable();
                adapter.Fill(deptDt);

                foreach (DataRow row in deptDt.Rows)
                {
                    dt.Rows.Add(row["DepartmentID"], row["DepartmentName"]);
                }
            }

            var view = dt.DefaultView;

            // 初始化所有院系相关的下拉框
            InitializeComboBox(DepartmentFilterComboBox, view, "DepartmentName", "DepartmentID");
            InitializeComboBox(DepartmentComboBox, view, "DepartmentName", "DepartmentID");
        }

        private void InitializeMajorComboBox()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT DISTINCT Major FROM Student ORDER BY Major", conn);
                var adapter = new SqlDataAdapter(cmd);
                var dt = new DataTable();

                // 创建一个新的 DataTable 包含"全部"选项
                var dtWithAll = new DataTable();
                dtWithAll.Columns.Add("Major", typeof(string));
                dtWithAll.Rows.Add("全部专业"); // 添加"全部"选项

                adapter.Fill(dt);

                // 将查询结果添加到带有"全部"选项的 DataTable 中
                foreach (DataRow row in dt.Rows)
                {
                    dtWithAll.Rows.Add(row["Major"]);
                }

                InitializeComboBox(MajorFilterComboBox, dtWithAll.DefaultView, "Major", "Major");
            }
        }

        private void InitializeClassComboBox()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                // 创建包含"全部"选项的 DataTable
                var dtWithAll = new DataTable();
                dtWithAll.Columns.Add("ClassID", typeof(string));
                dtWithAll.Columns.Add("ClassName", typeof(string));
                dtWithAll.Rows.Add("ALL", "全部班级"); // 添加"全部"选项

                // 获取实际的班级数据
                var cmd = new SqlCommand(@"
            SELECT c.ClassID, c.ClassName 
            FROM Class c 
            ORDER BY c.ClassName", conn);
                var adapter = new SqlDataAdapter(cmd);
                var dt = new DataTable();
                adapter.Fill(dt);

                // 将查询结果添加到带有"全部"选项的 DataTable 中
                foreach (DataRow row in dt.Rows)
                {
                    dtWithAll.Rows.Add(row["ClassID"], row["ClassName"]);
                }

                InitializeComboBox(ClassComboBox, dtWithAll.DefaultView, "ClassName", "ClassID");
                InitializeComboBox(ClassFilterComboBox, dtWithAll.DefaultView, "ClassName", "ClassID");
            }
        }

        // 通用的ComboBox初始化方法
        private void InitializeComboBox(ComboBox comboBox, DataView dataView, string displayMember, string valueMember)
        {
            if (comboBox != null)
            {
                comboBox.ItemsSource = null;
                comboBox.Items.Clear();
                comboBox.DisplayMemberPath = displayMember;
                comboBox.SelectedValuePath = valueMember;
                comboBox.ItemsSource = dataView;
            }
        }

        // ComboBox选择改变事件处理
        private void DepartmentManageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedValue != null)
            {
                string departmentId = comboBox.SelectedValue.ToString();
                UpdateClassComboBox(departmentId);
            }
        }

        private void UpdateClassComboBox(string departmentId)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                var cmd = new SqlCommand(@"
            SELECT ClassID, ClassName 
            FROM Class 
            WHERE DepartmentID = @DepartmentID
            ORDER BY ClassName", conn);

                cmd.Parameters.AddWithValue("@DepartmentID", departmentId);

                var adapter = new SqlDataAdapter(cmd);
                var dt = new DataTable();
                adapter.Fill(dt);

                InitializeComboBox(ClassComboBox, dt.DefaultView, "ClassName", "ClassID");
            }
        }

        private void InitializeFilters()
        {
            try
            {
                // 初始化年份下拉框
                var currentYear = DateTime.Now.Year;
                var years = Enumerable.Range(currentYear - 5, 6)
                    .Select(year => year.ToString())
                    .ToList();

                if (YearComboBox != null)
                {
                    YearComboBox.ItemsSource = null;
                    YearComboBox.Items.Clear();
                    YearComboBox.ItemsSource = years;
                }

                // 初始化院系下拉框
                var dt = new DataTable();
                dt.Columns.Add("DepartmentID", typeof(string));
                dt.Columns.Add("DepartmentName", typeof(string));
                dt.Rows.Add("", "全部学院");

                using (var conn = new SqlConnection(connectionString))
                {
                    var cmd = new SqlCommand("SELECT DepartmentID, DepartmentName FROM Department ORDER BY DepartmentName", conn);
                    var adapter = new SqlDataAdapter(cmd);
                    var deptDt = new DataTable();
                    adapter.Fill(deptDt);

                    foreach (DataRow row in deptDt.Rows)
                    {
                        dt.Rows.Add(row["DepartmentID"], row["DepartmentName"]);
                    }
                }

                if (DepartmentComboBox != null)
                {
                    DepartmentComboBox.ItemsSource = null;
                    DepartmentComboBox.Items.Clear();
                    DepartmentComboBox.DisplayMemberPath = "DepartmentName";
                    DepartmentComboBox.SelectedValuePath = "DepartmentID";
                    DepartmentComboBox.ItemsSource = dt.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化筛选条件时出错：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadAllClasses()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                var cmd = new SqlCommand(@"
            SELECT c.ClassID, c.ClassName, d.DepartmentName
            FROM Class c
            LEFT JOIN Department d ON c.DepartmentID = d.DepartmentID
            ORDER BY d.DepartmentName, c.ClassName", conn);

                var adapter = new SqlDataAdapter(cmd);
                var dt = new DataTable();
                adapter.Fill(dt);

                if (ClassComboBox != null)
                {
                    ClassComboBox.ItemsSource = dt.DefaultView;
                    ClassComboBox.DisplayMemberPath = "ClassName";
                    ClassComboBox.SelectedValuePath = "ClassID";
                }
            }
        }

        // 添加一个辅助方法来帮助我们验证和获取对话框中的控件
        private T FindDialogControl<T>(string hint) where T : FrameworkElement
        {
            if (DialogContentPanel == null) return null;
            return DialogContentPanel.Children.OfType<T>()
                .FirstOrDefault(x => MaterialDesignThemes.Wpf.HintAssist.GetHint(x)?.ToString() == hint);
        }

        // 添加一个方法来清理当前编辑状态
        private void ClearEditState()
        {
            currentEditType = null;
            currentEditItem = null;
            if (DialogContentPanel != null)
                DialogContentPanel.Children.Clear();
            if (DialogTitleBlock != null)
                DialogTitleBlock.Text = string.Empty;
        }

        // 添加一个方法来设置编辑状态
        private void SetEditState(string type, object item = null, string title = null)
        {
            currentEditType = type;
            currentEditItem = item;
            if (DialogTitleBlock != null)
                DialogTitleBlock.Text = title ?? $"添加{type}";
        }

        private void ShowEditDialog(string type, object item = null, string title = null)
        {
            currentEditType = type;
            currentEditItem = item;
            DialogTitleBlock.Text = title ?? $"添加{type}";
            DialogContentPanel.Children.Clear();

            switch (type)
            {
                case "教师":
                    CreateTeacherEditContent(item as DataRowView);
                    break;
                case "学生":
                    CreateStudentEditContent(item as DataRowView);
                    break;
                case "院系":
                    CreateDepartmentEditContent(item as DataRowView);
                    break;
                case "班级":
                    CreateClassEditContent(item as DataRowView);
                    break;
                case "学期":
                    CreateSemesterEditContent(item as DataRowView);
                    break;
            }

            EditDialog.IsOpen = true;
        }

        private void CreateTeacherEditContent(DataRowView teacher = null)
        {
            var isEdit = teacher != null;

            // 创建输入控件
            TextBox idBox = new TextBox();
            idBox.Style = FindResource("MaterialDesignOutlinedTextBox") as Style;
            idBox.Margin = new Thickness(0, 0, 0, 16);
            MaterialDesignThemes.Wpf.HintAssist.SetHint(idBox, "工号");

            TextBox nameBox = new TextBox();
            nameBox.Style = FindResource("MaterialDesignOutlinedTextBox") as Style;
            nameBox.Margin = new Thickness(0, 0, 0, 16);
            MaterialDesignThemes.Wpf.HintAssist.SetHint(nameBox, "姓名");

            TextBox titleBox = new TextBox();
            titleBox.Style = FindResource("MaterialDesignOutlinedTextBox") as Style;
            titleBox.Margin = new Thickness(0, 0, 0, 16);
            MaterialDesignThemes.Wpf.HintAssist.SetHint(titleBox, "职称");

            TextBox phoneBox = new TextBox();
            phoneBox.Style = FindResource("MaterialDesignOutlinedTextBox") as Style;
            phoneBox.Margin = new Thickness(0, 0, 0, 16);
            MaterialDesignThemes.Wpf.HintAssist.SetHint(phoneBox, "电话");

            TextBox emailBox = new TextBox();
            emailBox.Style = FindResource("MaterialDesignOutlinedTextBox") as Style;
            emailBox.Margin = new Thickness(0, 0, 0, 16);
            MaterialDesignThemes.Wpf.HintAssist.SetHint(emailBox, "邮箱");

            ComboBox deptCombo = new ComboBox();
            deptCombo.Style = FindResource("MaterialDesignOutlinedComboBox") as Style;
            deptCombo.Margin = new Thickness(0, 0, 0, 16);
            MaterialDesignThemes.Wpf.HintAssist.SetHint(deptCombo, "所属院系");

            // 加载部门数据
            using (var conn = new SqlConnection(connectionString))
            {
                var cmd = new SqlCommand("SELECT * FROM Department", conn);
                var adapter = new SqlDataAdapter(cmd);
                var dt = new DataTable();
                adapter.Fill(dt);
                deptCombo.ItemsSource = dt.DefaultView;
                deptCombo.DisplayMemberPath = "DepartmentName";
                deptCombo.SelectedValuePath = "DepartmentID";
            }

            // 如果是编辑模式，填充数据
            if (isEdit)
            {
                idBox.Text = teacher["TeacherID"].ToString();
                idBox.IsEnabled = false;
                nameBox.Text = teacher["Name"].ToString();
                titleBox.Text = teacher["Title"].ToString();
                phoneBox.Text = teacher["Phone"].ToString();
                emailBox.Text = teacher["Email"].ToString();
                deptCombo.SelectedValue = teacher["DepartmentID"];
            }

            // 添加到对话框
            DialogContentPanel.Children.Add(idBox);
            DialogContentPanel.Children.Add(nameBox);
            DialogContentPanel.Children.Add(titleBox);
            DialogContentPanel.Children.Add(phoneBox);
            DialogContentPanel.Children.Add(emailBox);
            DialogContentPanel.Children.Add(deptCombo);
        }

        private void CreateStudentEditContent(DataRowView student = null)
        {
            var isEdit = student != null;

            // 学号
            TextBox idBox = new TextBox
            {
                Style = FindResource("MaterialDesignOutlinedTextBox") as Style,
                Margin = new Thickness(0, 0, 0, 16)
            };
            MaterialDesignThemes.Wpf.HintAssist.SetHint(idBox, "学号");

            // 姓名
            TextBox nameBox = new TextBox
            {
                Style = FindResource("MaterialDesignOutlinedTextBox") as Style,
                Margin = new Thickness(0, 0, 0, 16)
            };
            MaterialDesignThemes.Wpf.HintAssist.SetHint(nameBox, "姓名");

            // 性别
            ComboBox genderBox = new ComboBox
            {
                Style = FindResource("MaterialDesignOutlinedComboBox") as Style,
                Margin = new Thickness(0, 0, 0, 16),
                ItemsSource = new[] { "男", "女" }
            };
            MaterialDesignThemes.Wpf.HintAssist.SetHint(genderBox, "性别");

            // 出生日期
            DatePicker birthDatePicker = new DatePicker
            {
                Style = FindResource("MaterialDesignOutlinedDatePicker") as Style,
                Margin = new Thickness(0, 0, 0, 16)
            };
            MaterialDesignThemes.Wpf.HintAssist.SetHint(birthDatePicker, "出生日期");

            // 专业
            TextBox majorBox = new TextBox
            {
                Style = FindResource("MaterialDesignOutlinedTextBox") as Style,
                Margin = new Thickness(0, 0, 0, 16)
            };
            MaterialDesignThemes.Wpf.HintAssist.SetHint(majorBox, "专业");

            // 班级选择
            ComboBox classBox = new ComboBox
            {
                Style = FindResource("MaterialDesignOutlinedComboBox") as Style,
                Margin = new Thickness(0, 0, 0, 16)
            };
            MaterialDesignThemes.Wpf.HintAssist.SetHint(classBox, "班级");

            // 入学年份
            TextBox yearBox = new TextBox
            {
                Style = FindResource("MaterialDesignOutlinedTextBox") as Style,
                Margin = new Thickness(0, 0, 0, 16)
            };
            MaterialDesignThemes.Wpf.HintAssist.SetHint(yearBox, "入学年份");

            // 学籍状态
            ComboBox statusBox = new ComboBox
            {
                Style = FindResource("MaterialDesignOutlinedComboBox") as Style,
                Margin = new Thickness(0, 0, 0, 16),
                ItemsSource = new[] { "在读", "休学", "毕业" }
            };
            MaterialDesignThemes.Wpf.HintAssist.SetHint(statusBox, "学籍状态");

            // 联系电话（可选）
            TextBox phoneBox = new TextBox
            {
                Style = FindResource("MaterialDesignOutlinedTextBox") as Style,
                Margin = new Thickness(0, 0, 0, 16)
            };
            MaterialDesignThemes.Wpf.HintAssist.SetHint(phoneBox, "联系电话（选填）");

            // 电子邮箱（可选）
            TextBox emailBox = new TextBox
            {
                Style = FindResource("MaterialDesignOutlinedTextBox") as Style,
                Margin = new Thickness(0, 0, 0, 16)
            };
            MaterialDesignThemes.Wpf.HintAssist.SetHint(emailBox, "电子邮箱（选填）");

            // 通讯地址（可选）
            TextBox addressBox = new TextBox
            {
                Style = FindResource("MaterialDesignOutlinedTextBox") as Style,
                Margin = new Thickness(0, 0, 0, 16)
            };
            MaterialDesignThemes.Wpf.HintAssist.SetHint(addressBox, "通讯地址（选填）");

            // 加载班级数据
            LoadClassesForComboBox(classBox);

            // 如果是编辑模式，填充数据
            if (isEdit)
            {
                idBox.Text = student["StudentID"].ToString();
                idBox.IsEnabled = false;
                nameBox.Text = student["Name"].ToString();
                genderBox.SelectedItem = student["Gender"].ToString();
                birthDatePicker.SelectedDate = Convert.ToDateTime(student["BirthDate"]);
                majorBox.Text = student["Major"].ToString();
                classBox.SelectedValue = student["ClassID"];
                yearBox.Text = student["YearOfAdmission"].ToString();
                statusBox.SelectedItem = student["Status"].ToString();
                phoneBox.Text = student["Phone"]?.ToString();
                emailBox.Text = student["Email"]?.ToString();
                addressBox.Text = student["Address"]?.ToString();
            }

            // 添加到对话框
            DialogContentPanel.Children.Add(idBox);
            DialogContentPanel.Children.Add(nameBox);
            DialogContentPanel.Children.Add(genderBox);
            DialogContentPanel.Children.Add(birthDatePicker);
            DialogContentPanel.Children.Add(majorBox);
            DialogContentPanel.Children.Add(classBox);
            DialogContentPanel.Children.Add(yearBox);
            DialogContentPanel.Children.Add(statusBox);
            DialogContentPanel.Children.Add(phoneBox);
            DialogContentPanel.Children.Add(emailBox);
            DialogContentPanel.Children.Add(addressBox);
        }

        private void LoadClassesForComboBox(ComboBox classBox)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                var cmd = new SqlCommand(@"
            SELECT c.ClassID, c.ClassName, d.DepartmentName
            FROM Class c
            JOIN Department d ON c.DepartmentID = d.DepartmentID
            ORDER BY d.DepartmentName, c.ClassName", conn);
                var adapter = new SqlDataAdapter(cmd);
                var dt = new DataTable();
                adapter.Fill(dt);
                classBox.ItemsSource = dt.DefaultView;
                classBox.DisplayMemberPath = "ClassName";
                classBox.SelectedValuePath = "ClassID";
            }
        }

        private void CreateDepartmentEditContent(DataRowView department = null)
        {
            var isEdit = department != null;

            // 院系编号
            TextBox idBox = new TextBox();
            idBox.Style = FindResource("MaterialDesignOutlinedTextBox") as Style;
            idBox.Margin = new Thickness(0, 0, 0, 16);
            MaterialDesignThemes.Wpf.HintAssist.SetHint(idBox, "院系编号");

            // 院系名称
            TextBox nameBox = new TextBox();
            nameBox.Style = FindResource("MaterialDesignOutlinedTextBox") as Style;
            nameBox.Margin = new Thickness(0, 0, 0, 16);
            MaterialDesignThemes.Wpf.HintAssist.SetHint(nameBox, "院系名称");

            // 办公电话
            TextBox phoneBox = new TextBox();
            phoneBox.Style = FindResource("MaterialDesignOutlinedTextBox") as Style;
            phoneBox.Margin = new Thickness(0, 0, 0, 16);
            MaterialDesignThemes.Wpf.HintAssist.SetHint(phoneBox, "办公电话");

            if (isEdit)
            {
                idBox.Text = department["DepartmentID"].ToString();
                idBox.IsEnabled = false;
                nameBox.Text = department["DepartmentName"].ToString();
                phoneBox.Text = department["OfficePhone"].ToString();
            }

            DialogContentPanel.Children.Add(idBox);
            DialogContentPanel.Children.Add(nameBox);
            DialogContentPanel.Children.Add(phoneBox);
        }

        private void CreateClassEditContent(DataRowView classItem = null)
        {
            var isEdit = classItem != null;

            // 班级编号
            TextBox idBox = new TextBox();
            idBox.Style = FindResource("MaterialDesignOutlinedTextBox") as Style;
            idBox.Margin = new Thickness(0, 0, 0, 16);
            MaterialDesignThemes.Wpf.HintAssist.SetHint(idBox, "班级编号");

            // 班级名称
            TextBox nameBox = new TextBox();
            nameBox.Style = FindResource("MaterialDesignOutlinedTextBox") as Style;
            nameBox.Margin = new Thickness(0, 0, 0, 16);
            MaterialDesignThemes.Wpf.HintAssist.SetHint(nameBox, "班级名称");

            // 所属院系
            ComboBox deptBox = new ComboBox();
            deptBox.Style = FindResource("MaterialDesignOutlinedComboBox") as Style;
            deptBox.Margin = new Thickness(0, 0, 0, 16);
            MaterialDesignThemes.Wpf.HintAssist.SetHint(deptBox, "所属院系");

            // 加载院系数据
            using (var conn = new SqlConnection(connectionString))
            {
                var cmd = new SqlCommand("SELECT DepartmentID, DepartmentName FROM Department", conn);
                var adapter = new SqlDataAdapter(cmd);
                var dt = new DataTable();
                adapter.Fill(dt);
                deptBox.ItemsSource = dt.DefaultView;
                deptBox.DisplayMemberPath = "DepartmentName";
                deptBox.SelectedValuePath = "DepartmentID";
            }

            if (isEdit)
            {
                idBox.Text = classItem["ClassID"].ToString();
                idBox.IsEnabled = false;
                nameBox.Text = classItem["ClassName"].ToString();
                deptBox.SelectedValue = classItem["DepartmentID"];
            }

            DialogContentPanel.Children.Add(idBox);
            DialogContentPanel.Children.Add(nameBox);
            DialogContentPanel.Children.Add(deptBox);
        }

        private void CreateSemesterEditContent(DataRowView semester = null)
        {
            var isEdit = semester != null;

            // 学年选择
            ComboBox yearBox = new ComboBox();
            yearBox.Style = FindResource("MaterialDesignOutlinedComboBox") as Style;
            yearBox.Margin = new Thickness(0, 0, 0, 16);
            MaterialDesignThemes.Wpf.HintAssist.SetHint(yearBox, "学年");

            // 填充学年数据（当前年份往前推5年）
            var currentYear = DateTime.Now.Year;
            var years = Enumerable.Range(currentYear - 5, 6)
                .Select(year => $"{year}-{year + 1}");
            yearBox.ItemsSource = years;

            // 学期选择
            ComboBox semesterBox = new ComboBox();
            semesterBox.Style = FindResource("MaterialDesignOutlinedComboBox") as Style;
            semesterBox.Margin = new Thickness(0, 0, 0, 16);
            MaterialDesignThemes.Wpf.HintAssist.SetHint(semesterBox, "学期");
            semesterBox.ItemsSource = new[] { "第一学期", "第二学期" };

            // 开始日期
            DatePicker startDatePicker = new DatePicker();
            startDatePicker.Style = FindResource("MaterialDesignOutlinedDatePicker") as Style;
            startDatePicker.Margin = new Thickness(0, 0, 0, 16);
            MaterialDesignThemes.Wpf.HintAssist.SetHint(startDatePicker, "开始日期");

            // 结束日期
            DatePicker endDatePicker = new DatePicker();
            endDatePicker.Style = FindResource("MaterialDesignOutlinedDatePicker") as Style;
            endDatePicker.Margin = new Thickness(0, 0, 0, 16);
            MaterialDesignThemes.Wpf.HintAssist.SetHint(endDatePicker, "结束日期");

            if (isEdit)
            {
                yearBox.SelectedValue = semester["AcademicYearID"].ToString();
                semesterBox.SelectedItem = semester["SemesterName"].ToString();
                startDatePicker.SelectedDate = Convert.ToDateTime(semester["StartDate"]);
                endDatePicker.SelectedDate = Convert.ToDateTime(semester["EndDate"]);
            }

            DialogContentPanel.Children.Add(yearBox);
            DialogContentPanel.Children.Add(semesterBox);
            DialogContentPanel.Children.Add(startDatePicker);
            DialogContentPanel.Children.Add(endDatePicker);
        }

        private async void SaveDialog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 显示加载指示器
                LoadingIndicator.Visibility = Visibility.Visible;

                bool success = false;
                string message = "保存成功！";

                switch (currentEditType)
                {
                    case "教师":
                        await SaveTeacher();
                        success = true;
                        break;

                    case "学生":
                        await SaveStudent();
                        success = true;
                        break;

                    case "院系":
                        await SaveDepartment();
                        success = true;
                        break;

                    case "班级":
                        await SaveClass();
                        success = true;
                        break;

                    case "学期":
                        await SaveSemester();
                        success = true;
                        break;

                    default:
                        message = "未知的编辑类型！";
                        success = false;
                        break;
                }

                if (success)
                {
                    EditDialog.IsOpen = false;
                    LoadInitialData(); // 刷新数据
                    MessageBox.Show(message, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (SqlException sqlEx)
            {
                string errorMessage = "数据库错误：";
                switch (sqlEx.Number)
                {
                    case 2627: // 主键重复
                        errorMessage += "该记录已存在！";
                        break;
                    case 547:  // 外键约束
                        errorMessage += "无法保存，存在关联数据！";
                        break;
                    case 2601: // 唯一索引冲突
                        errorMessage += "数据重复！";
                        break;
                    default:
                        errorMessage += sqlEx.Message;
                        break;
                }
                MessageBox.Show(errorMessage, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 隐藏加载指示器
                LoadingIndicator.Visibility = Visibility.Collapsed;
            }
        }

        private async Task SaveTeacher()
        {
            var inputs = DialogContentPanel.Children.OfType<TextBox>().ToList();
            var deptCombo = DialogContentPanel.Children.OfType<ComboBox>().FirstOrDefault();

            var idBox = inputs.FirstOrDefault(x => MaterialDesignThemes.Wpf.HintAssist.GetHint(x).ToString() == "工号");
            var nameBox = inputs.FirstOrDefault(x => MaterialDesignThemes.Wpf.HintAssist.GetHint(x).ToString() == "姓名");
            var titleBox = inputs.FirstOrDefault(x => MaterialDesignThemes.Wpf.HintAssist.GetHint(x).ToString() == "职称");
            var phoneBox = inputs.FirstOrDefault(x => MaterialDesignThemes.Wpf.HintAssist.GetHint(x).ToString() == "电话");
            var emailBox = inputs.FirstOrDefault(x => MaterialDesignThemes.Wpf.HintAssist.GetHint(x).ToString() == "邮箱");

            using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                var cmd = new SqlCommand();

                if (currentEditItem == null)
                {
                    cmd.CommandText = @"
                    INSERT INTO Teacher (TeacherID, Name, Title, Phone, Email, DepartmentID)
                    VALUES (@TeacherID, @Name, @Title, @Phone, @Email, @DepartmentID)";
                }
                else
                {
                    cmd.CommandText = @"
                    UPDATE Teacher 
                    SET Name = @Name, Title = @Title, 
                        Phone = @Phone, Email = @Email, 
                        DepartmentID = @DepartmentID
                    WHERE TeacherID = @TeacherID";
                }

                cmd.Connection = conn;
                cmd.Parameters.AddWithValue("@TeacherID", idBox.Text);
                cmd.Parameters.AddWithValue("@Name", nameBox.Text);
                cmd.Parameters.AddWithValue("@Title", titleBox.Text);
                cmd.Parameters.AddWithValue("@Phone", phoneBox.Text);
                cmd.Parameters.AddWithValue("@Email", emailBox.Text);
                cmd.Parameters.AddWithValue("@DepartmentID", deptCombo.SelectedValue);

                await cmd.ExecuteNonQueryAsync();
            }
        }
        private async Task SaveStudent()
        {
            try
            {
                var inputs = DialogContentPanel.Children.OfType<TextBox>().ToList();
                var combos = DialogContentPanel.Children.OfType<ComboBox>().ToList();
                var datePickers = DialogContentPanel.Children.OfType<DatePicker>().ToList();

                var studentId = inputs.FirstOrDefault(x => MaterialDesignThemes.Wpf.HintAssist.GetHint(x).ToString() == "学号")?.Text;
                var name = inputs.FirstOrDefault(x => MaterialDesignThemes.Wpf.HintAssist.GetHint(x).ToString() == "姓名")?.Text;
                var gender = combos.FirstOrDefault(x => MaterialDesignThemes.Wpf.HintAssist.GetHint(x).ToString() == "性别")?.SelectedValue?.ToString();
                var birthDate = datePickers.FirstOrDefault(x => MaterialDesignThemes.Wpf.HintAssist.GetHint(x).ToString() == "出生日期")?.SelectedDate;
                var classId = combos.FirstOrDefault(x => MaterialDesignThemes.Wpf.HintAssist.GetHint(x).ToString() == "班级")?.SelectedValue?.ToString();
                var yearOfAdmission = inputs.FirstOrDefault(x => MaterialDesignThemes.Wpf.HintAssist.GetHint(x).ToString() == "入学年份")?.Text;
                var major = inputs.FirstOrDefault(x => MaterialDesignThemes.Wpf.HintAssist.GetHint(x).ToString() == "专业")?.Text;
                var phone = inputs.FirstOrDefault(x => MaterialDesignThemes.Wpf.HintAssist.GetHint(x).ToString().Contains("联系电话"))?.Text;
                var email = inputs.FirstOrDefault(x => MaterialDesignThemes.Wpf.HintAssist.GetHint(x).ToString().Contains("电子邮箱"))?.Text;
                var status = combos.FirstOrDefault(x => MaterialDesignThemes.Wpf.HintAssist.GetHint(x).ToString() == "学籍状态")?.SelectedValue?.ToString();
                var address = inputs.FirstOrDefault(x => MaterialDesignThemes.Wpf.HintAssist.GetHint(x).ToString().Contains("通讯地址"))?.Text;

                // 验证必填字段
                if (string.IsNullOrWhiteSpace(studentId) ||
                    string.IsNullOrWhiteSpace(name) ||
                    string.IsNullOrWhiteSpace(gender) ||
                    !birthDate.HasValue ||
                    string.IsNullOrWhiteSpace(classId) ||
                    string.IsNullOrWhiteSpace(yearOfAdmission) ||
                    string.IsNullOrWhiteSpace(major) ||
                    string.IsNullOrWhiteSpace(status))
                {
                    throw new Exception("请填写所有必填字段！");
                }

                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    var cmd = new SqlCommand();

                    if (currentEditItem == null) // 添加
                    {
                        cmd.CommandText = @"
                    INSERT INTO Student 
                    (StudentID, Name, Gender, BirthDate, ClassID, YearOfAdmission, 
                     Major, Phone, Email, Status, Address)
                    VALUES 
                    (@StudentID, @Name, @Gender, @BirthDate, @ClassID, @YearOfAdmission,
                     @Major, @Phone, @Email, @Status, @Address)";
                    }
                    else // 编辑
                    {
                        cmd.CommandText = @"
                    UPDATE Student 
                    SET Name = @Name, 
                        Gender = @Gender, 
                        BirthDate = @BirthDate,
                        ClassID = @ClassID, 
                        YearOfAdmission = @YearOfAdmission,
                        Major = @Major, 
                        Phone = @Phone, 
                        Email = @Email, 
                        Status = @Status,
                        Address = @Address
                    WHERE StudentID = @StudentID";
                    }

                    cmd.Connection = conn;
                    cmd.Parameters.AddWithValue("@StudentID", studentId);
                    cmd.Parameters.AddWithValue("@Name", name);
                    cmd.Parameters.AddWithValue("@Gender", gender);
                    cmd.Parameters.AddWithValue("@BirthDate", birthDate.Value); // 使用 .Value 因为我们已经验证了它不是 null
                    cmd.Parameters.AddWithValue("@ClassID", classId);
                    cmd.Parameters.AddWithValue("@YearOfAdmission", int.Parse(yearOfAdmission));
                    cmd.Parameters.AddWithValue("@Major", major);
                    cmd.Parameters.AddWithValue("@Phone", (object)phone ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Email", (object)email ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Status", status);
                    cmd.Parameters.AddWithValue("@Address", (object)address ?? DBNull.Value);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }
        private async Task SaveDepartment()
        {
            var inputs = DialogContentPanel.Children.OfType<TextBox>().ToList();

            var departmentId = inputs.FirstOrDefault(x => MaterialDesignThemes.Wpf.HintAssist.GetHint(x).ToString() == "院系编号")?.Text;
            var departmentName = inputs.FirstOrDefault(x => MaterialDesignThemes.Wpf.HintAssist.GetHint(x).ToString() == "院系名称")?.Text;
            var officePhone = inputs.FirstOrDefault(x => MaterialDesignThemes.Wpf.HintAssist.GetHint(x).ToString() == "办公电话")?.Text;

            using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                var cmd = new SqlCommand();

                if (currentEditItem == null)
                {
                    cmd.CommandText = @"
                INSERT INTO Department (DepartmentID, DepartmentName, OfficePhone)
                VALUES (@DepartmentID, @DepartmentName, @OfficePhone)";
                }
                else
                {
                    cmd.CommandText = @"
                UPDATE Department 
                SET DepartmentName = @DepartmentName, OfficePhone = @OfficePhone
                WHERE DepartmentID = @DepartmentID";
                }

                cmd.Connection = conn;
                cmd.Parameters.AddWithValue("@DepartmentID", departmentId);
                cmd.Parameters.AddWithValue("@DepartmentName", departmentName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@OfficePhone", officePhone ?? (object)DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task SaveClass()
        {
            var inputs = DialogContentPanel.Children.OfType<TextBox>().ToList();
            var combos = DialogContentPanel.Children.OfType<ComboBox>().ToList();

            var classId = inputs.FirstOrDefault(x => MaterialDesignThemes.Wpf.HintAssist.GetHint(x).ToString() == "班级编号")?.Text;
            var className = inputs.FirstOrDefault(x => MaterialDesignThemes.Wpf.HintAssist.GetHint(x).ToString() == "班级名称")?.Text;
            var departmentId = combos.FirstOrDefault(x => MaterialDesignThemes.Wpf.HintAssist.GetHint(x).ToString() == "所属院系")?.SelectedValue?.ToString();

            using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                var cmd = new SqlCommand();

                if (currentEditItem == null)
                {
                    cmd.CommandText = @"
                INSERT INTO Class (ClassID, ClassName, DepartmentID)
                VALUES (@ClassID, @ClassName, @DepartmentID)";
                }
                else
                {
                    cmd.CommandText = @"
                UPDATE Class 
                SET ClassName = @ClassName, DepartmentID = @DepartmentID
                WHERE ClassID = @ClassID";
                }

                cmd.Connection = conn;
                cmd.Parameters.AddWithValue("@ClassID", classId);
                cmd.Parameters.AddWithValue("@ClassName", className ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@DepartmentID", departmentId ?? (object)DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task SaveSemester()
        {
            var yearCombo = DialogContentPanel.Children.OfType<ComboBox>().FirstOrDefault(x => MaterialDesignThemes.Wpf.HintAssist.GetHint(x).ToString() == "学年");
            var semesterCombo = DialogContentPanel.Children.OfType<ComboBox>().FirstOrDefault(x => MaterialDesignThemes.Wpf.HintAssist.GetHint(x).ToString() == "学期");
            var startDatePicker = DialogContentPanel.Children.OfType<DatePicker>().FirstOrDefault(x => MaterialDesignThemes.Wpf.HintAssist.GetHint(x).ToString() == "开始日期");
            var endDatePicker = DialogContentPanel.Children.OfType<DatePicker>().FirstOrDefault(x => MaterialDesignThemes.Wpf.HintAssist.GetHint(x).ToString() == "结束日期");

            using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                var cmd = new SqlCommand();

                if (currentEditItem == null)
                {
                    cmd.CommandText = @"
                INSERT INTO Semester (SemesterName, AcademicYearID, StartDate, EndDate)
                VALUES (@SemesterName, @AcademicYearID, @StartDate, @EndDate)";
                }
                else
                {
                    cmd.CommandText = @"
                UPDATE Semester 
                SET SemesterName = @SemesterName, 
                    AcademicYearID = @AcademicYearID,
                    StartDate = @StartDate,
                    EndDate = @EndDate
                WHERE SemesterID = @SemesterID";

                    cmd.Parameters.AddWithValue("@SemesterID", (currentEditItem as DataRowView)["SemesterID"]);
                }

                cmd.Connection = conn;
                cmd.Parameters.AddWithValue("@SemesterName", semesterCombo.SelectedValue?.ToString());
                cmd.Parameters.AddWithValue("@AcademicYearID", yearCombo.SelectedValue);
                cmd.Parameters.AddWithValue("@StartDate", startDatePicker.SelectedDate);
                cmd.Parameters.AddWithValue("@EndDate", endDatePicker.SelectedDate);

                await cmd.ExecuteNonQueryAsync();
            }
        }


        private void DepartmentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DepartmentComboBox?.SelectedValue != null)
            {
                LoadClassesForDepartment(DepartmentComboBox.SelectedValue.ToString());
            }
        }
        private void LoadClassesForDepartment(string departmentId)
        {
            if (string.IsNullOrEmpty(departmentId) || departmentId == "ALL")
            {
                LoadAllClasses();
                return;
            }

            using (var conn = new SqlConnection(connectionString))
            {
                var dtWithAll = new DataTable();
                dtWithAll.Columns.Add("ClassID", typeof(string));
                dtWithAll.Columns.Add("ClassName", typeof(string));
                dtWithAll.Rows.Add("ALL", "全部班级");

                var cmd = new SqlCommand(@"
            SELECT ClassID, ClassName 
            FROM Class 
            WHERE DepartmentID = @DepartmentID
            ORDER BY ClassName", conn);

                cmd.Parameters.AddWithValue("@DepartmentID", departmentId);

                var adapter = new SqlDataAdapter(cmd);
                var dt = new DataTable();
                adapter.Fill(dt);

                foreach (DataRow row in dt.Rows)
                {
                    dtWithAll.Rows.Add(row["ClassID"], row["ClassName"]);
                }

                if (ClassComboBox != null)
                {
                    ClassComboBox.ItemsSource = dtWithAll.DefaultView;
                    ClassComboBox.DisplayMemberPath = "ClassName";
                    ClassComboBox.SelectedValuePath = "ClassID";
                }
            }
        }

        private void EditStudent_Click(object sender, RoutedEventArgs e)
        {
            var row = ((Button)sender).DataContext as DataRowView;
            if (row == null) return;
            ShowEditDialog("学生", row, "编辑学生信息");
        }

        private void EditSemester_Click(object sender, RoutedEventArgs e)
        {
            var row = ((Button)sender).DataContext as DataRowView;
            if (row == null) return;

            DialogYearComboBox.SelectedItem = row["AcademicYearID"].ToString();
            DialogSemesterComboBox.Text = row["SemesterName"].ToString();
            StartDatePicker.SelectedDate = Convert.ToDateTime(row["StartDate"]);
            EndDatePicker.SelectedDate = Convert.ToDateTime(row["EndDate"]);

            SemesterDialogTitle.Text = "编辑学期";
            SemesterDialog.IsOpen = true;
        }

        private void LoadTeachers()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                var cmd = new SqlCommand(@"
            SELECT t.TeacherID, t.Name, t.Title, t.Phone, t.Email, 
                   t.DepartmentID, d.DepartmentName
            FROM Teacher t
            LEFT JOIN Department d ON t.DepartmentID = d.DepartmentID", conn);

                var adapter = new SqlDataAdapter(cmd);
                teachersTable.Clear();
                adapter.Fill(teachersTable);
                TeacherDataGrid.ItemsSource = teachersTable.DefaultView;
            }
        }

        private void LoadStudents()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                var cmd = new SqlCommand(@"
            SELECT s.*, c.ClassName, d.DepartmentName
            FROM Student s
            LEFT JOIN Class c ON s.ClassID = c.ClassID
            LEFT JOIN Department d ON c.DepartmentID = d.DepartmentID", conn);

                var adapter = new SqlDataAdapter(cmd);
                studentsTable.Clear();
                adapter.Fill(studentsTable);
                StudentDataGrid.ItemsSource = studentsTable.DefaultView;
            }
        }

        private void LoadDepartments()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                var cmd = new SqlCommand("SELECT * FROM Department ORDER BY DepartmentName", conn);
                var adapter = new SqlDataAdapter(cmd);
                departmentsTable.Clear();
                adapter.Fill(departmentsTable);
            }
        }

        private void LoadClasses()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                var cmd = new SqlCommand(@"
            SELECT c.*, d.DepartmentName
            FROM Class c
            LEFT JOIN Department d ON c.DepartmentID = d.DepartmentID
            ORDER BY c.ClassName", conn);
                var adapter = new SqlDataAdapter(cmd);
                classesTable.Clear();
                adapter.Fill(classesTable);
            }
        }

        private void LoadSemesters()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                var cmd = new SqlCommand("SELECT * FROM Semester", conn);
                var adapter = new SqlDataAdapter(cmd);
                semestersTable.Clear();
                adapter.Fill(semestersTable);
                SemesterDataGrid.ItemsSource = semestersTable.DefaultView;
            }
        }

        private void InitializeYearAndSemesterTypes()
        {
            var currentYear = DateTime.Now.Year;
            var years = new List<string>();
            for (int i = 0; i < 5; i++)
            {
                years.Add((currentYear - i).ToString());
            }
            YearComboBox.ItemsSource = years;

            var semesterTypes = new List<string> { "第一学期", "第二学期" };
            SemesterTypeComboBox.ItemsSource = semesterTypes;
        }

        // 添加一个方法来更新搜索结果数量
        private void UpdateStudentSearchResults(int count)
        {
            if (SearchResultCount != null)
            {
                SearchResultCount.Text = $"找到 {count} 条记录";
            }
        }

        private void ViewStudentDetails_Click(object sender, RoutedEventArgs e)
        {
            var student = ((Button)sender).DataContext as DataRowView;
            if (student == null) return;

            StudentDetailsGrid.Children.Clear();
            StudentDetailsGrid.RowDefinitions.Clear();

            var details = new Dictionary<string, string>
    {
        {"学号", student["StudentID"].ToString()},
        {"姓名", student["Name"].ToString()},
        {"性别", student["Gender"].ToString()},
        {"出生日期", Convert.ToDateTime(student["BirthDate"]).ToString("yyyy-MM-dd")},
        {"专业", student["Major"].ToString()},
        {"班级", student["ClassName"].ToString()},
        {"入学年份", student["YearOfAdmission"].ToString()},
        {"学籍状态", student["Status"].ToString()},
        {"联系电话", student["Phone"]?.ToString() ?? "未填写"},
        {"电子邮箱", student["Email"]?.ToString() ?? "未填写"},
        {"通讯地址", student["Address"]?.ToString() ?? "未填写"},
        {"平均学分绩点", student["GPA"]?.ToString() ?? "暂无"}
    };

            int row = 0;
            foreach (var detail in details)
            {
                // 添加行定义
                StudentDetailsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // 添加标签
                var label = new TextBlock
                {
                    Text = detail.Key + "：",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 10, 10)
                };
                Grid.SetRow(label, row);
                Grid.SetColumn(label, 0);

                // 添加值
                var value = new TextBlock
                {
                    Text = detail.Value,
                    Margin = new Thickness(0, 0, 0, 10),
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetRow(value, row);
                Grid.SetColumn(value, 1);

                StudentDetailsGrid.Children.Add(label);
                StudentDetailsGrid.Children.Add(value);

                row++;
            }

            StudentDetailsDialog.IsOpen = true;
        }

        // 删除教师
        private void DeleteTeacher_Click(object sender, RoutedEventArgs e)
        {
            var row = ((Button)sender).DataContext as DataRowView;
            if (row == null) return;

            var result = MessageBox.Show(
                $"确定要删除教师 {row["Name"]} 吗？",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using (var conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        var cmd = new SqlCommand(
                            "DELETE FROM Teacher WHERE TeacherID = @TeacherID", conn);
                        cmd.Parameters.AddWithValue("@TeacherID", row["TeacherID"]);
                        cmd.ExecuteNonQuery();
                    }
                    LoadTeachers();
                    MessageBox.Show("删除成功！");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"删除失败：{ex.Message}");
                }
            }
        }

        // 删除学生
        private void DeleteStudent_Click(object sender, RoutedEventArgs e)
        {
            var row = ((Button)sender).DataContext as DataRowView;
            if (row == null) return;

            var result = MessageBox.Show(
                $"确定要删除学生 {row["Name"]} 吗？",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using (var conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        var cmd = new SqlCommand(
                            "DELETE FROM Student WHERE StudentID = @StudentID", conn);
                        cmd.Parameters.AddWithValue("@StudentID", row["StudentID"]);
                        cmd.ExecuteNonQuery();
                    }
                    LoadStudents();
                    MessageBox.Show("删除成功！");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"删除失败：{ex.Message}");
                }
            }
        }

        // 编辑教师
        private void EditTeacher_Click(object sender, RoutedEventArgs e)
        {
            var row = ((Button)sender).DataContext as DataRowView;
            if (row == null) return;
            ShowEditDialog("教师", row, "编辑教师信息");
        }

        private void AddSemester_Click(object sender, RoutedEventArgs e)
        {
            SemesterDialog.IsOpen = true;
        }

        private void SaveSemester_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand(@"
                INSERT INTO Semester (SemesterName, AcademicYearID, StartDate, EndDate)
                VALUES (@SemesterName, @AcademicYearID, @StartDate, @EndDate)", conn);

                    cmd.Parameters.AddWithValue("@SemesterName", DialogSemesterComboBox.Text);
                    cmd.Parameters.AddWithValue("@AcademicYearID",
                        int.Parse(DialogYearComboBox.SelectedItem.ToString()));
                    cmd.Parameters.AddWithValue("@StartDate", StartDatePicker.SelectedDate);
                    cmd.Parameters.AddWithValue("@EndDate", EndDatePicker.SelectedDate);

                    cmd.ExecuteNonQuery();
                }

                MessageBox.Show("添加成功！");
                SemesterDialog.IsOpen = false;
                LoadSemesters();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"添加失败：{ex.Message}");
            }
        }

        private void DeleteSemester_Click(object sender, RoutedEventArgs e)
        {
            var row = ((Button)sender).DataContext as DataRowView;
            if (row == null) return;

            var result = MessageBox.Show(
                $"确定要删除学期 {row["SemesterName"]} 吗？",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using (var conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        var cmd = new SqlCommand(
                            "DELETE FROM Semester WHERE SemesterID = @SemesterID", conn);
                        cmd.Parameters.AddWithValue("@SemesterID", row["SemesterID"]);
                        cmd.ExecuteNonQuery();
                    }
                    LoadSemesters();
                    MessageBox.Show("删除成功！");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"删除失败：{ex.Message}");
                }
            }
        }

        // 添加学生按钮点击事件
        private void AddStudent_Click(object sender, RoutedEventArgs e)
        {
            ShowEditDialog("学生", null, "添加学生");
        }

        // 添加班级按钮点击事件
        private void AddClass_Click(object sender, RoutedEventArgs e)
        {
            ShowEditDialog("班级", null, "添加班级");
        }

        // 添加院系按钮点击事件
        private void AddDepartment_Click(object sender, RoutedEventArgs e)
        {
            ShowEditDialog("院系", null, "添加院系");
        }

        // 添加教师按钮点击事件
        private void AddTeacher_Click(object sender, RoutedEventArgs e)
        {
            ShowEditDialog("教师", null, "添加教师");
        }


        private async Task<string> GenerateNewDepartmentIdAsync()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                var cmd = new SqlCommand(@"
            SELECT TOP 1 DepartmentID 
            FROM Department 
            ORDER BY DepartmentID DESC", conn);

                var lastId = await cmd.ExecuteScalarAsync() as string;

                if (string.IsNullOrEmpty(lastId))
                    return "D0001";

                var number = int.Parse(lastId.Substring(1)) + 1;
                return $"D{number:D4}";
            }
        }


        private async Task<bool> HasRelatedDataAsync(string departmentId)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();

                // 检查是否有关联的班级
                var cmdClass = new SqlCommand(@"
            SELECT COUNT(*) 
            FROM Class 
            WHERE DepartmentID = @DepartmentID", conn);
                cmdClass.Parameters.AddWithValue("@DepartmentID", departmentId);
                var classCount = (int)await cmdClass.ExecuteScalarAsync();

                // 检查是否有关联的教师
                var cmdTeacher = new SqlCommand(@"
            SELECT COUNT(*) 
            FROM Teacher 
            WHERE DepartmentID = @DepartmentID", conn);
                cmdTeacher.Parameters.AddWithValue("@DepartmentID", departmentId);
                var teacherCount = (int)await cmdTeacher.ExecuteScalarAsync();

                return classCount > 0 || teacherCount > 0;
            }
        }

        private void ManageDepartments_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = new DepartmentManagementWindow(connectionString)
                {
                    Owner = Window.GetWindow(this),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                window.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"打开院系管理窗口失败：{ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ManageClasses_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = new ClassManagementWindow(connectionString)
                {
                    Owner = Window.GetWindow(this),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                window.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"打开班级管理窗口失败：{ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void SearchTeacher_Click(object sender, RoutedEventArgs e)
        {
            var searchText = TeacherSearchBox.Text.Trim();
            var selectedDept = DepartmentFilterComboBox.SelectedValue?.ToString();

            using (var conn = new SqlConnection(connectionString))
            {
                var query = @"
            SELECT t.TeacherID, t.Name, t.Title, t.Phone, t.Email, 
                   t.DepartmentID, d.DepartmentName
            FROM Teacher t
            LEFT JOIN Department d ON t.DepartmentID = d.DepartmentID
            WHERE 1=1";

                if (!string.IsNullOrEmpty(searchText))
                    query += " AND (t.TeacherID LIKE @Search OR t.Name LIKE @Search)";

                if (!string.IsNullOrEmpty(selectedDept))
                    query += " AND t.DepartmentID = @DepartmentID";

                var cmd = new SqlCommand(query, conn);

                if (!string.IsNullOrEmpty(searchText))
                    cmd.Parameters.AddWithValue("@Search", $"%{searchText}%");

                if (!string.IsNullOrEmpty(selectedDept))
                    cmd.Parameters.AddWithValue("@DepartmentID", selectedDept);

                var adapter = new SqlDataAdapter(cmd);
                teachersTable.Clear();
                adapter.Fill(teachersTable);
                TeacherDataGrid.ItemsSource = teachersTable.DefaultView;
            }
        }

        private void SearchStudent_Click(object sender, RoutedEventArgs e)
        {
            var searchText = StudentSearchBox.Text.Trim();
            var selectedMajor = MajorFilterComboBox.SelectedValue?.ToString();
            var selectedClass = ClassFilterComboBox.SelectedValue?.ToString();

            using (var conn = new SqlConnection(connectionString))
            {
                var query = @"
            SELECT s.*, c.ClassName, d.DepartmentName
            FROM Student s
            LEFT JOIN Class c ON s.ClassID = c.ClassID
            LEFT JOIN Department d ON c.DepartmentID = d.DepartmentID
            WHERE 1=1";

                if (!string.IsNullOrEmpty(searchText))
                    query += " AND (s.StudentID LIKE @Search OR s.Name LIKE @Search)";

                if (!string.IsNullOrEmpty(selectedMajor) && selectedMajor != "全部专业")
                    query += " AND s.Major = @Major";

                if (!string.IsNullOrEmpty(selectedClass) && selectedClass != "ALL")
                    query += " AND s.ClassID = @ClassID";

                var cmd = new SqlCommand(query, conn);

                if (!string.IsNullOrEmpty(searchText))
                    cmd.Parameters.AddWithValue("@Search", $"%{searchText}%");

                if (!string.IsNullOrEmpty(selectedMajor) && selectedMajor != "全部专业")
                    cmd.Parameters.AddWithValue("@Major", selectedMajor);

                if (!string.IsNullOrEmpty(selectedClass) && selectedClass != "ALL")
                    cmd.Parameters.AddWithValue("@ClassID", selectedClass);

                var adapter = new SqlDataAdapter(cmd);
                studentsTable.Clear();
                adapter.Fill(studentsTable);
                StudentDataGrid.ItemsSource = studentsTable.DefaultView;

                // 更新搜索结果数量
                UpdateSearchResultCount(studentsTable.Rows.Count);
            }
        }
        private void UpdateSearchResultCount(int count)
        {
            if (SearchResultCount != null)
            {
                SearchResultCount.Text = $"找到 {count} 条记录";
            }
        }

        private void DepartmentFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedDept = DepartmentFilterComboBox.SelectedValue?.ToString();
            if (selectedDept == "") // 选择了"全部学院"
            {
                LoadAllClasses();
            }
            else if (selectedDept != null)
            {
                LoadClassesForDepartment(selectedDept);
            }
        }

        private void ClassFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 如果需要在选择班级时自动触发搜索，可以在这里调用SearchStudent_Click
            if (ClassFilterComboBox.SelectedValue != null)
            {
                SearchStudent_Click(sender, e);
            }
        }

        private void SearchClassStudent_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedYear = YearComboBox.SelectedValue?.ToString();
                var selectedDept = DepartmentComboBox.SelectedValue?.ToString();
                var selectedClass = ClassComboBox.SelectedValue?.ToString();

                using (var conn = new SqlConnection(connectionString))
                {
                    var query = @"
                SELECT s.StudentID, s.Name, s.Gender, s.Major, s.BirthDate,
                       s.ClassID, s.YearOfAdmission,
                       c.ClassName, d.DepartmentName
                FROM Student s
                LEFT JOIN Class c ON s.ClassID = c.ClassID
                LEFT JOIN Department d ON c.DepartmentID = d.DepartmentID
                WHERE 1=1";

                    if (!string.IsNullOrEmpty(selectedYear) && selectedYear != "ALL")
                        query += " AND s.YearOfAdmission = @Year";

                    if (!string.IsNullOrEmpty(selectedDept) && selectedDept != "ALL")
                        query += " AND d.DepartmentID = @DepartmentID";

                    if (!string.IsNullOrEmpty(selectedClass) && selectedClass != "ALL")
                        query += " AND c.ClassID = @ClassID";

                    query += " ORDER BY s.StudentID";

                    var cmd = new SqlCommand(query, conn);

                    if (!string.IsNullOrEmpty(selectedYear) && selectedYear != "ALL")
                        cmd.Parameters.AddWithValue("@Year", selectedYear);

                    if (!string.IsNullOrEmpty(selectedDept) && selectedDept != "ALL")
                        cmd.Parameters.AddWithValue("@DepartmentID", selectedDept);

                    if (!string.IsNullOrEmpty(selectedClass) && selectedClass != "ALL")
                        cmd.Parameters.AddWithValue("@ClassID", selectedClass);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);

                    ClassStudentDataGrid.ItemsSource = dt.DefaultView;
                    UpdateSearchResultCount(dt.Rows.Count);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"查询失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void EditClassStudent_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var student = button.DataContext as DataRowView;
                if (student == null) return;

                var dialog = new Window
                {
                    Title = "编辑学生信息",
                    Width = 500,
                    Height = 650,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Window.GetWindow(this),
                    Style = FindResource("MaterialDesignWindow") as Style,
                    ResizeMode = ResizeMode.NoResize
                };

                var scrollViewer = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Margin = new Thickness(0, 0, 0, 10)
                };

                var panel = new StackPanel { Margin = new Thickness(16) };

                // 学号（只读）
                var studentIdBox = CreateTextBox(student["StudentID"].ToString(), "学号", true);

                // 姓名
                var nameBox = CreateTextBox(student["Name"].ToString(), "姓名");
                MaterialDesignThemes.Wpf.HintAssist.SetHelperText(nameBox, "必填项");

                // 性别
                var genderBox = new ComboBox
                {
                    Style = FindResource("MaterialDesignOutlinedComboBox") as Style,
                    Margin = new Thickness(0, 0, 0, 16),
                    ItemsSource = new[] { "男", "女" },
                    SelectedItem = student["Gender"].ToString()
                };
                MaterialDesignThemes.Wpf.HintAssist.SetHint(genderBox, "性别");

                // 出生日期
                var birthDatePicker = new DatePicker
                {
                    Style = FindResource("MaterialDesignOutlinedDatePicker") as Style,
                    Margin = new Thickness(0, 0, 0, 16),
                    Language = System.Windows.Markup.XmlLanguage.GetLanguage("zh-CN")
                };
                if (student["BirthDate"] != DBNull.Value)
                {
                    birthDatePicker.SelectedDate = Convert.ToDateTime(student["BirthDate"]);
                }
                MaterialDesignThemes.Wpf.HintAssist.SetHint(birthDatePicker, "出生日期");

                // 专业
                var majorBox = CreateTextBox(student["Major"].ToString(), "专业");

                // 入学年份
                var yearBox = CreateTextBox(student["YearOfAdmission"].ToString(), "入学年份");

                // 班级选择
                var classBox = new ComboBox
                {
                    Style = FindResource("MaterialDesignOutlinedComboBox") as Style,
                    Margin = new Thickness(0, 0, 0, 16)
                };
                MaterialDesignThemes.Wpf.HintAssist.SetHint(classBox, "班级");

                // 异步加载班级数据
                await LoadClassesForComboBoxAsync(classBox);
                classBox.SelectedValue = student["ClassID"].ToString();

                // 电话
                var phoneBox = CreateTextBox(
                    student["Phone"] != DBNull.Value ? student["Phone"].ToString() : "",
                    "电话");

                // 电子邮件
                var emailBox = CreateTextBox(
                    student["Email"] != DBNull.Value ? student["Email"].ToString() : "",
                    "电子邮件");

                // 地址
                var addressBox = CreateMultiLineTextBox(
                    student["Address"] != DBNull.Value ? student["Address"].ToString() : "",
                    "地址");

                // 按钮面板
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 16, 0, 0)
                };

                // 取消按钮
                var cancelButton = new Button
                {
                    Content = "取消",
                    Style = FindResource("MaterialDesignOutlinedButton") as Style,
                    Margin = new Thickness(0, 0, 16, 0)
                };
                cancelButton.Click += (s, args) => dialog.Close();

                // 保存按钮
                var saveButton = new Button
                {
                    Content = "保存",
                    Style = FindResource("MaterialDesignRaisedButton") as Style
                };

                saveButton.Click += async (s, args) =>
                {
                    try
                    {
                        if (!ValidateInput(nameBox, genderBox, birthDatePicker, majorBox, classBox, yearBox, out int yearValue))
                        {
                            return;
                        }

                        if (!ValidateContactInfo(phoneBox, emailBox))
                        {
                            return;
                        }

                        await UpdateStudentAsync(
                            studentIdBox.Text,
                            nameBox.Text.Trim(),
                            genderBox.SelectedItem.ToString(),
                            birthDatePicker.SelectedDate.Value,
                            majorBox.Text.Trim(),
                            classBox.SelectedValue.ToString(),
                            yearValue,
                            phoneBox.Text.Trim(),
                            emailBox.Text.Trim(),
                            addressBox.Text.Trim()
                        );

                        MessageBox.Show("保存成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        dialog.Close();
                        await RefreshDataGridAsync();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"保存失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };

                // 添加所有控件到面板
                AddControlsToPanel(panel, new UIElement[]
                {
            studentIdBox, nameBox, genderBox, birthDatePicker, majorBox,
            classBox, yearBox, phoneBox, emailBox, addressBox
                });

                buttonPanel.Children.Add(cancelButton);
                buttonPanel.Children.Add(saveButton);
                panel.Children.Add(buttonPanel);

                scrollViewer.Content = panel;
                dialog.Content = scrollViewer;
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"编辑失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private TextBox CreateTextBox(string text, string hint, bool isReadOnly = false)
        {
            var textBox = new TextBox
            {
                Style = FindResource("MaterialDesignOutlinedTextBox") as Style,
                Margin = new Thickness(0, 0, 0, 16),
                Text = text,
                IsReadOnly = isReadOnly
            };
            MaterialDesignThemes.Wpf.HintAssist.SetHint(textBox, hint);
            return textBox;
        }

        private TextBox CreateMultiLineTextBox(string text, string hint)
        {
            var textBox = new TextBox
            {
                Style = FindResource("MaterialDesignOutlinedTextBox") as Style,
                Margin = new Thickness(0, 0, 0, 16),
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                Height = 60
            };
            MaterialDesignThemes.Wpf.HintAssist.SetHint(textBox, hint);
            return textBox;
        }

        private void AddControlsToPanel(StackPanel panel, UIElement[] controls)
        {
            foreach (var control in controls)
            {
                panel.Children.Add(control);
            }
        }

        private bool ValidateInput(TextBox nameBox, ComboBox genderBox, DatePicker birthDatePicker,
            TextBox majorBox, ComboBox classBox, TextBox yearBox, out int yearValue)
        {
            yearValue = 0;

            if (string.IsNullOrWhiteSpace(nameBox.Text) ||
                genderBox.SelectedItem == null ||
                birthDatePicker.SelectedDate == null ||
                string.IsNullOrWhiteSpace(majorBox.Text) ||
                classBox.SelectedValue == null ||
                !int.TryParse(yearBox.Text, out yearValue))
            {
                MessageBox.Show("请填写所有必填字段！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (yearValue < 1900 || yearValue > DateTime.Now.Year)
            {
                MessageBox.Show("请输入有效的入学年份！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private bool ValidateContactInfo(TextBox phoneBox, TextBox emailBox)
        {
            if (!string.IsNullOrWhiteSpace(phoneBox.Text) && !Regex.IsMatch(phoneBox.Text, @"^[0-9-]{6,20}$"))
            {
                MessageBox.Show("请输入有效的电话号码！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(emailBox.Text) && !Regex.IsMatch(emailBox.Text, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                MessageBox.Show("请输入有效的电子邮件地址！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private async Task LoadClassesForComboBoxAsync(ComboBox classBox)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    var cmd = new SqlCommand(@"
                SELECT c.ClassID, 
                       c.ClassName + ' (' + d.DepartmentName + ')' as DisplayName
                FROM Class c
                JOIN Department d ON c.DepartmentID = d.DepartmentID
                ORDER BY d.DepartmentName, c.ClassName", conn);

                    var dt = new DataTable();
                    using (var adapter = new SqlDataAdapter(cmd))
                    {
                        await Task.Run(() => adapter.Fill(dt));
                    }

                    classBox.ItemsSource = dt.DefaultView;
                    classBox.DisplayMemberPath = "DisplayName";
                    classBox.SelectedValuePath = "ClassID";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载班级数据失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task UpdateStudentAsync(string studentId, string name, string gender,
            DateTime birthDate, string major, string classId, int yearOfAdmission,
            string phone, string email, string address)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                var cmd = new SqlCommand(@"
            UPDATE Student 
            SET Name = @Name,
                Gender = @Gender,
                BirthDate = @BirthDate,
                Major = @Major,
                ClassID = @ClassID,
                YearOfAdmission = @YearOfAdmission,
                Phone = @Phone,
                Email = @Email,
                Address = @Address
            WHERE StudentID = @StudentID", conn);

                cmd.Parameters.AddWithValue("@StudentID", studentId);
                cmd.Parameters.AddWithValue("@Name", name);
                cmd.Parameters.AddWithValue("@Gender", gender);
                cmd.Parameters.AddWithValue("@BirthDate", birthDate.Date);
                cmd.Parameters.AddWithValue("@Major", major);
                cmd.Parameters.AddWithValue("@ClassID", classId);
                cmd.Parameters.AddWithValue("@YearOfAdmission", yearOfAdmission);
                cmd.Parameters.AddWithValue("@Phone", string.IsNullOrWhiteSpace(phone) ? DBNull.Value : (object)phone);
                cmd.Parameters.AddWithValue("@Email", string.IsNullOrWhiteSpace(email) ? DBNull.Value : (object)email);
                cmd.Parameters.AddWithValue("@Address", string.IsNullOrWhiteSpace(address) ? DBNull.Value : (object)address);

                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task RefreshDataGridAsync()
        {
            await Task.Run(() => Dispatcher.Invoke(() => SearchClassStudent_Click(null, null)));
        }

        private async void DeleteClassStudent_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var student = button.DataContext as DataRowView;
                if (student == null) return;

                // 获取学生信息用于显示确认消息
                string studentId = student["StudentID"].ToString();
                string studentName = student["Name"].ToString();

                // 显示确认对话框
                var result = MessageBox.Show(
                    $"确定要删除学生 {studentName}（学号：{studentId}）吗？\n此操作不可恢复！",
                    "确认删除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No); // 默认选择"否"

                if (result == MessageBoxResult.Yes)
                {
                    using (var conn = new SqlConnection(connectionString))
                    {
                        await conn.OpenAsync();

                        // 首先检查是否存在关联数据
                        var checkCmd = new SqlCommand(@"
                    SELECT 
                        (SELECT COUNT(*) FROM StudentCourse WHERE StudentID = @StudentID) as CourseCount
                    ", conn);
                        checkCmd.Parameters.AddWithValue("@StudentID", studentId);

                        using (var reader = await checkCmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                int courseCount = reader.GetInt32(0);

                                if (courseCount > 0)
                                {
                                    MessageBox.Show(
                                        "无法删除该学生记录，因为存在关联的选课数据。\n请先删除相关的选课记录。",
                                        "警告",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Warning);
                                    return;
                                }
                            }
                        }

                        // 如果没有关联数据，执行删除操作
                        var deleteCmd = new SqlCommand(
                            "DELETE FROM Student WHERE StudentID = @StudentID",
                            conn);
                        deleteCmd.Parameters.AddWithValue("@StudentID", studentId);

                        int rowsAffected = await deleteCmd.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            MessageBox.Show(
                                $"已成功删除学生 {studentName} 的记录。",
                                "删除成功",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);

                            // 刷新数据网格
                            SearchClassStudent_Click(null, null);
                        }
                        else
                        {
                            MessageBox.Show(
                                "删除操作未影响任何记录。\n可能该记录已被其他用户删除。",
                                "提示",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                MessageBox.Show(
                    $"数据库操作错误：{sqlEx.Message}\n\n如果问题持续存在，请联系系统管理员。",
                    "数据库错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"删除操作失败：{ex.Message}\n\n如果问题持续存在，请联系系统管理员。",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 清除选择
                YearComboBox.SelectedIndex = -1;
                DepartmentComboBox.SelectedIndex = -1;
                ClassComboBox.SelectedIndex = -1;

                using (var conn = new SqlConnection(connectionString))
                {
                    var query = @"
                SELECT s.StudentID, s.Name, s.Gender, s.Major, s.BirthDate,
                       s.ClassID,
                       c.ClassName, d.DepartmentName, s.YearOfAdmission
                FROM Student s
                LEFT JOIN Class c ON s.ClassID = c.ClassID
                LEFT JOIN Department d ON c.DepartmentID = d.DepartmentID
                ORDER BY s.StudentID";

                    var cmd = new SqlCommand(query, conn);
                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);

                    ClassStudentDataGrid.ItemsSource = dt.DefaultView;
                    UpdateSearchResultCount(dt.Rows.Count);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"清除筛选失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 辅助方法：按年份和院系加载班级
        private void LoadClassesByYearAndDepartment(string year, string departmentId)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                var cmd = new SqlCommand(@"
            SELECT DISTINCT c.ClassID, c.ClassName
            FROM Class c
            INNER JOIN Student s ON c.ClassID = s.ClassID
            WHERE c.DepartmentID = @DepartmentID
            AND s.YearOfAdmission = @Year
            ORDER BY c.ClassName", conn);

                cmd.Parameters.AddWithValue("@DepartmentID", departmentId);
                cmd.Parameters.AddWithValue("@Year", int.Parse(year));

                var adapter = new SqlDataAdapter(cmd);
                var dt = new DataTable();
                adapter.Fill(dt);

                ClassComboBox.ItemsSource = dt.DefaultView;
                ClassComboBox.DisplayMemberPath = "ClassName";
                ClassComboBox.SelectedValuePath = "ClassID";
                ClassComboBox.SelectedIndex = -1;
            }
        }

        // 年份选择改变事件
        private void YearComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (YearComboBox?.SelectedItem != null && DepartmentComboBox?.SelectedValue != null)
            {
                LoadClassesByYearAndDepartment(
                    YearComboBox.SelectedItem.ToString(),
                    DepartmentComboBox.SelectedValue.ToString());
            }
        }

        private void ExportStudentList_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ClassStudentDataGrid.Items.Count == 0)
                {
                    MessageBox.Show("没有可导出的数据！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "Excel文件(*.xlsx)|*.xlsx",
                    Title = "导出班级学生名单",
                    DefaultExt = "xlsx",
                    FileName = $"班级学生名单_{DateTime.Now:yyyyMMddHHmmss}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    using (var workbook = new XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("学生名单");

                        // 添加标题行
                        var headers = new[] { "学号", "姓名", "性别", "院系", "专业", "班级", "入学年份", "出生日期" };
                        for (int i = 0; i < headers.Length; i++)
                        {
                            worksheet.Cell(1, i + 1).Value = headers[i];
                        }

                        // 设置标题行样式
                        var titleRow = worksheet.Row(1);
                        titleRow.Style.Font.Bold = true;
                        titleRow.Style.Fill.BackgroundColor = XLColor.LightGray;
                        titleRow.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        // 添加数据
                        int row = 2;
                        foreach (DataRowView rowView in ClassStudentDataGrid.Items)
                        {
                            worksheet.Cell(row, 1).Value = rowView["StudentID"]?.ToString();
                            worksheet.Cell(row, 2).Value = rowView["Name"]?.ToString();
                            worksheet.Cell(row, 3).Value = rowView["Gender"]?.ToString();
                            worksheet.Cell(row, 4).Value = rowView["DepartmentName"]?.ToString();
                            worksheet.Cell(row, 5).Value = rowView["Major"]?.ToString();
                            worksheet.Cell(row, 6).Value = rowView["ClassName"]?.ToString();
                            worksheet.Cell(row, 7).Value = rowView["YearOfAdmission"]?.ToString();

                            // 格式化日期
                            if (rowView["BirthDate"] != DBNull.Value)
                            {
                                var birthDate = (DateTime)rowView["BirthDate"];
                                worksheet.Cell(row, 8).Value = birthDate.ToString("yyyy-MM-dd");
                            }

                            row++;
                        }

                        // 调整列宽
                        worksheet.Columns().AdjustToContents();

                        // 添加边框
                        var dataRange = worksheet.Range(1, 1, row - 1, headers.Length);
                        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                        // 设置数据行居中对齐
                        worksheet.Range(2, 1, row - 1, headers.Length).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        // 保存文件
                        workbook.SaveAs(saveFileDialog.FileName);

                        MessageBox.Show("导出成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);

                        // 询问是否打开导出的文件
                        if (MessageBox.Show("是否打开导出的文件？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = saveFileDialog.FileName,
                                UseShellExecute = true
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 添加更详细的错误信息
                MessageBox.Show($"导出失败：{ex.Message}\n\n详细错误：{ex.StackTrace}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);

                // 调试信息
                if (ClassStudentDataGrid.Items.Count > 0)
                {
                    var firstRow = (ClassStudentDataGrid.Items[0] as DataRowView).Row;
                    var columns = string.Join(", ", firstRow.Table.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
                    MessageBox.Show($"可用列：{columns}", "调试信息", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

    }
}
