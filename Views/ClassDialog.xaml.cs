using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace UniAcamanageWpfApp.Views
{
    public partial class ClassDialog : Window
    {
        private readonly string connectionString;
        public string ClassId { get; set; }
        public string ClassName { get; set; }
        public string DepartmentId { get; set; }
        public bool IsEdit { get; set; }

        public ClassDialog(string connString)
        {
            InitializeComponent();
            connectionString = connString;
            this.Loaded += ClassDialog_Loaded;
        }

        private async void ClassDialog_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsEdit)
            {
                this.Title = "编辑班级";
                ClassIdBox.IsEnabled = false;
            }
            else
            {
                this.Title = "添加班级";
                ClassIdBox.IsEnabled = true;
            }

            ClassIdBox.Text = ClassId;
            ClassNameBox.Text = ClassName;

            await LoadDepartments();
            DepartmentComboBox.SelectedValue = DepartmentId;
        }

        private async Task LoadDepartments()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    var cmd = new SqlCommand(
                        "SELECT DepartmentID, DepartmentName FROM Department ORDER BY DepartmentName",
                        conn);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    await Task.Run(() => adapter.Fill(dt));

                    DepartmentComboBox.ItemsSource = dt.DefaultView;
                    DepartmentComboBox.DisplayMemberPath = "DepartmentName";
                    DepartmentComboBox.SelectedValuePath = "DepartmentID";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载院系数据失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ClassIdBox.Text) ||
                string.IsNullOrWhiteSpace(ClassNameBox.Text) ||
                DepartmentComboBox.SelectedValue == null)
            {
                MessageBox.Show("请填写所有必填字段！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Regex.IsMatch(ClassIdBox.Text, @"^[A-Za-z0-9]+$"))
            {
                MessageBox.Show("班级编号只能包含字母和数字！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ClassId = ClassIdBox.Text.Trim();
            ClassName = ClassNameBox.Text.Trim();
            DepartmentId = DepartmentComboBox.SelectedValue.ToString();

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}