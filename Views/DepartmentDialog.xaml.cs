using System;
using System.Collections.Generic;
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
    public partial class DepartmentDialog : Window
    {
        public string DepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public string OfficePhone { get; set; }
        public bool IsEdit { get; set; }

        public DepartmentDialog()
        {
            InitializeComponent();
            this.Loaded += DepartmentDialog_Loaded;
        }

        private void DepartmentDialog_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsEdit)
            {
                this.Title = "编辑院系";
                DepartmentIdBox.IsEnabled = false;
            }
            else
            {
                this.Title = "添加院系";
                DepartmentIdBox.IsEnabled = true;
            }

            DepartmentIdBox.Text = DepartmentId;
            DepartmentNameBox.Text = DepartmentName;
            OfficePhoneBox.Text = OfficePhone;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(DepartmentIdBox.Text) ||
                string.IsNullOrWhiteSpace(DepartmentNameBox.Text))
            {
                MessageBox.Show("请填写所有必填字段！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Regex.IsMatch(DepartmentIdBox.Text, @"^[A-Za-z0-9]+$"))
            {
                MessageBox.Show("院系编号只能包含字母和数字！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!string.IsNullOrWhiteSpace(OfficePhoneBox.Text) &&
                !Regex.IsMatch(OfficePhoneBox.Text, @"^[0-9-]{6,20}$"))
            {
                MessageBox.Show("请输入有效的电话号码！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DepartmentId = DepartmentIdBox.Text.Trim();
            DepartmentName = DepartmentNameBox.Text.Trim();
            OfficePhone = OfficePhoneBox.Text.Trim();

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
