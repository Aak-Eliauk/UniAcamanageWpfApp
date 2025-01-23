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


namespace UniAcamanageWpfApp.Controls
{
    public partial class CampusMapControl : UserControl
    {
        #region
        private readonly CampusDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly MapSearchService _searchService;
        private readonly NavigationService _navigationService;
        private readonly CourseLocationService _courseLocationService;

        private List<ClassroomSpatial> _allClassrooms;
        private bool isMapInitialized = false;
        private bool isSelectingStartPoint = false;  // 导航起点选择状态
        private bool isSelectingEndPoint = false;    // 导航终点选择状态
        private NavigationPoint startPoint;          // 导航起点
        private NavigationPoint endPoint;            // 导航终点

        private readonly JsonSerializerOptions _jsonOptions;
        private System.Timers.Timer _searchDebounceTimer = null;
        private DispatcherTimer _courseUpdateTimer = null;

        private GeoJsonFeatureCollection _currentRoute;  // 当前显示的导航路线
        #endregion

        #region 初始化
        public CampusMapControl()
        {
            InitializeComponent();

            // 初始化上下文和HTTP客户端
            _context = new CampusDbContext();
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "UniAcamanageWpfApp");

            // 初始化服务
            _searchService = new MapSearchService(_httpClient, _context);
            _navigationService = new NavigationService(_httpClient);
            _courseLocationService = new CourseLocationService(_context);

            // JSON序列化配置
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                Converters = { new GeoJsonConverterFactory() }
            };

            // 初始化定时器
            InitializeTimers();
            InitializeEventHandlers();

