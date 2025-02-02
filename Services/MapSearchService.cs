using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using UniAcamanageWpfApp.Data;
using UniAcamanageWpfApp.Models;

namespace UniAcamanageWpfApp.Services
{
    public class MapSearchService
    {
        private readonly HttpClient _httpClient;
        private readonly CampusDbContext _context;
        private const string OVERPASS_API = "https://overpass-api.de/api/interpreter";
        private const string BBOX = "114.3,30.4,114.7,30.5"; // 校园范围

        public MapSearchService(HttpClient httpClient, CampusDbContext context)
        {
            _httpClient = httpClient;
            _context = context;
        }

        public async Task<List<SearchResult>> Search(string searchText, string facilityTypeId = null)
        {
            var results = new List<SearchResult>();

            // 1. 搜索数据库中的教室和设施
            var dbResults = await SearchDatabase(searchText, facilityTypeId);
            results.AddRange(dbResults);

            // 2. 搜索OSM数据
            var osmResults = await SearchOSM(searchText, facilityTypeId);
            results.AddRange(osmResults);

            return results.DistinctBy(r => $"{r.Latitude},{r.Longitude}").ToList();
        }

        private async Task<List<SearchResult>> SearchDatabase(string searchText, string facilityTypeId)
        {
            var query = _context.ClassroomSpatials.AsQueryable();
            var facilityType = facilityTypeId != null ? FacilityType.Types[facilityTypeId] : null;

            if (!string.IsNullOrEmpty(searchText))
            {
                query = query.Where(c =>
                    c.RoomNumber.Contains(searchText) ||
                    c.SpatialLocation.Contains(searchText));
            }

            if (facilityType != null)
            {
                query = query.Where(c =>
                    facilityType.Keywords.Any(k => c.SpatialLocation.Contains(k)));
            }

            var classrooms = await query.ToListAsync();

            return classrooms.Select(c => new SearchResult
            {
                DisplayName = $"{c.RoomNumber} ({c.SpatialLocation})",
                Category = "教室",
                Latitude = (c.CenterPoint as Point)?.Y ?? 0,  // 修改这行
                Longitude = (c.CenterPoint as Point)?.X ?? 0, // 修改这行
                IsClassroom = true,
                Classroom = c,
                Source = "Database",
                IconKind = "ClassRoom",
                AdditionalInfo = new Dictionary<string, string>
                {
                    ["楼层"] = c.Floor.ToString(),
                    ["容量"] = c.Capacity.ToString(),
                    ["位置"] = c.SpatialLocation
                }
            }).ToList();
        }

        private async Task<List<SearchResult>> SearchOSM(string searchText, string facilityTypeId)
        {
            try
            {
                var facilityType = facilityTypeId != null ? FacilityType.Types[facilityTypeId] : null;
                var keywords = facilityType?.Keywords ??
                             FacilityType.Types.Values.SelectMany(t => t.Keywords).Distinct();

                // 构建 Overpass QL 查询
                var query = "[out:json][timeout:25];" +
                           $"area[name=\"中国地质大学未来城校区\"]->.searchArea;" +
                           "(" +
                           string.Join("", keywords.Select(k =>
                               $"way[\"name\"~\"{k}\"][\"building\"](area.searchArea);" +
                               $"relation[\"name\"~\"{k}\"](area.searchArea);"
                           )) +
                           ");" +
                           "out body geom;";

                var response = await _httpClient.PostAsync(OVERPASS_API,
                    new StringContent(query));

                var json = await response.Content.ReadAsStringAsync();
                var osmData = JsonSerializer.Deserialize<OverpassResponse>(json);

                return osmData.Elements.Select(e => new SearchResult
                {
                    DisplayName = e.Tags.GetValueOrDefault("name", "未命名设施"),
                    Category = DetermineFacilityCategory(e.Tags),
                    Latitude = e.CalculateCenterLatitude(),
                    Longitude = e.CalculateCenterLongitude(),
                    IsClassroom = false,
                    Source = "OSM",
                    IconKind = GetIconKind(e.Tags),
                    AdditionalInfo = new Dictionary<string, string>
                    {
                        ["类型"] = e.Tags.GetValueOrDefault("building", ""),
                        ["层数"] = e.Tags.GetValueOrDefault("building:levels", ""),
                        ["地址"] = e.Tags.GetValueOrDefault("addr:full", "")
                    }
                }).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OSM搜索失败: {ex.Message}");
                return new List<SearchResult>();
            }
        }

        private string DetermineFacilityCategory(Dictionary<string, string> tags)
        {
            foreach (var type in FacilityType.Types.Values)
            {
                if (type.Keywords.Any(k =>
                    tags.Values.Any(v => v.Contains(k, StringComparison.OrdinalIgnoreCase))))
                {
                    return type.Name;
                }
            }
            return "其他";
        }

        private string GetIconKind(Dictionary<string, string> tags)
        {
            foreach (var type in FacilityType.Types.Values)
            {
                if (type.Keywords.Any(k =>
                    tags.Values.Any(v => v.Contains(k, StringComparison.OrdinalIgnoreCase))))
                {
                    return type.IconKind;
                }
            }
            return "MapMarker";
        }

        public class OverpassResponse
        {
            public List<OverpassElement> Elements { get; set; }
        }

        public class OverpassElement
        {
            public long Id { get; set; }
            public string Type { get; set; }
            public Dictionary<string, string> Tags { get; set; }
            public List<OverpassNode> Geometry { get; set; }

            public double CalculateCenterLatitude()
            {
                if (Geometry == null || !Geometry.Any())
                    return 0;
                return Geometry.Average(n => n.Lat);
            }

            public double CalculateCenterLongitude()
            {
                if (Geometry == null || !Geometry.Any())
                    return 0;
                return Geometry.Average(n => n.Lon);
            }
        }

        public class OverpassNode
        {
            public double Lat { get; set; }
            public double Lon { get; set; }
        }
    }
}