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
using UniAcamanageWpfApp.Data;
using UniAcamanageWpfApp.Models;
using NetTopologySuite.IO.Converters;
using System.Globalization;
using System.Net;

namespace UniAcamanageWpfApp.Controls
{
    public partial class CampusMapControl : UserControl
    {
        private readonly CampusDbContext _context;
        private readonly HttpClient _httpClient;
        private List<ClassroomSpatial> _allClassrooms;
        private bool isMapInitialized = false;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly System.Timers.Timer _searchDebounceTimer; // 明确指定 Timer 类型

        public CampusMapControl()
        {
            InitializeComponent();
            _context = new CampusDbContext();
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "UniAcamanageWpfApp");

            // 创建 JSON 序列化选项
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                Converters = { new GeoJsonConverterFactory() }  // 添加 GeoJSON 转换器
            };

            // 初始化定时器
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

        private void CampusMapControl_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeWebView();
        }

        private async void InitializeWebView()
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
                    MessageBox.Show($"找不到地图文件：{htmlPath}", "错误");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化地图失败: {ex.Message}", "错误");
                Debug.WriteLine($"初始化地图错误详情: {ex}");
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

        private async Task LoadClassroomsData()
        {
            try
            {
                // 从数据库加载教室数据
                _allClassrooms = await _context.ClassroomSpatials
                    .AsNoTracking()
                    .ToListAsync();

                // 更新教室列表
                UpdateClassroomsList(_allClassrooms);

                // 在地图上显示所有教室
                foreach (var classroom in _allClassrooms)
                {
                    await AddClassroomToMap(classroom);
                }

                // 调整地图视图以显示所有教室
                await webView.ExecuteScriptAsync("showAllClassrooms();");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载教室数据失败: {ex.Message}", "错误");
                Debug.WriteLine($"加载数据错误详情: {ex}");
            }
        }


        private void UpdateClassroomsList(IEnumerable<ClassroomSpatial> classrooms)
        {
            listBuildings.ItemsSource = classrooms.OrderBy(c => c.RoomNumber).ToList();
        }

        private async void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 重置定时器
            _searchDebounceTimer.Stop();

            string searchText = txtSearch.Text;
            if (string.IsNullOrWhiteSpace(searchText))
            {
                searchResultsList.Visibility = Visibility.Collapsed;
                return;
            }

            _searchDebounceTimer.Start();
        }

        private async Task HighlightClassrooms(List<ClassroomSpatial> classrooms)
        {
            try
            {
                await webView.ExecuteScriptAsync("resetClassroomStyles();");

                if (classrooms.Any())
                {
                    var ids = classrooms.Select(c => c.ClassroomID).ToList();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };
                    await webView.ExecuteScriptAsync($"highlightClassrooms({System.Text.Json.JsonSerializer.Serialize(ids, options)});");
                    await webView.ExecuteScriptAsync($"focusOnClassrooms({System.Text.Json.JsonSerializer.Serialize(ids, options)});");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"高亮教室失败: {ex.Message}");
            }
        }

        private async void SearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (searchResultsList.SelectedItem is SearchResult result)
            {
                if (result.IsClassroom)
                {
                    await HighlightClassrooms(new List<ClassroomSpatial> { result.Classroom });
                }
                else
                {
                    // 使用 JsonSerializer 来正确处理字符串转义
                    var popupContent = JsonSerializer.Serialize(WebUtility.HtmlDecode(result.DisplayName));
                    var script = $@"
                map.setView([{result.Latitude.ToString(CultureInfo.InvariantCulture)}, 
                           {result.Longitude.ToString(CultureInfo.InvariantCulture)}], 18);
                L.marker([{result.Latitude.ToString(CultureInfo.InvariantCulture)}, 
                         {result.Longitude.ToString(CultureInfo.InvariantCulture)}])
                 .bindPopup({popupContent})
                 .addTo(map)
                 .openPopup();";

                    await webView.ExecuteScriptAsync(script);
                }

                searchResultsList.Visibility = Visibility.Collapsed;
            }
        }

        private async Task AddClassroomToMap(ClassroomSpatial classroom)
        {
            try
            {
                var geoJson = new
                {
                    type = "Feature",
                    geometry = new
                    {
                        type = "Polygon",
                        coordinates = new[] { classroom.Shape.Coordinates.Select(c => new[] { c.X, c.Y }).ToArray() }
                    },
                    properties = new
                    {
                        classroom.ClassroomID,
                        Name = classroom.RoomNumber,
                        Description = $"楼层: {classroom.Floor}\n容量: {classroom.Capacity}人",
                        Location = classroom.SpatialLocation
                    }
                };

                string script = $"addClassroom({JsonSerializer.Serialize(geoJson, _jsonOptions)}, {JsonSerializer.Serialize(geoJson.properties, _jsonOptions)});";
                await webView.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"添加教室到地图失败: {ex.Message}");
            }
        }

        private class NominatimResult
        {
            public string display_name { get; set; }
            public string type { get; set; }
            public string lat { get; set; }
            public string lon { get; set; }
        }

        private async Task PerformSearch(string searchText)
        {
            try
            {
                var results = new List<SearchResult>();

                // 1. 搜索教室
                var classroomResults = _allClassrooms
                    .Where(c => c.RoomNumber.ToLower().Contains(searchText.ToLower()) ||
                               c.SpatialLocation.ToLower().Contains(searchText.ToLower()))
                    .Select(c => new SearchResult
                    {
                        DisplayName = c.RoomNumber,
                        Category = "教室",
                        IsClassroom = true,
                        Classroom = c
                    })
                    .ToList();

                results.AddRange(classroomResults);

                // 2. 搜索 OSM 数据
                var bbox = "114.3,30.4,114.7,30.5";
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
                    DisplayName = WebUtility.HtmlDecode(r.display_name), // 使用 HtmlDecode 解码中文
                    Category = r.type,
                    Latitude = double.Parse(r.lat, CultureInfo.InvariantCulture),
                    Longitude = double.Parse(r.lon, CultureInfo.InvariantCulture),
                    IsClassroom = false
                }));

                searchResultsList.ItemsSource = results;
                searchResultsList.Visibility = results.Any() ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"搜索失败: {ex.Message}");
                MessageBox.Show("搜索时发生错误，请稍后重试", "搜索错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private object GetCoordinatesFromWkt(string wkt)
        {
            try
            {
                var coordsText = wkt
                    .Replace("POLYGON((", "")
                    .Replace("))", "")
                    .Trim();

                var coordinates = coordsText
                    .Split(',')
                    .Select(coord =>
                    {
                        var parts = coord.Trim().Split(' ');
                        return new double[]
                        {
                    double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
                    double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture)
                        };
                    })
                    .ToArray();

                return new[] { coordinates }; // GeoJSON 多边形格式要求
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WKT 转换失败: {ex.Message}");
                return null;
            }
        }

        private async void CmbBuildingType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbBuildingType.SelectedItem is ComboBoxItem selectedItem)
            {
                string buildingType = selectedItem.Content.ToString();
                var filteredClassrooms = buildingType == "全部"
                    ? _allClassrooms
                    : _allClassrooms.Where(c => c.SpatialLocation.Contains(buildingType)).ToList();

                UpdateClassroomsList(filteredClassrooms);
                await HighlightClassrooms(filteredClassrooms);
            }
        }

        private async void ListBuildings_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listBuildings.SelectedItem is ClassroomSpatial selectedClassroom)
            {
                await webView.ExecuteScriptAsync($"zoomToClassroom({selectedClassroom.ClassroomID});");
            }
        }

        private async void BtnResetView_Click(object sender, RoutedEventArgs e)
        {
            await webView.ExecuteScriptAsync("resetView();");
        }

        private async void BtnShowAllBuildings_Click(object sender, RoutedEventArgs e)
        {
            await webView.ExecuteScriptAsync("showAllClassrooms();");
        }

        private async void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            await webView.ExecuteScriptAsync("map.zoomIn();");
        }

        private async void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            await webView.ExecuteScriptAsync("map.zoomOut();");
        }
    }
    // 添加 SearchResult 类定义
    public class SearchResult
    {
        public string DisplayName { get; set; }
        public string Category { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool IsClassroom { get; set; }
        public ClassroomSpatial Classroom { get; set; }
    }
}