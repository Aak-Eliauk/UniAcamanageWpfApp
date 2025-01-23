using Microsoft.EntityFrameworkCore;
using Microsoft.Web.WebView2.Core;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using UniAcamanageWpfApp.Data;
using UniAcamanageWpfApp.Models;
using static UniAcamanageWpfApp.Controls.CampusMapControl;

namespace UniAcamanageWpfApp.Windows
{
    public partial class ClassroomSelectorWindow : Window
    {
        private readonly CampusDbContext _context;
        private bool isMapInitialized = false;
        private ClassroomSpatial _selectedClassroom;
        private readonly DebounceDispatcher _searchDebouncer = new DebounceDispatcher();

        // 添加选中教室的属性
        public string SelectedClassroomNumber { get; private set; }

        public ClassroomSelectorWindow()
        {
            InitializeComponent();
            _context = new CampusDbContext();
            Loaded += ClassroomSelectorWindow_Loaded;
        }

        private async void ClassroomSelectorWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await InitializeWebView();
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
                await Task.Delay(500);
                await LoadAllClassrooms();

                // 添加消息处理
                webView.WebMessageReceived += WebView_WebMessageReceived;
            }
        }

        private async void WebView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                Debug.WriteLine($"Received message: {e.WebMessageAsJson}");
                var message = System.Text.Json.JsonSerializer.Deserialize<WebViewMessage>(e.WebMessageAsJson);

                if (message?.type == "classroom-selected" && message.classroom != null)
                {
                    var classroom = await _context.ClassroomSpatials
                        .FirstOrDefaultAsync(c => c.ClassroomID == message.classroom.classroomID);

                    if (classroom != null)
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            _selectedClassroom = classroom;
                            txtSelectedClassroom.Text = $"已选择: {classroom.RoomNumber} ({classroom.SpatialLocation})";
                            btnClearSelection.Visibility = Visibility.Visible;
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"处理WebView消息时出错: {ex.Message}");
                Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        private async void BtnClearSelection_Click(object sender, RoutedEventArgs e)
        {
            _selectedClassroom = null;
            txtSelectedClassroom.Text = "";
            btnClearSelection.Visibility = Visibility.Collapsed; // 隐藏取消选择按钮
            await webView.ExecuteScriptAsync("mapFunctions.resetHighlights();");
        }


        private async Task LoadAllClassrooms()
        {
            try
            {
                var classrooms = await _context.ClassroomSpatials.ToListAsync();
                foreach (var classroom in classrooms)
                {
                    await AddClassroomToMap(classroom);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载教室数据时出错: {ex}");
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
                Debug.WriteLine($"添加教室到地图时出错: {ex}");
            }
        }



        private async void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchDebouncer.Debounce(500, async () =>
            {
                string searchText = txtSearch.Text;
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    searchResultsList.Visibility = Visibility.Collapsed;
                    return;
                }

                var results = await _context.ClassroomSpatials
                    .Where(c => c.RoomNumber.Contains(searchText) ||
                               c.SpatialLocation.Contains(searchText))
                    .Select(c => new SearchResult
                    {
                        DisplayName = $"{c.RoomNumber} ({c.SpatialLocation})",
                        Category = "教室",
                        Classroom = c,
                        IconKind = "School"
                    })
                    .ToListAsync();

                searchResultsList.ItemsSource = results;
                searchResultsList.Visibility = results.Any() ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        private async void SearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (searchResultsList.SelectedItem is SearchResult result)
            {
                _selectedClassroom = result.Classroom;
                txtSelectedClassroom.Text = $"已选择: {result.DisplayName}";
                btnClearSelection.Visibility = Visibility.Visible; // 显示取消选择按钮
                searchResultsList.Visibility = Visibility.Collapsed;
                txtSearch.Text = result.DisplayName;

                await HighlightAndZoomToClassroom(_selectedClassroom);
            }
        }

        private async Task HighlightAndZoomToClassroom(ClassroomSpatial classroom)
        {
            try
            {
                await webView.ExecuteScriptAsync("mapFunctions.resetHighlights();");
                var script = $"mapFunctions.zoomToClassroom({classroom.ClassroomID});";
                await webView.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"高亮显示教室失败: {ex}");
            }
        }


        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedClassroom != null)
            {
                SelectedClassroomNumber = _selectedClassroom.RoomNumber;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("请先选择一个教室", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

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
            await webView.ExecuteScriptAsync("mapFunctions.resetView();");
        }


        // WebView消息类
        private class WebViewMessage
        {
            public string type { get; set; }
            public ClassroomInfo classroom { get; set; }
        }

        private class ClassroomInfo
        {
            public int classroomID { get; set; }
            public string roomNumber { get; set; }
            public string spatialLocation { get; set; }
            public int floor { get; set; }
            public int capacity { get; set; }
        }

        
    }

    public class SearchResult
    {
        public string DisplayName { get; set; }
        public string Category { get; set; }
        public string IconKind { get; set; }
        public ClassroomSpatial Classroom { get; set; }
    }


}