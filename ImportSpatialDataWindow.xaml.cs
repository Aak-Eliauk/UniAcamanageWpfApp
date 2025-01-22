using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;
using UniAcamanageWpfApp.GeometryT;

namespace UniAcamanageWpfApp
{
    public partial class ImportSpatialDataWindow : Window
    {
        private readonly ClassroomSpatialImporter _importer;
        private string _selectedFilePath;

        public ImportSpatialDataWindow()
        {
            InitializeComponent();
            _importer = new ClassroomSpatialImporter();

            // 订阅日志事件
            _importer.LogMessage += message =>
            {
                // 确保在UI线程上更新
                Dispatcher.Invoke(() =>
                {
                    txtLog.AppendText(message + Environment.NewLine);
                    txtLog.ScrollToEnd();
                });
            };

            // 订阅进度事件
            _importer.ProgressChanged += progress =>
            {
                Dispatcher.Invoke(() =>
                {
                    progressBar.Value = progress;
                });
            };
        }

        private async void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFilePath))
            {
                MessageBox.Show("请先选择Shapefile文件");
                return;
            }

            try
            {
                // 清空之前的日志
                txtLog.Clear();
                progressBar.Value = 0;

                txtStatus.Text = "正在导入...";
                await _importer.ImportShapefileData(_selectedFilePath);

                var (matched, unmatched) = await _importer.ValidateDataMapping();
                txtMatchedCount.Text = $"已匹配：{matched}";
                txtUnmatchedCount.Text = $"未匹配：{unmatched}";

                txtStatus.Text = "导入完成";
                MessageBox.Show("导入完成！");
            }
            catch (Exception ex)
            {
                txtStatus.Text = "导入失败";
                MessageBox.Show($"导入失败：{ex.Message}");
            }
        }

        private void BtnSelectFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                DefaultExt = ".shp",
                Filter = "Shapefile文件|*.shp"
            };

            if (dialog.ShowDialog() == true)
            {
                _selectedFilePath = dialog.FileName;
                txtFilePath.Text = _selectedFilePath;
            }
        }
    }
}
