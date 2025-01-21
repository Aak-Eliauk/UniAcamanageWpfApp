using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace UniAcamanageWpfApp.Views
{
    public partial class DepartmentManagementWindow : Window
    {
        private readonly string connectionString;

        public DepartmentManagementWindow(string connString)
        {
            InitializeComponent();
            connectionString = connString;
            LoadDepartments();
        }

        private async void LoadDepartments()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    var cmd = new SqlCommand(@"
                SELECT DepartmentID, DepartmentName, OfficePhone 
                FROM Department 
                ORDER BY DepartmentName", conn);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    await Task.Run(() => adapter.Fill(dt));
                    DepartmentsGrid.ItemsSource = dt.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载院系数据失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddDepartment_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new DepartmentDialog();
                if (dialog.ShowDialog() == true)
                {
                    using (var conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        var cmd = new SqlCommand(@"
                    INSERT INTO Department (DepartmentID, DepartmentName, OfficePhone)
                    VALUES (@DepartmentID, @DepartmentName, @OfficePhone)", conn);

                        cmd.Parameters.AddWithValue("@DepartmentID", dialog.DepartmentId);
                        cmd.Parameters.AddWithValue("@DepartmentName", dialog.DepartmentName);
                        cmd.Parameters.AddWithValue("@OfficePhone",
                            string.IsNullOrWhiteSpace(dialog.OfficePhone) ?
                            (object)DBNull.Value : dialog.OfficePhone);

                        cmd.ExecuteNonQuery();
                        LoadDepartments();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"添加院系失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditDepartment_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var departmentData = button.DataContext as DataRowView;
            if (departmentData == null) return;

            try
            {
                var dialog = new DepartmentDialog
                {
                    DepartmentId = departmentData["DepartmentID"].ToString(),
                    DepartmentName = departmentData["DepartmentName"].ToString(),
                    OfficePhone = departmentData["OfficePhone"]?.ToString() ?? "",
                    IsEdit = true
                };

                if (dialog.ShowDialog() == true)
                {
                    using (var conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        var cmd = new SqlCommand(@"
                    UPDATE Department 
                    SET DepartmentName = @DepartmentName,
                        OfficePhone = @OfficePhone
                    WHERE DepartmentID = @DepartmentID", conn);

                        cmd.Parameters.AddWithValue("@DepartmentID", dialog.DepartmentId);
                        cmd.Parameters.AddWithValue("@DepartmentName", dialog.DepartmentName);
                        cmd.Parameters.AddWithValue("@OfficePhone",
                            string.IsNullOrWhiteSpace(dialog.OfficePhone) ?
                            (object)DBNull.Value : dialog.OfficePhone);

                        cmd.ExecuteNonQuery();
                        LoadDepartments();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"编辑院系失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DeleteDepartment_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var departmentData = button.DataContext as DataRowView;
            if (departmentData == null) return;

            var result = MessageBox.Show(
                $"确定要删除院系 \"{departmentData["DepartmentName"]}\" 吗？\n删除院系将影响该院系下的所有班级和学生！",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using (var conn = new SqlConnection(connectionString))
                    {
                        await conn.OpenAsync();

                        // 检查是否有班级属于这个院系
                        var checkCmd = new SqlCommand(
                            "SELECT COUNT(*) FROM Class WHERE DepartmentID = @DepartmentID",
                            conn);
                        checkCmd.Parameters.AddWithValue("@DepartmentID", departmentData["DepartmentID"]);

                        int classCount = (int)await checkCmd.ExecuteScalarAsync();
                        if (classCount > 0)
                        {
                            MessageBox.Show(
                                "无法删除该院系，因为还有班级属于该院系。\n请先删除或转移相关班级后再试。",
                                "警告",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                            return;
                        }

                        // 删除院系
                        var deleteCmd = new SqlCommand(
                            "DELETE FROM Department WHERE DepartmentID = @DepartmentID",
                            conn);
                        deleteCmd.Parameters.AddWithValue("@DepartmentID", departmentData["DepartmentID"]);
                        await deleteCmd.ExecuteNonQueryAsync();

                        LoadDepartments();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"删除院系失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}