using Microsoft.EntityFrameworkCore;
using Microsoft.Web.WebView2.Core;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives; 
using UniAcamanageWpfApp.Data;
using UniAcamanageWpfApp.Models;
using NetTopologySuite.IO.Converters;
using System.Globalization;
using System.Net;
using UniAcamanageWpfApp.Services;
using System.Text;
using Azure.Core.GeoJson;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Threading; // 用于 CancellationTokenSource
using System.Windows.Threading; // 用于 DispatcherTimer
using Style = System.Windows.Style;
using System.Text.Json.Serialization;


namespace UniAcamanageWpfApp.Controls
{
    public partial class CampusMapControl : UserControl
    {
        private readonly CampusDbContext _context;
        private readonly HttpClient _httpClient;
        private List<ClassroomSpatial> _allClassrooms;
        private bool isMapInitialized = false;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly System.Timers.Timer _searchDebounceTimer;

        public CampusMapControl()
        {
            InitializeComponent();

            // 初始化数据库上下文
            _context = new CampusDbContext();

            // 初始化 HTTP 客户端
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "UniAcamanageWpfApp");

            // 配置 JSON 序列化选项
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                Converters = { new GeoJsonConverterFactory() }
            };

            // 初始化搜索防抖定时器
            _searchDebounceTimer = new System.Timers.Timer(500);
            _searchDebounceTimer.Elapsed += async (s, e) =>
            {
                await Dispatcher.InvokeAsync(async () =>
                {
                    await PerformSearch(txtSearch.Text);
                });
            };
            _searchDebounceTimer.AutoReset = false;

