using Microsoft.EntityFrameworkCore;
using Microsoft.Web.WebView2.Core;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using UniAcamanageWpfApp.Data;
using UniAcamanageWpfApp.Models;

namespace UniAcamanageWpfApp
{
    public partial class ClassroomMapWindow : Window
    {
        private readonly CampusDbContext _context;
        private bool isMapInitialized = false;
        private readonly string _classroomNumber;
        private ClassroomSpatial _targetClassroom;

        public ClassroomMapWindow(string classroomNumber)
        {
            InitializeComponent();
            _context = new CampusDbContext();
            _classroomNumber = classroomNumber;

            Loaded += ClassroomMapWindow_Loaded;
        }

        private async void ClassroomMapWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 加载教室信息
                _targetClassroom = await _context.ClassroomSpatials
                    .FirstOrDefaultAsync(c => c.RoomNumber == _classroomNumber);

                if (_targetClassroom == null)
                {
                    MessageBox.Show("未找到指定教室", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                    return;
                }

                // 显示教室基本信息
                txtClassroomInfo.Text = $"教室: {_targetClassroom.RoomNumber} ({_targetClassroom.SpatialLocation})";

                // 初始化地图
                await InitializeWebView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载教室信息失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private async Task InitializeWebView()
        {
            try
            {
                await webView.EnsureCoreWebView2Async();
                string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "map.html");

                if (File.Exists(htmlPath))
                {
                    webView.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
                    webView.NavigationCompleted += WebView_NavigationCompleted;
                }
                else
                {
                    MessageBox.Show("地图文件不存在", "错误");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化地图失败: {ex.Message}", "错误");
                Debug.WriteLine($"地图初始化错误: {ex}");
            }
        }

        private async void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess && !isMapInitialized)
            {
                isMapInitialized = true;
                await Task.Delay(500); // 等待地图完全加载
                await ShowClassroom();
            }
        }

        private async Task ShowClassroom()
        {
            try
            {
                // 添加教室到地图
                await AddClassroomToMap(_targetClassroom);

                // 高亮显示并定位到教室
                await HighlightAndZoomToClassroom(_targetClassroom);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"显示教室时出错: {ex}");
                MessageBox.Show("显示教室位置失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task AddClassroomToMap(ClassroomSpatial classroom)
        {
            try
            {
                var classroomData = new
                {
                    type = "Feature",
                    geometry = new
                    {
                        type = "Polygon",
                        coordinates = new[] { classroom.Shape.Coordinates.Select(coord =>
                            new double[] { coord.X, coord.Y }).ToArray() }
                    },
                    properties = new
                    {
                        classroomID = classroom.ClassroomID,
                        roomNumber = classroom.RoomNumber,
                        spatialLocation = classroom.SpatialLocation,
                        floor = classroom.Floor,
                        capacity = classroom.Capacity
                    }
                };

                string json = System.Text.Json.JsonSerializer.Serialize(classroomData);
                await webView.ExecuteScriptAsync($"mapFunctions.addClassroom({json});");
            }
            catch (Exception ex)
            {
                throw new Exception($"添加教室到地图时出错: {ex.Message}");
            }
        }

        private async Task HighlightAndZoomToClassroom(ClassroomSpatial classroom)
        {
            var script = $"mapFunctions.zoomToClassroom({classroom.ClassroomID});";
            await webView.ExecuteScriptAsync(script);
        }

        // 缩放控制按钮事件处理
        private async void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            await webView.ExecuteScriptAsync("map.zoomIn();");
        }

        private async void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            await webView.ExecuteScriptAsync("map.zoomOut();");
        }

        private async void BtnResetView_Click(object sender, RoutedEventArgs e)
        {
            await ShowClassroom();
        }
    }
}