            Loaded += CampusMapControl_Loaded;
            Unloaded += CampusMapControl_Unloaded;
        }

        private void InitializeTimers()
        {
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

            // 初始化课程更新定时器
            _courseUpdateTimer = new DispatcherTimer();
            _courseUpdateTimer.Interval = TimeSpan.FromMinutes(1);
            _courseUpdateTimer.Tick += async (s, e) => await UpdateCurrentCourses();
            _courseUpdateTimer.Start();
        }

        private StringBuilder _messageBuffer = new StringBuilder(); // 添加在类级别声明

        private void InitializeEventHandlers()
        {
            // 地图点击事件处理
            webView.WebMessageReceived += async (s, e) =>
            {
                try
                {
                    HandleWebViewMessage(e);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"处理地图消息失败: {ex.Message}");
                }
            };
        }

        private void HandleWebViewMessage(CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var messageJson = e.WebMessageAsJson;  // 获取原始JSON字符串
                ProcessWebViewMessage(messageJson);     // 直接传递JSON字符串
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling web message: {ex.Message}");
            }
        }

        private void ProcessWebViewMessage(string messageJson)
        {
            try
            {
                var message = JsonSerializer.Deserialize<WebMessage>(messageJson);

                switch (message.Type)
                {
                    case "classroom-click":
                        HandleClassroomClick(message.Data).Wait();
                        break;
                    case "search-result-click":
                        HandleSearchResultClick(message.Data).Wait();
                        break;
                    case "mapClick":
                        if (message.Data != null)
                        {
                            var clickData = JsonSerializer.Deserialize<MapClickMessage>(message.Data.ToString());
                            HandleMapClick(clickData).Wait();
                        }
                        break;
                    case "location":
                        // 处理位置信息
                        break;
                    case "error":
                        Debug.WriteLine($"从地图收到错误: {message.Data}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"处理消息失败: {ex.Message}");
            }
        }

        private bool IsValidJson(string strInput)
        {
            if (string.IsNullOrEmpty(strInput)) return false;
            try
            {
                JsonDocument.Parse(strInput);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task HandleSearchResultClick(object data)
        {
            try
            {
                var searchResult = JsonSerializer.Deserialize<SearchResultClick>(data.ToString());
                if (searchResult == null)
                {
                    Debug.WriteLine("Search result data is null");
                    return;
                }

                Debug.WriteLine($"Handling search result click: {searchResult.name}");

                if (searchResult.type == "classroom")
                {
                    // 处理教室点击
                    var classroom = await _context.ClassroomSpatials
                        .FirstOrDefaultAsync(c => c.ClassroomID == searchResult.id);

                    if (classroom != null)
                    {
                        await AddClassroomToMap(classroom);
                        await webView.ExecuteScriptAsync(
                            $"focusOnLocation({classroom.CenterPoint.Y}, {classroom.CenterPoint.X}, 19);"
                        );
                    }
                }
                else
                {
                    // 处理其他设施点击（如图书馆、食堂等）
                    double latitude = 0, longitude = 0;
                    if (searchResult.coordinates != null && searchResult.coordinates.Length >= 2)
                    {
                        latitude = searchResult.coordinates[1];
                        longitude = searchResult.coordinates[0];
                    }

                    await webView.ExecuteScriptAsync(
                        $"focusOnLocation({latitude}, {longitude}, 19);"
                    );
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"处理搜索结果点击失败: {ex.Message}");
            }
        }

        private async Task HandleClassroomClick(object data)
        {
            try
            {
                var classroomInfo = JsonSerializer.Deserialize<ClassroomClickInfo>(data.ToString());
                // 可以在这里添加更多的处理逻辑
                Debug.WriteLine($"Clicked classroom: {classroomInfo.building} {classroomInfo.name}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"处理教室点击失败: {ex.Message}");
            }
        }

        private class ClassroomClickInfo
        {
            public int classroomId { get; set; }
            public string name { get; set; }
            public string building { get; set; }
            public int floor { get; set; }
        }

        private void CampusMapControl_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeWebView();
        }

        private void CampusMapControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _searchDebounceTimer?.Dispose();
            _courseUpdateTimer?.Stop();
            _httpClient?.Dispose();
        }

        private async void InitializeWebView()
        {
            try
            {
                await webView.EnsureCoreWebView2Async();

                // 注入JavaScript通信接口
                await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                window.chrome.webview.postMessage = function(data) {
                    window.chrome.webview.postMessage(JSON.stringify(data));
                };
            ");

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
                try
                {
                    isMapInitialized = true;
                    await Task.Delay(1000); // 等待地图完全加载

                    // 确保地图函数已经初始化
                    await webView.ExecuteScriptAsync("if (typeof window.mapFunctions === 'undefined') { window.mapFunctions = {}; }");

                    // 加载教室数据
                    await InitializeMapData();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"地图初始化失败: {ex.Message}");
                    MessageBox.Show("初始化地图时发生错误，请刷新页面重试", "错误");
                }
            }
            else if (!e.IsSuccess)
            {
                Debug.WriteLine($"地图加载失败: {e.WebErrorStatus}");
            }
        }

        private async Task InitializeMapData()
        {
            try
            {
                Debug.WriteLine("开始初始化地图数据");
                _allClassrooms = await _context.ClassroomSpatials
                    .AsNoTracking()
                    .ToListAsync();

                // 更新建筑物下拉列表
                var buildings = _allClassrooms
                    .Select(c => c.SpatialLocation)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();

                await Dispatcher.InvokeAsync(() =>
                {
                    InitializeBuildingComboBox(buildings);
                    UpdateClassroomsList(_allClassrooms);
                });

                foreach (var classroom in _allClassrooms)
                {
                    await AddClassroomToMap(classroom);
                }

                await webView.ExecuteScriptAsync("showAllClassrooms();");
                Debug.WriteLine($"成功加载 {_allClassrooms.Count} 个教室");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"初始化地图数据失败: {ex.Message}");
                MessageBox.Show("加载地图数据失败，请检查数据库连接", "错误");
            }
        }
        #endregion

        #region Data Loading Methods
        private async Task AddClassroomToMap(ClassroomSpatial classroom)
        {
            try
            {
                if (classroom?.Shape == null || classroom.Shape.Coordinates == null)
                {
                    Debug.WriteLine($"Invalid classroom data for ID: {classroom?.ClassroomID}");
                    return;
                }

                // 检查并过滤无效坐标
                var validCoordinates = classroom.Shape.Coordinates
                    .Where(c => c != null &&
                               !double.IsNaN(c.X) && !double.IsNaN(c.Y) &&
                               !double.IsInfinity(c.X) && !double.IsInfinity(c.Y))
                    .Select(c => new[] { c.X, c.Y })
                    .ToList();

                if (validCoordinates.Count < 3)
                {
                    Debug.WriteLine($"Not enough valid coordinates for classroom {classroom.RoomNumber}");
                    return;
                }

                // 创建简化的 GeoJSON 对象
                var geoJson = new
                {
                    type = "Feature",
                    geometry = new
                    {
                        type = "Polygon",
                        coordinates = new[] { validCoordinates }
                    },
                    properties = new
                    {
                        id = classroom.ClassroomID,
                        name = classroom.RoomNumber ?? "未命名教室",
                        building = classroom.SpatialLocation ?? "未知位置",
                        floor = classroom.Floor
                    }
                };

                // 将对象转换为 JSON 字符串，使用自定义选项
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                };

                var jsonString = JsonSerializer.Serialize(geoJson, jsonOptions);

                // 检查 JSON 字符串的长度
                if (jsonString.Length > 0)
                {
                    // 使用转义后的 JSON 字符串
                    var script = $"window.mapFunctions.addClassroom({jsonString});";
                    await webView.ExecuteScriptAsync(script);
                    Debug.WriteLine($"Successfully added classroom {classroom.RoomNumber} to map");
                }
                else
                {
                    Debug.WriteLine($"Empty JSON string for classroom {classroom.RoomNumber}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error adding classroom to map: {ex.Message}\nStack trace: {ex.StackTrace}");
            }
        }


        private async Task HighlightClassrooms(List<ClassroomSpatial> classrooms)
        {
            try
            {
                var features = classrooms.Select(c => new GeoJsonFeature
                {
                    Type = "Feature",
                    Geometry = CreateClassroomGeometry(c),
                    Properties = new Dictionary<string, object>
                    {
                        ["id"] = c.ClassroomID,
                        ["name"] = c.RoomNumber,
                        ["building"] = c.SpatialLocation,
                        ["floor"] = c.Floor,
                        ["type"] = "highlighted"
                    }
                }).ToList();

                var featureCollection = new GeoJsonFeatureCollection
                {
                    Type = "FeatureCollection",
                    Features = features
                };

                await webView.ExecuteScriptAsync(
                    $"highlightClassrooms({JsonSerializer.Serialize(featureCollection, _jsonOptions)});"
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"高亮显示教室失败: {ex.Message}");
            }
        }

        private void FloorFilter_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radioButton)
            {
                string floor = radioButton.Content.ToString()
                    .Replace("楼", "")
                    .Replace("全部", "");

                var building = cmbBuilding.SelectedItem?.ToString();
                if (building == null) return;

                var filteredClassrooms = _allClassrooms
                    .Where(c => c.SpatialLocation == building &&
                               (string.IsNullOrEmpty(floor) || c.Floor.ToString() == floor))
                    .ToList();

                UpdateClassroomsList(filteredClassrooms);
            }
        }

        private async Task LoadClassroomsData()
        {
            try
            {
                _allClassrooms = await _context.ClassroomSpatials
                    .AsNoTracking()
                    .ToListAsync();

                var buildings = _allClassrooms
                    .Select(c => c.SpatialLocation)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();

                InitializeBuildingComboBox(buildings);
                UpdateClassroomsList(_allClassrooms);

                foreach (var classroom in _allClassrooms)
                {
                    await AddClassroomToMap(classroom);
                }

                await webView.ExecuteScriptAsync("showAllClassrooms();");
            }
            catch (Exception ex)
            {
                throw new Exception($"加载教室数据失败: {ex.Message}", ex);
            }
        }

        private async Task LoadCurrentCourses()
        {
            try
            {
                var currentCourses = await _courseLocationService.GetCurrentLocationCourses();
                var upcomingCourses = await _courseLocationService.GetUpcomingLocationCourses();

                await Dispatcher.InvokeAsync(() =>
                {
                    currentClassList.ItemsSource = currentCourses;
                    upcomingClassList.ItemsSource = upcomingCourses;
                });

                // 高亮显示当前课程的教室
                if (currentCourses.Any())
                {
                    var currentClassrooms = currentCourses
                        .Where(c => c.Classroom != null)
                        .Select(c => c.Classroom)
                        .ToList();

                    await HighlightClassrooms(currentClassrooms);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载课程数据失败: {ex.Message}");
            }
        }
        #endregion

        #region UI Update Methods
        private void InitializeBuildingComboBox(List<string> buildings)
        {
            cmbBuilding.ItemsSource = new[] { "全部" }.Concat(buildings);
            cmbBuilding.SelectedIndex = 0;
        }

        private void UpdateClassroomsList(IEnumerable<ClassroomSpatial> classrooms)
        {
            try
            {
                var groupedClassrooms = classrooms
                    .GroupBy(c => c.SpatialLocation)
                    .Select(g => new BuildingClassrooms
                    {
                        Building = g.Key,
                        Classrooms = g.OrderBy(c => c.Floor)
                                     .ThenBy(c => c.RoomNumber)
                                     .ToList()
                    })
                    .ToList();

                listClassrooms.ItemsSource = groupedClassrooms;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"更新教室列表失败: {ex.Message}");
            }
        }

        private void UpdateFloorFilter(string building)
        {
            if (building == "全部")
            {
                floorFilterPanel.Visibility = Visibility.Collapsed;
                return;
            }

            var floors = _allClassrooms
                .Where(c => c.SpatialLocation == building)
                .Select(c => c.Floor)
                .Distinct()
                .OrderBy(f => f)
                .ToList();

            floorFilterPanel.Children.Clear();

            // 添加"全部"选项
            var allFloorsRadio = new RadioButton
            {
                Content = "全部",
                Style = FindResource("MaterialDesignChoiceChipPrimaryOutlineRadioButton") as Style,
                IsChecked = true,
                Margin = new Thickness(0, 0, 8, 0)
            };
            floorFilterPanel.Children.Add(allFloorsRadio);

            // 添加各楼层选项
            foreach (var floor in floors)
            {
                var radio = new RadioButton
                {
                    Content = $"{floor}楼",
                    Style = FindResource("MaterialDesignChoiceChipPrimaryOutlineRadioButton") as Style,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                radio.Checked += FloorFilter_Changed;
                floorFilterPanel.Children.Add(radio);
            }

            floorFilterPanel.Visibility = Visibility.Visible;
        }

        private async Task UpdateRouteDisplay(NavigationResult route)
        {
            if (route == null) return;

            // 创建路线的GeoJSON
            _currentRoute = new GeoJsonFeatureCollection
            {
                Features = new List<GeoJsonFeature>
            {
                new GeoJsonFeature
                {
                    Type = "Feature",
                    Geometry = new GeoJsonGeometry
                    {
                        Type = "LineString",
                        Coordinates = route.RouteGeometry
                    },
                    Properties = new Dictionary<string, object>
                    {
                        ["type"] = "route",
                        ["mode"] = route.Mode
                    }
                }
            }
            };

            // 添加路线点标记
            if (startPoint != null)
            {
                _currentRoute.Features.Add(CreatePointFeature(startPoint, "start"));
            }
            if (endPoint != null)
            {
                _currentRoute.Features.Add(CreatePointFeature(endPoint, "end"));
            }

            // 在地图上显示路线
            await webView.ExecuteScriptAsync($"showRoute({JsonSerializer.Serialize(_currentRoute, _jsonOptions)});");

            // 更新路线信息面板
            UpdateRouteInfo(route);
        }

        private void UpdateRouteInfo(NavigationResult route)
        {
            var duration = TimeSpan.FromSeconds(route.TotalDuration);
            var distance = route.TotalDistance / 1000; // 转换为公里

            var routeInfo = new StringBuilder();
            routeInfo.AppendLine($"总距离: {distance:F2} 公里");
            routeInfo.AppendLine($"预计用时: {duration:hh\\:mm\\:ss}");
            routeInfo.AppendLine();
            routeInfo.AppendLine("导航指引:");

            foreach (var step in route.Steps)
            {
                routeInfo.AppendLine($"- {step.Instruction} ({step.Distance:F0}米)");
            }

            txtRouteInfo.Text = routeInfo.ToString();
        }
        #endregion

        #region Search Methods
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
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    searchResultsList.Visibility = Visibility.Collapsed;
                    return;
                }

                var results = new List<SearchResult>();

                // 搜索教室
                var classroomResults = _allClassrooms
                    .Where(c => (c.RoomNumber?.ToLower().Contains(searchText.ToLower()) ?? false) ||
                               (c.SpatialLocation?.ToLower().Contains(searchText.ToLower()) ?? false))
                    .Take(10)
                    .Select(c => new SearchResult
                    {
                        DisplayName = $"{c.SpatialLocation} {c.RoomNumber}",
                        Category = "教室",
                        IsClassroom = true,
                        Classroom = c,
                        IconKind = "School"
                    });

                results.AddRange(classroomResults);

                // 更新搜索结果
                await Dispatcher.InvokeAsync(() =>
                {
                    searchResultsList.ItemsSource = results;
                    searchResultsList.Visibility = results.Any() ? Visibility.Visible : Visibility.Collapsed;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"搜索失败: {ex.Message}");
            }
        }

        private string GetFacilityType(string searchText)
        {
            if (searchText.Contains("教学楼")) return "教学楼";
            if (searchText.Contains("学院")) return "学院";
            if (searchText.Contains("实验室")) return "实验室";
            if (searchText.Contains("图书馆")) return "图书馆";
            if (searchText.Contains("体育") || searchText.Contains("体育馆")) return "体育";
            if (searchText.Contains("食堂")) return "食堂";
            if (searchText.Contains("医院") || searchText.Contains("医疗")) return "医院";
            if (searchText.Contains("宿舍") || searchText.Contains("寝室")) return "学生宿舍";
            return null;
        }

        private async Task<List<SearchResult>> SearchFacilitiesByType(string facilityType)
        {
            try
            {
                var bbox = "114.3,30.4,114.7,30.5"; // 武汉地区边界
                var nominatimUrl = $"https://nominatim.openstreetmap.org/search" +
                    $"?q={Uri.EscapeDataString(facilityType)}" +
                    $"&format=json" +
                    $"&viewbox={bbox}" +
                    $"&bounded=1" +
                    $"&limit=10";

                var response = await _httpClient.GetStringAsync(nominatimUrl);
                var osmResults = JsonSerializer.Deserialize<List<NominatimResult>>(response);

                return osmResults.Select(r => new SearchResult
                {
                    DisplayName = WebUtility.HtmlDecode(r.display_name),
                    Category = facilityType,
                    Latitude = double.Parse(r.lat, CultureInfo.InvariantCulture),
                    Longitude = double.Parse(r.lon, CultureInfo.InvariantCulture),
                    IsClassroom = false,
                    IconKind = GetFacilityIcon(facilityType)
                }).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设施搜索失败: {ex.Message}");
                return new List<SearchResult>();
            }
        }

        private string GetFacilityIcon(string facilityType)
        {
            return facilityType switch
            {
                "教学楼" => "School",
                "学院" => "AccountMultiple",
                "实验室" => "Flask",
                "图书馆" => "Library",
                "体育" => "SportsHandball",
                "食堂" => "FoodForkDrink",
                "医院" => "Hospital",
                "学生宿舍" => "Home",
                _ => "MapMarker"
            };
        }

        // 翻译设施类型
        private string TranslateOsmType(string facilityType)
        {
            return facilityType switch
            {
                "教学楼" => "教学楼",
                "学院" => "学院",
                "实验室" => "实验室",
                "图书馆" => "图书馆",
                "体育" => "体育设施",
                "食堂" => "食堂",
                "医院" => "医疗设施",
                "学生宿舍" => "学生宿舍",
                _ => facilityType
            };
        }


        private string GetSelectedFacilityType()
        {
            var selectedButton = facilityTypePanel.Children
                .OfType<ToggleButton>()
                .FirstOrDefault(b => b.IsChecked == true);

            return selectedButton?.Tag as string;
        }
        #endregion

        #region Navigation Methods
        private async void BtnSelectStart_Click(object sender, RoutedEventArgs e)
        {
            isSelectingStartPoint = true;
            isSelectingEndPoint = false;

            await webView.ExecuteScriptAsync("enableLocationPicking('start');");
            statusText.Text = "请在地图上点击选择起点位置";
            btnSelectStart.IsEnabled = false;
        }

        private async void BtnSelectEnd_Click(object sender, RoutedEventArgs e)
        {
            isSelectingStartPoint = false;
            isSelectingEndPoint = true;

            await webView.ExecuteScriptAsync("enableLocationPicking('end');");
            statusText.Text = "请在地图上点击选择终点位置";
            btnSelectEnd.IsEnabled = false;
        }

        private async void BtnPlanRoute_Click(object sender, RoutedEventArgs e)
        {
            if (startPoint == null || endPoint == null)
            {
                MessageBox.Show("请先选择起点和终点", "提示");
                return;
            }

            try
            {
                string mode = rbWalking.IsChecked == true ? "foot" : "bicycle";
                var route = await _navigationService.GetRoute(startPoint, endPoint, mode);

                if (route != null)
                {
                    await UpdateRouteDisplay(route);
                    tabNavigation.IsSelected = true; // 切换到导航结果标签
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"路径规划失败: {ex.Message}", "错误");
                Debug.WriteLine($"路径规划错误详情: {ex}");
            }
        }

        private async Task HandleMapClick(MapClickMessage message)
        {
            if (!isSelectingStartPoint && !isSelectingEndPoint)
                return;

            try
            {
                var clickedPoint = new NavigationPoint
                {
                    Latitude = message.Latitude,
                    Longitude = message.Longitude,
                    Name = await GetLocationName(message.Latitude, message.Longitude)
                };

                if (isSelectingStartPoint)
                {
                    startPoint = clickedPoint;
                    txtStartPoint.Text = startPoint.Name;
                    isSelectingStartPoint = false;
                    btnSelectStart.IsEnabled = true;
                    await webView.ExecuteScriptAsync($"addMarker('start', {message.Latitude}, {message.Longitude});");
                }
                else if (isSelectingEndPoint)
                {
                    endPoint = clickedPoint;
                    txtEndPoint.Text = endPoint.Name;
                    isSelectingEndPoint = false;
                    btnSelectEnd.IsEnabled = true;
                    await webView.ExecuteScriptAsync($"addMarker('end', {message.Latitude}, {message.Longitude});");
                }

                await webView.ExecuteScriptAsync("disableLocationPicking();");
                statusText.Text = "";

                // 如果起点和终点都已选择，自动计算路线
                if (startPoint != null && endPoint != null)
                {
                    await Task.Delay(500); // 稍微延迟以确保标记已添加
                    BtnPlanRoute_Click(null, null);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"处理地图点击失败: {ex.Message}");
                MessageBox.Show("选择位置失败，请重试", "错误");
            }
        }

        private async Task<string> GetLocationName(double latitude, double longitude)
        {
            try
            {
                // 首先检查是否点击了教室或其他已知设施
                var nearbyClassroom = await _context.ClassroomSpatials
                    .Where(c => c.CenterPoint.Distance(
                        GeometryFactory.Default.CreatePoint(
                            new Coordinate(longitude, latitude))) <= 20) // 20米范围内
                    .OrderBy(c => c.CenterPoint.Distance(
                        GeometryFactory.Default.CreatePoint(
                            new Coordinate(longitude, latitude))))
                    .FirstOrDefaultAsync();

                if (nearbyClassroom != null)
                {
                    return $"{nearbyClassroom.SpatialLocation} {nearbyClassroom.RoomNumber}";
                }

                // 如果不是已知设施，则使用OSM反向地理编码
                var response = await _httpClient.GetStringAsync(
                    $"https://nominatim.openstreetmap.org/reverse?" +
                    $"format=json&lat={latitude}&lon={longitude}&zoom=18");

                var result = JsonSerializer.Deserialize<NominatimReverseResult>(response);
                return result?.DisplayName?.Split(',')[0] ?? $"位置 ({latitude:F6}, {longitude:F6})";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"获取位置名称失败: {ex.Message}");
                return $"位置 ({latitude:F6}, {longitude:F6})";
            }
        }

        private void BtnClearRoute_Click(object sender, RoutedEventArgs e)
        {
            startPoint = null;
            endPoint = null;
            txtStartPoint.Text = "";
            txtEndPoint.Text = "";
            txtRouteInfo.Text = "";

            webView.ExecuteScriptAsync("clearRoute();");
            _currentRoute = null;
        }
        #endregion

        #region Course Location Methods
        private async Task UpdateCurrentCourses()
        {
            try
            {
                var currentCourses = await _courseLocationService.GetCurrentLocationCourses();
                var upcomingCourses = await _courseLocationService.GetUpcomingLocationCourses();

                // 更新课程列表
                currentClassList.ItemsSource = currentCourses;
                upcomingClassList.ItemsSource = upcomingCourses;

                // 更新当前课程位置显示
                if (currentCourses.Any())
                {
                    var currentClassrooms = currentCourses
                        .Where(c => c.Classroom != null)
                        .Select(c => c.Classroom)
                        .ToList();

                    await HighlightCurrentCourseLocations(currentClassrooms);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"更新课程信息失败: {ex.Message}");
            }
        }

        private async Task HighlightCurrentCourseLocations(List<ClassroomSpatial> classrooms)
        {
            try
            {
                var features = classrooms.Select(c => new GeoJsonFeature
                {
                    Type = "Feature",
                    Geometry = CreateClassroomGeometry(c),
                    Properties = new Dictionary<string, object>
                    {
                        ["id"] = c.ClassroomID,
                        ["name"] = c.RoomNumber,
                        ["building"] = c.SpatialLocation,
                        ["floor"] = c.Floor,
                        ["type"] = "currentCourse"
                    }
                }).ToList();

                var featureCollection = new GeoJsonFeatureCollection
                {
                    Type = "FeatureCollection",
                    Features = features
                };

                await webView.ExecuteScriptAsync(
                    $"highlightCurrentCourses({JsonSerializer.Serialize(featureCollection, _jsonOptions)});"
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"高亮当前课程位置失败: {ex.Message}");
            }
        }

        private async void BtnNavigateToClass_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is CourseSchedule course)
            {
                try
                {
                    var locationInfo = await _courseLocationService.GetClassroomLocation(course.CourseID);
                    if (locationInfo != null)
                    {
                        // 设置终点为课程教室
                        endPoint = new NavigationPoint
                        {
                            Latitude = locationInfo.Latitude,
                            Longitude = locationInfo.Longitude,
                            Name = $"{locationInfo.Building} {locationInfo.RoomNumber}"
                        };
                        txtEndPoint.Text = endPoint.Name;

                        // 如果还没有设置起点，提示用户选择起点
                        if (startPoint == null)
                        {
                            MessageBox.Show("请选择起点位置", "提示");
                            BtnSelectStart_Click(null, null);
                        }
                        else
                        {
                            // 直接规划路线
                            BtnPlanRoute_Click(null, null);
                        }

                        // 切换到导航标签
                        tabNavigation.IsSelected = true;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导航至教室失败: {ex.Message}", "错误");
                }
            }
        }

        private async void BtnCheckIn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is CourseSchedule course)
            {
                try
                {
                    // 获取当前位置
                    var position = await GetCurrentPosition();
                    if (position == null)
                    {
                        MessageBox.Show("无法获取当前位置，请确保已启用位置服务", "签到失败");
                        return;
                    }

                    var result = await _courseLocationService.CheckInAtLocation(
                        course.CourseID,
                        position.Latitude,
                        position.Longitude
                    );

                    if (result.Success)
                    {
                        MessageBox.Show($"签到成功！\n距离教室: {result.Distance:F0}米", "提示");
                    }
                    else
                    {
                        MessageBox.Show(result.Message, "签到失败");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"签到失败: {ex.Message}", "错误");
                }
            }
        }

        private async Task<GeoPosition> GetCurrentPosition()
        {
            try
            {
                // 创建一个TaskCompletionSource来等待位置结果
                var tcs = new TaskCompletionSource<GeoPosition>();

                // 添加一次性事件处理程序来接收位置信息
                void Handler(object s, CoreWebView2WebMessageReceivedEventArgs e)
                {
                    try
                    {
                        var message = JsonSerializer.Deserialize<WebMessage>(e.WebMessageAsJson);
                        if (message.Type == "location")
                        {
                            var position = JsonSerializer.Deserialize<GeoPosition>(message.Data.ToString());
                            tcs.TrySetResult(position);
                        }
                        else if (message.Type == "error")
                        {
                            tcs.TrySetException(new Exception(message.Data.ToString()));
                        }
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                    finally
                    {
                        webView.WebMessageReceived -= Handler;
                    }
                }

                // 添加事件处理程序
                webView.WebMessageReceived += Handler;

                // 调用JavaScript获取位置
                await webView.ExecuteScriptAsync("getLocationAsync()");

                // 等待结果，设置5秒超时
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(-1, cts.Token));

                if (completedTask == tcs.Task)
                {
                    return await tcs.Task;
                }
                else
                {
                    throw new TimeoutException("获取位置超时");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"获取位置失败: {ex.Message}");
                MessageBox.Show("获取位置失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        private async void CmbBuilding_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbBuilding.SelectedItem == null) return;

            try
            {
                string selectedBuilding = cmbBuilding.SelectedItem.ToString();

                // 更新楼层筛选器
                UpdateFloorFilter(selectedBuilding);

                // 更新教室列表
                var filteredClassrooms = selectedBuilding == "全部"
                    ? _allClassrooms
                    : _allClassrooms.Where(c => c.SpatialLocation == selectedBuilding);

                UpdateClassroomsList(filteredClassrooms);

                // 在地图上定位到选中的建筑
                if (selectedBuilding != "全部")
                {
                    var building = _allClassrooms.FirstOrDefault(c => c.SpatialLocation == selectedBuilding);
                    if (building != null)
                    {
                        // 计算建筑物的边界框
                        var bounds = CalculateBuildingBounds(
                            _allClassrooms.Where(c => c.SpatialLocation == selectedBuilding)
                        );
                        await webView.ExecuteScriptAsync(
                            $"focusOnBuildingBounds({JsonSerializer.Serialize(bounds)});"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"切换建筑物视图失败: {ex.Message}");
                MessageBox.Show("切换建筑物视图时发生错误", "错误");
            }
        }
        private class BoundingBox
        {
            public double North { get; set; }
            public double South { get; set; }
            public double East { get; set; }
            public double West { get; set; }
        }

        private BoundingBox CalculateBuildingBounds(IEnumerable<ClassroomSpatial> classrooms)
        {
            var coordinates = classrooms
                .Where(c => c.Shape != null)
                .SelectMany(c => c.Shape.Coordinates);

            if (!coordinates.Any())
                return null;

            return new BoundingBox
            {
                North = coordinates.Max(c => c.Y) + 0.001, // 添加一些边距
                South = coordinates.Min(c => c.Y) - 0.001,
                East = coordinates.Max(c => c.X) + 0.001,
                West = coordinates.Min(c => c.X) - 0.001
            };
        }

        private async void SearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (searchResultsList.SelectedItem is SearchResult result)
            {
                try
                {
                    if (result.IsClassroom)
                    {
                        await HighlightClassrooms(new List<ClassroomSpatial> { result.Classroom });
                        // 缩放到选中的教室
                        if (result.Classroom?.CenterPoint != null)
                        {
                            await webView.ExecuteScriptAsync(
                                $"focusOnLocation({result.Classroom.CenterPoint.Y}, {result.Classroom.CenterPoint.X}, 19);"
                            );
                        }
                    }
                    else
                    {
                        await webView.ExecuteScriptAsync(
                            $"focusOnLocation({result.Latitude}, {result.Longitude}, 18);"
                        );
                    }

                    searchResultsList.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"处理搜索结果选择失败: {ex.Message}");
                }
            }
        }

        private async void ListClassrooms_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (e.AddedItems.Count > 0 && e.AddedItems[0] is ClassroomSpatial selectedClassroom)
                {
                    Debug.WriteLine($"Selected classroom: {selectedClassroom.RoomNumber}");
                    await AddClassroomToMap(selectedClassroom);

                    if (selectedClassroom.CenterPoint != null)
                    {
                        await webView.ExecuteScriptAsync(
                            $"focusOnLocation({selectedClassroom.CenterPoint.Y}, {selectedClassroom.CenterPoint.X});"
                        );
                    }
                    else
                    {
                        Debug.WriteLine("Selected classroom has no center point");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"选择教室失败: {ex.Message}");
            }
        }

        private async void BtnLocateClassroom_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ClassroomSpatial classroom)
            {
                await AddClassroomToMap(classroom);
                await webView.ExecuteScriptAsync(
                    $"focusOnLocation({classroom.CenterPoint.Y}, {classroom.CenterPoint.X});"
                );
            }
        }

        private async void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            await webView.ExecuteScriptAsync("zoomIn();");
        }

        private async void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            await webView.ExecuteScriptAsync("zoomOut();");
        }

        private async void BtnResetView_Click(object sender, RoutedEventArgs e)
        {
            await webView.ExecuteScriptAsync("resetView();");
        }

        private async void BtnShowAllBuildings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 重新加载所有教室
                foreach (var classroom in _allClassrooms)
                {
                    await AddClassroomToMap(classroom);
                }

                // 调整地图视图以显示所有建筑
                await webView.ExecuteScriptAsync("showAllClassrooms();");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"显示所有建筑失败: {ex.Message}");
                MessageBox.Show("显示所有建筑时发生错误", "错误");
            }
        }

        private GeoJsonGeometry CreateClassroomGeometry(ClassroomSpatial classroom)
        {
            try
            {
                if (classroom?.Shape == null || classroom.Shape.Coordinates.Length == 0)
                    return null;

                // 确保坐标有效
                var coordinates = classroom.Shape.Coordinates
                    .Where(c => !double.IsInfinity(c.X) && !double.IsInfinity(c.Y) &&
                               !double.IsNaN(c.X) && !double.IsNaN(c.Y))
                    .Select(c => new[] { c.X, c.Y })
                    .ToArray();

                if (coordinates.Length < 3) // 多边形至少需要3个点
                    return null;

                return new GeoJsonGeometry
                {
                    Type = "Polygon",
                    Coordinates = new[] { coordinates }
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"创建教室几何形状失败: {ex.Message}");
                return null;
            }
        }

        private GeoJsonGeometry CreatePointGeometry(double latitude, double longitude)
        {
            return new GeoJsonGeometry
            {
                Type = "Point",
                Coordinates = new[] { longitude, latitude }
            };
        }

        private GeoJsonFeature CreatePointFeature(NavigationPoint point, string type)
        {
            return new GeoJsonFeature
            {
                Type = "Feature",
                Geometry = CreatePointGeometry(point.Latitude, point.Longitude),
                Properties = new Dictionary<string, object>
                {
                    ["type"] = type,
                    ["name"] = point.Name
                }
            };
        }

        private class SearchResultClick
        {
            public int id { get; set; }
            public string name { get; set; }
            public string type { get; set; }
            public string category { get; set; }
            public double[] coordinates { get; set; }
        }

        private class WebMessage
        {
            public string Type { get; set; }
            public object Data { get; set; }
        }

        private class MapClickMessage
        {
            public double Latitude { get; set; }
            public double Longitude { get; set; }
        }

        public class GeoPosition
        {
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public double? Accuracy { get; set; }
        }

        private class NominatimReverseResult
        {
            public string DisplayName { get; set; }
        }

    }
}
#endregion