using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace UniAcamanageWpfApp.Views
{
    public partial class ClassManagementWindow : Window
    {
        private readonly string connectionString;

        public ClassManagementWindow(string connString)
        {
            InitializeComponent();
            connectionString = connString;
            LoadDepartments();
            LoadClasses();
        }

        private async void LoadDepartments()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    var cmd = new SqlCommand(@"
                    SELECT DepartmentID, DepartmentName 
                    FROM Department 
                    ORDER BY DepartmentName", conn);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    await Task.Run(() => adapter.Fill(dt));

                    // 添加"全部"选项
                    var allRow = dt.NewRow();
                    allRow["DepartmentID"] = "ALL";
                    allRow["DepartmentName"] = "全部院系";
                    dt.Rows.InsertAt(allRow, 0);

                    DepartmentFilter.ItemsSource = dt.DefaultView;
                    DepartmentFilter.DisplayMemberPath = "DepartmentName";
                    DepartmentFilter.SelectedValuePath = "DepartmentID";
                    DepartmentFilter.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载院系数据失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LoadClasses(string departmentId = null)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    var query = @"
                    SELECT c.ClassID, c.ClassName, c.DepartmentID, d.DepartmentName
                    FROM Class c
                    JOIN Department d ON c.DepartmentID = d.DepartmentID";

                    if (!string.IsNullOrEmpty(departmentId) && departmentId != "ALL")
                    {
                        query += " WHERE c.DepartmentID = @DepartmentID";
                    }

                    query += " ORDER BY d.DepartmentName, c.ClassName";

                    var cmd = new SqlCommand(query, conn);
                    if (!string.IsNullOrEmpty(departmentId) && departmentId != "ALL")
                    {
                        cmd.Parameters.AddWithValue("@DepartmentID", departmentId);
                    }

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    await Task.Run(() => adapter.Fill(dt));
                    ClassesGrid.ItemsSource = dt.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载班级数据失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void AddClass_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ClassDialog(connectionString);
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    using (var conn = new SqlConnection(connectionString))
                    {
                        await conn.OpenAsync();
                        var cmd = new SqlCommand(@"
                        INSERT INTO Class (ClassID, ClassName, DepartmentID)
                        VALUES (@ClassID, @ClassName, @DepartmentID)", conn);

                        cmd.Parameters.AddWithValue("@ClassID", dialog.ClassId);
                        cmd.Parameters.AddWithValue("@ClassName", dialog.ClassName);
                        cmd.Parameters.AddWithValue("@DepartmentID", dialog.DepartmentId);

                        await cmd.ExecuteNonQueryAsync();
                        LoadClasses(DepartmentFilter.SelectedValue?.ToString());
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"添加班级失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void EditClass_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var classData = button.DataContext as DataRowView;
            if (classData == null) return;

            var dialog = new ClassDialog(connectionString)
            {
                ClassId = classData["ClassID"].ToString(),
                ClassName = classData["ClassName"].ToString(),
                DepartmentId = classData["DepartmentID"].ToString(),
                IsEdit = true
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    using (var conn = new SqlConnection(connectionString))
                    {
                        await conn.OpenAsync();
                        var cmd = new SqlCommand(@"
                        UPDATE Class 
                        SET ClassName = @ClassName,
                            DepartmentID = @DepartmentID
                        WHERE ClassID = @ClassID", conn);

                        cmd.Parameters.AddWithValue("@ClassID", dialog.ClassId);
                        cmd.Parameters.AddWithValue("@ClassName", dialog.ClassName);
                        cmd.Parameters.AddWithValue("@DepartmentID", dialog.DepartmentId);

                        await cmd.ExecuteNonQueryAsync();
                        LoadClasses(DepartmentFilter.SelectedValue?.ToString());
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"更新班级失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void DeleteClass_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var classData = button.DataContext as DataRowView;
            if (classData == null) return;

            var result = MessageBox.Show(
                $"确定要删除班级 \"{classData["ClassName"]}\" 吗？\n删除班级将影响该班级下的所有学生！",
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

                        // 首先检查是否有学生在这个班级
                        var checkCmd = new SqlCommand(
                            "SELECT COUNT(*) FROM Student WHERE ClassID = @ClassID",
                            conn);
                        checkCmd.Parameters.AddWithValue("@ClassID", classData["ClassID"]);

                        int studentCount = (int)await checkCmd.ExecuteScalarAsync();
                        if (studentCount > 0)
                        {
                            MessageBox.Show(
                                "无法删除该班级，因为还有学生属于该班级。\n请先将学生调离该班级后再试。",
                                "警告",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                            return;
                        }

                        // 删除班级
                        var deleteCmd = new SqlCommand(
                            "DELETE FROM Class WHERE ClassID = @ClassID",
                            conn);
                        deleteCmd.Parameters.AddWithValue("@ClassID", classData["ClassID"]);
                        await deleteCmd.ExecuteNonQueryAsync();

                        LoadClasses(DepartmentFilter.SelectedValue?.ToString());
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"删除班级失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DepartmentFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedDepartmentId = DepartmentFilter.SelectedValue?.ToString();
            LoadClasses(selectedDepartmentId);
        }
    }
}
