using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using UniAcamanageWpfApp.Models;

namespace UniAcamanageWpfApp.Services
{
    public class NavigationService
    {
        private readonly HttpClient _httpClient;
        private const string OSRM_BASE_URL = "http://router.project-osrm.org/route/v1";

        public NavigationService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<NavigationResult> GetRoute(
            NavigationPoint start,
            NavigationPoint end,
            string mode = "foot")
        {
            try
            {
                var url = $"{OSRM_BASE_URL}/{mode}/" +
                         $"{start.Longitude},{start.Latitude};" +
                         $"{end.Longitude},{end.Latitude}" +
                         "?overview=full&steps=true&geometries=geojson";

                var response = await _httpClient.GetStringAsync(url);
                var result = JsonSerializer.Deserialize<OSRMResponse>(response);

                if (result?.Routes?.FirstOrDefault() is OSRMRoute route)
                {
                    return new NavigationResult
                    {
                        Steps = route.Legs[0].Steps.Select(s => new NavigationStep
                        {
                            Instruction = s.Maneuver.Instruction,
                            Distance = s.Distance,
                            Duration = s.Duration,
                            Name = s.Name,
                            Geometry = s.Geometry.Coordinates
                        }).ToList(),
                        TotalDistance = route.Distance,
                        TotalDuration = route.Duration,
                        RouteGeometry = route.Geometry.Coordinates,
                        Mode = mode
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"路径规划失败: {ex.Message}");
                throw new Exception("无法获取导航路线，请稍后重试");
            }

            return null;
        }

        // OSRM API响应模型
        private class OSRMResponse
        {
            public OSRMRoute[] Routes { get; set; }
        }

        private class OSRMRoute
        {
            public double Distance { get; set; }
            public double Duration { get; set; }
            public OSRMGeometry Geometry { get; set; }
            public OSRMLeg[] Legs { get; set; }
        }

        private class OSRMGeometry
        {
            public List<double[]> Coordinates { get; set; }
        }

        private class OSRMLeg
        {
            public OSRMStep[] Steps { get; set; }
        }

        private class OSRMStep
        {
            public double Distance { get; set; }
            public double Duration { get; set; }
            public OSRMGeometry Geometry { get; set; }
            public string Name { get; set; }
            public OSRMManeuver Maneuver { get; set; }
        }

        private class OSRMManeuver
        {
            public string Instruction { get; set; }
        }
    }
}