            Loaded += CampusMapControl_Loaded;
        }

        private async void CampusMapControl_Loaded(object sender, RoutedEventArgs e)
        {
            await InitializeWebView();
            await LoadBuildingList();
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

        private async Task LoadBuildingList()
        {
            try
            {
                // 获取所有建筑物
                var buildings = await _context.ClassroomSpatials
                    .Select(c => c.SpatialLocation)
                    .Distinct()
                    .OrderBy(b => b)
                    .ToListAsync();

                // 更新建筑物下拉列表
                cmbBuilding.Items.Clear();
                cmbBuilding.Items.Add("全部");
                foreach (var building in buildings)
                {
                    cmbBuilding.Items.Add(building);
                }
                cmbBuilding.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载建筑列表失败: {ex.Message}", "错误");
            }
        }

        private async void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess && !isMapInitialized)
            {
                isMapInitialized = true;
                await Task.Delay(500); // 等待地图完全加载
                await LoadClassroomsData();
            }
        }

        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        private async Task LoadClassroomsData()
        {
            try
            {
                await _semaphore.WaitAsync();
                using (var context = new CampusDbContext()) // 创建新的 context 实例
                {
                    _allClassrooms = await context.ClassroomSpatials
                        .AsNoTracking()
                        .ToListAsync();
                }

                if (_allClassrooms != null && _allClassrooms.Any())
                {
                    InitializeBuildingComboBox();

                    foreach (var classroom in _allClassrooms)
                    {
                        await AddClassroomToMap(classroom);
                    }

                    await webView.ExecuteScriptAsync("mapFunctions.showAllClassrooms();");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载教室数据失败: {ex.Message}", "错误");
                Debug.WriteLine($"加载数据错误: {ex}");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task AddClassroomToMap(ClassroomSpatial classroom)
        {
            try
            {
                Debug.WriteLine($"Adding classroom to map: {classroom.RoomNumber}");

                var options = new JsonSerializerOptions
                {
                    NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
                    WriteIndented = true
                };

                // 处理坐标数据
                var cleanedCoordinates = classroom.Shape.Coordinates.Select(coord =>
                    new double[]
                    {
                double.IsInfinity(coord.X) || double.IsNaN(coord.X) ? 0.0 : coord.X,
                double.IsInfinity(coord.Y) || double.IsNaN(coord.Y) ? 0.0 : coord.Y
                    }
                ).ToArray();

                var classroomData = new
                {
                    type = "Feature",
                    geometry = new
                    {
                        type = "Polygon",
                        coordinates = new[] { cleanedCoordinates }
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

                string json = JsonSerializer.Serialize(classroomData, options);
                Debug.WriteLine($"Serialized classroom data: {json}");

                // 验证 JSON
                try
                {
                    JsonDocument.Parse(json);
                }
                catch (JsonException ex)
                {
                    throw new Exception($"生成的 JSON 无效: {ex.Message}");
                }

                await webView.ExecuteScriptAsync($"mapFunctions.addClassroom({json});");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"添加教室到地图时出错: {ex}");
                MessageBox.Show($"添加教室到地图时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchDebounceTimer.Stop();

            string searchText = txtSearch.Text;
            if (string.IsNullOrWhiteSpace(searchText))
            {
                searchResultsList.Visibility = Visibility.Collapsed;
                return;
            }

            _searchDebounceTimer.Start();
        }

        private async Task PerformSearch(string searchText)
        {
            try
            {
                var results = new List<SearchResult>();

                // 1. 搜索教室
                var classroomResults = _allClassrooms
                    .Where(c => c.RoomNumber.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                               c.SpatialLocation.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    .Select(c => new SearchResult
                    {
                        DisplayName = $"{c.RoomNumber} ({c.SpatialLocation})",
                        Category = "教室",
                        IsClassroom = true,
                        Classroom = c,
                        IconKind = "School" // MaterialDesign 图标
                    })
                    .ToList();

                results.AddRange(classroomResults);

                // 2. 搜索 OSM 数据
                var bbox = "114.3,30.4,114.7,30.5"; // 武汉未来城区域范围
                var nominatimUrl = $"https://nominatim.openstreetmap.org/search" +
                    $"?q={Uri.EscapeDataString(searchText)}" +
                    $"&format=json" +
                    $"&viewbox={bbox}" +
                    $"&bounded=1" +
                    $"&limit=10";

                var response = await _httpClient.GetStringAsync(nominatimUrl);
                var osmResults = JsonSerializer.Deserialize<List<NominatimResult>>(response);

                results.AddRange(osmResults.Select(r => new SearchResult
                {
                    DisplayName = WebUtility.HtmlDecode(r.display_name),
                    Category = TranslateOsmType(r.type),
                    Latitude = double.Parse(r.lat, CultureInfo.InvariantCulture),
                    Longitude = double.Parse(r.lon, CultureInfo.InvariantCulture),
                    IsClassroom = false,
                    IconKind = GetIconForOsmType(r.type)
                }));

                // 更新搜索结果列表
                searchResultsList.ItemsSource = results;
                searchResultsList.Visibility = results.Any() ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"搜索失败: {ex.Message}");
                MessageBox.Show("搜索时发生错误，请稍后重试", "搜索错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // OSM 类型转换为中文
        private string TranslateOsmType(string type)
        {
            return type switch
            {
                "amenity" => "设施",
                "building" => "建筑",
                "highway" => "道路",
                "leisure" => "休闲场所",
                "shop" => "商店",
                _ => type
            };
        }

        // 根据 OSM 类型获取对应的 MaterialDesign 图标
        private string GetIconForOsmType(string type)
        {
            return type switch
            {
                "amenity" => "Store",
                "building" => "Building",
                "highway" => "Road",
                "leisure" => "Park",
                "shop" => "Shop",
                _ => "MapMarker"
            };
        }

        private class NominatimResult
        {
            public string display_name { get; set; }
            public string type { get; set; }
            public string lat { get; set; }
            public string lon { get; set; }
        }

        // 修改 SearchResult 类，添加图标属性
        public class SearchResult
        {
            public string DisplayName { get; set; }
            public string Category { get; set; }
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public bool IsClassroom { get; set; }
            public ClassroomSpatial Classroom { get; set; }
            public string IconKind { get; set; }
        }

        // 搜索结果选中处理
        private async void SearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (searchResultsList.SelectedItem is SearchResult result)
            {
                try
                {
                    // 清除之前的搜索标记
                    await webView.ExecuteScriptAsync("mapFunctions.clearSearchMarkers();");

                    if (result.IsClassroom && result.Classroom != null)
                    {
                        // 处理教室搜索结果
                        await HighlightAndZoomToClassroom(result.Classroom);
                    }
                    else
                    {
                        // 处理 OSM 位置搜索结果
                        await ZoomToLocation(result.Latitude, result.Longitude);
                        // 添加搜索结果标记
                        await AddSearchMarker(result.Latitude, result.Longitude, result.DisplayName);
                    }

                    // 隐藏搜索结果列表
                    searchResultsList.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"处理搜索结果选择时出错: {ex}");
                    MessageBox.Show("无法定位到选中的位置", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async Task AddSearchMarker(double lat, double lon, string title)
        {
            var script = $@"mapFunctions.addSearchMarker({lat}, {lon}, '{title.Replace("'", "\\'")}');";
            await webView.ExecuteScriptAsync(script);
        }

        private async Task HighlightAndZoomToClassroom(ClassroomSpatial classroom)
        {
            try
            {
                Debug.WriteLine($"开始定位教室: ID={classroom.ClassroomID}, RoomNumber={classroom.RoomNumber}");

                var script = @"
        (function() {
            try {
                console.log('开始定位教室，接收到的ID:', " + classroom.ClassroomID + @");
                console.log('当前地图图层数量:', MapState.buildingsLayer.getLayers().length);
                
                // 重置高亮显示
                mapFunctions.resetHighlights();
                
                let found = false;
                MapState.buildingsLayer.eachLayer(layer => {
                    console.log('检查图层:', {
                        'layer': layer,
                        'feature': layer.feature,
                        'properties': layer.feature ? layer.feature.properties : null,
                        'currentId': layer.feature ? layer.feature.properties.classroomID : null
                    });

                    // 修改比较逻辑，同时检查数字和字符串形式
                    const layerId = layer.feature?.properties?.classroomID;
                    const targetId = " + classroom.ClassroomID + @";
                    console.log('比较ID:', layerId, targetId);

                    if (layer.feature && (layerId == targetId)) {
                        console.log('找到匹配的教室');
                        found = true;
                        
                        // 高亮样式
                        const originalStyle = {
                            color: '#1e88e5',
                            weight: 2,
                            fillColor: '#1e88e5',
                            fillOpacity: 0.3
                        };
                        
                        const highlightStyle = {
                            color: '#f44336',
                            weight: 3,
                            fillColor: '#f44336',
                            fillOpacity: 0.5
                        };
                        
                        // 闪烁效果
                        let count = 0;
                        const flash = setInterval(() => {
                            layer.setStyle(count % 2 === 0 ? highlightStyle : originalStyle);
                            console.log('闪烁效果:', count);
                            count++;
                            if (count > 5) {
                                clearInterval(flash);
                                layer.setStyle(highlightStyle);
                            }
                        }, 200);
                        
                        // 获取边界并记录
                        const bounds = layer.getBounds();
                        console.log('教室边界:', bounds);
                        
                        // 平滑缩放
                        MapState.map.flyToBounds(bounds, {
                            padding: [50, 50],
                            maxZoom: 19,
                            duration: 1
                        });
                        
                        // 显示信息窗口
                        setTimeout(() => {
                            console.log('打开信息窗口');
                            layer.openPopup();
                        }, 1000);
                    }
                });
                
                console.log('教室查找结果:', found ? '成功' : '未找到');
                return found;
            } catch (error) {
                console.error('定位教室时发生错误:', error);
                return false;
            }
        })();";

                // 先输出调试信息
                await webView.ExecuteScriptAsync("console.log('MapState状态:', MapState);");
                await webView.ExecuteScriptAsync("console.log('buildingsLayer状态:', MapState.buildingsLayer);");

                // 执行主脚本
                var result = await webView.ExecuteScriptAsync(script);
                Debug.WriteLine($"定位教室执行结果: {result}");

                if (result.ToLower() == "false")
                {
                    Debug.WriteLine($"未找到教室: {classroom.ClassroomID}");
                    MessageBox.Show($"未能找到教室 {classroom.RoomNumber}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"高亮并缩放到教室时出错: {ex}");
                Debug.WriteLine($"异常详情: {ex.ToString()}");
                MessageBox.Show($"定位教室时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ZoomToLocation(double latitude, double longitude)
        {
            try
            {
                var script = $"mapFunctions.focusOnLocation({latitude}, {longitude}, 18);";
                await webView.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"缩放到位置时出错: {ex}");
                throw;
            }
        }

        private async Task HighlightClassrooms(List<ClassroomSpatial> classrooms)
        {
            try
            {
                // 首先重置所有教室的样式
                await webView.ExecuteScriptAsync("resetClassroomStyles();");

                if (classrooms.Any())
                {
                    // 获取要高亮显示的教室ID列表
                    var ids = classrooms.Select(c => c.ClassroomID).ToList();

                    // 序列化ID列表
                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };

                    // 调用JavaScript方法高亮显示选中的教室
                    await webView.ExecuteScriptAsync($"highlightClassrooms({JsonSerializer.Serialize(ids, options)});");

                    // 调整地图视图以显示选中的教室
                    await webView.ExecuteScriptAsync($"focusOnClassrooms({JsonSerializer.Serialize(ids, options)});");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"高亮显示教室失败: {ex.Message}");
            }
        }

        private async void CmbBuilding_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (cmbBuilding.SelectedItem == null || _allClassrooms == null)
                {
                    return; // 如果没有选中项或没有教室数据，直接返回
                }

                string selectedBuilding = cmbBuilding.SelectedItem.ToString();

                // 确保不会有空值进入 GroupBy
                var filteredClassrooms = _allClassrooms
                    .Where(c => !string.IsNullOrEmpty(c.SpatialLocation))
                    .ToList();

                if (selectedBuilding != "全部")
                {
                    // 按选定建筑物筛选
                    filteredClassrooms = filteredClassrooms
                        .Where(c => c.SpatialLocation == selectedBuilding)
                        .ToList();
                }

                // 按建筑物分组
                var groupedClassrooms = filteredClassrooms
                    .GroupBy(c => c.SpatialLocation ?? "未知位置") // 处理可能的空值
                    .Select(g => new
                    {
                        Building = g.Key,
                        Classrooms = g.OrderBy(c => c.Floor)
                                     .ThenBy(c => c.RoomNumber)
                                     .ToList()
                    })
                    .OrderBy(g => g.Building)
                    .ToList();

                listClassrooms.ItemsSource = groupedClassrooms;

                // 更新地图显示
                await webView.ExecuteScriptAsync("mapFunctions.resetHighlights();"); // 重置高亮显示

                if (selectedBuilding != "全部")
                {
                    var classroomsToHighlight = filteredClassrooms.Select(c => c.ClassroomID).ToList();
                    if (classroomsToHighlight.Any())
                    {
                        await webView.ExecuteScriptAsync($"mapFunctions.highlightClassrooms({JsonSerializer.Serialize(classroomsToHighlight)});");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载教室数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"教室数据加载错误: {ex}");
            }
        }

        // 添加初始化建筑物下拉列表的方法
        private void InitializeBuildingComboBox()
        {
            try
            {
                // 清空当前项
                cmbBuilding.ItemsSource = null;
                cmbBuilding.Items.Clear();

                if (_allClassrooms == null || !_allClassrooms.Any())
                {
                    return;
                }

                var buildings = new List<string> { "全部" };
                buildings.AddRange(_allClassrooms
                    .Where(c => !string.IsNullOrEmpty(c.SpatialLocation))
                    .Select(c => c.SpatialLocation)
                    .Distinct()
                    .OrderBy(b => b));

                cmbBuilding.ItemsSource = buildings;
                cmbBuilding.SelectedIndex = 0; // 默认选择"全部"
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化建筑物列表失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"建筑物列表初始化错误: {ex}");
            }
        }

        private async void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 清空搜索框
                txtSearch.Text = string.Empty;

                // 隐藏搜索结果列表
                searchResultsList.Visibility = Visibility.Collapsed;

                // 清除地图上的搜索标注
                await webView.ExecuteScriptAsync("mapFunctions.clearSearchMarkers();");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清除搜索时出错: {ex}");
                MessageBox.Show("清除搜索失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnLocateClassroom_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var classroom = button?.DataContext as ClassroomSpatial;

                if (classroom != null)
                {
                    Debug.WriteLine($"尝试定位教室: ID={classroom.ClassroomID}, RoomNumber={classroom.RoomNumber}");

                    // 先重置所有高亮显示
                    await webView.ExecuteScriptAsync("mapFunctions.resetHighlights();");

                    // 然后定位到新的教室
                    var script = $"mapFunctions.zoomToClassroom({classroom.ClassroomID});";
                    Debug.WriteLine($"执行脚本: {script}");
                    await webView.ExecuteScriptAsync(script);
                }
                else
                {
                    Debug.WriteLine("无法获取教室信息");
                    MessageBox.Show("无法获取教室信息", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"定位教室时出错: {ex}");
                MessageBox.Show($"定位教室时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
            await webView.ExecuteScriptAsync("resetView();");
        }

        private async void BtnShowAllBuildings_Click(object sender, RoutedEventArgs e)
        {
            await webView.ExecuteScriptAsync("showAllClassrooms();");
        }
    }
}