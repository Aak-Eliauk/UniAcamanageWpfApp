using GMap.NET;
using Microsoft.SqlServer.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UniAcamanageWpfApp.GeometryT
{
    public static class GeometryConverter
    {
        public static List<PointLatLng> SqlGeometryToGMapPoints(SqlGeometry geometry)
        {
            var points = new List<PointLatLng>();
            if (geometry == null || geometry.IsNull)
                return points;

            try
            {
                // 获取几何对象的类型
                string geometryType = geometry.STGeometryType().Value;

                switch (geometryType.ToUpper())
                {
                    case "POLYGON":
                        // 获取外环坐标
                        var ring = geometry.STExteriorRing();
                        for (int i = 1; i <= ring.STNumPoints(); i++)
                        {
                            var point = ring.STPointN(i);
                            points.Add(new PointLatLng(point.STY.Value, point.STX.Value));
                        }
                        break;

                    case "MULTIPOLYGON":
                        // 处理多多边形
                        for (int i = 1; i <= geometry.STNumGeometries(); i++)
                        {
                            var polygon = geometry.STGeometryN(i);
                            var polygonRing = polygon.STExteriorRing();
                            for (int j = 1; j <= polygonRing.STNumPoints(); j++)
                            {
                                var point = polygonRing.STPointN(j);
                                points.Add(new PointLatLng(point.STY.Value, point.STX.Value));
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"几何转换错误: {ex.Message}");
            }

            return points;
        }
    }
}
