using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetTopologySuite.IO;
using NetTopologySuite.Geometries;
using System.IO;

namespace UniAcamanageWpfApp.GeometryT
{
    public class ClassroomSpatialImporter
    {
        private readonly string _connectionString;
        public event Action<string> LogMessage;
        public event Action<int> ProgressChanged;

        public ClassroomSpatialImporter()
        {
            _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
        }

        private void Log(string message)
        {
            LogMessage?.Invoke($"[{DateTime.Now:HH:mm:ss}] {message}");
        }

        private void UpdateProgress(int progress)
        {
            ProgressChanged?.Invoke(progress);
        }

        public async Task ImportShapefileData(string shapefilePath)
        {
            if (string.IsNullOrEmpty(shapefilePath))
                throw new ArgumentNullException(nameof(shapefilePath), "Shapefile路径不能为空");

            if (!File.Exists(shapefilePath))
                throw new FileNotFoundException("找不到指定的Shapefile文件", shapefilePath);

            try
            {
                Log($"开始读取Shapefile: {shapefilePath}");
                var geometryFactory = new GeometryFactory();
                using var shapeFileDataReader = new ShapefileDataReader(shapefilePath, geometryFactory);

                // 获取字段信息
                var fields = shapeFileDataReader.DbaseHeader.Fields;
                Log($"发现 {fields.Length} 个字段:");
                for (int i = 0; i < fields.Length; i++)
                {
                    Log($"字段 {i}: {fields[i].Name}");
                }

                // 查找RoomNumber字段索引
                int roomNumberIndex = FindFieldIndex(fields, "RoomNumber");
                if (roomNumberIndex == -1)
                {
                    throw new Exception("在Shapefile中未找到RoomNumber字段，请检查字段名称");
                }
                Log($"使用RoomNumber字段，索引为: {roomNumberIndex}");

                // 数据库操作
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var transaction = conn.BeginTransaction();

                try
                {
                    // 清理临时表
                    Log("清理旧的临时表...");
                    await new SqlCommand(@"
                IF OBJECT_ID('TempClassroomSpatial', 'U') IS NOT NULL 
                BEGIN
                    DROP TABLE TempClassroomSpatial;
                END", conn, transaction).ExecuteNonQueryAsync();

                    // 创建新的临时表
                    Log("创建新的临时表...");
                    await new SqlCommand(@"
                CREATE TABLE TempClassroomSpatial (
                    ID INT IDENTITY(1,1) PRIMARY KEY,
                    RoomNumber nvarchar(50),
                    OriginalNumber nvarchar(50),
                    Shape geometry,
                    CenterPoint AS Shape.STCentroid() PERSISTED
                );", conn, transaction).ExecuteNonQueryAsync();

                    // 计算总记录数
                    int totalRecords = 0;
                    while (shapeFileDataReader.Read()) totalRecords++;
                    shapeFileDataReader.Reset();

                    Log($"开始导入 {totalRecords} 条记录...");
                    int importedCount = 0;
                    int skippedCount = 0;
                    var processedRoomNumbers = new HashSet<string>();

                    // 读取并导入数据
                    while (shapeFileDataReader.Read())
                    {
                        try
                        {
                            var geometry = shapeFileDataReader.Geometry;
                            var values = new object[fields.Length];
                            shapeFileDataReader.GetValues(values);
                            var roomNumber = values[roomNumberIndex]?.ToString()?.Trim();

                            if (string.IsNullOrEmpty(roomNumber))
                            {
                                Log($"跳过空房间号的记录");
                                skippedCount++;
                                continue;
                            }

                            // 检查重复
                            if (!processedRoomNumbers.Add(roomNumber))
                            {
                                Log($"跳过重复的房间号: {roomNumber}");
                                skippedCount++;
                                continue;
                            }

                            string wkt = ConvertTo2DGeometry(geometry);

                            // 插入数据
                            var cmd = new SqlCommand(@"
                        INSERT INTO TempClassroomSpatial (RoomNumber, OriginalNumber, Shape)
                        VALUES (@roomNumber, @originalNumber, geometry::STGeomFromText(@shape, 4326))",
                                conn, transaction);

                            cmd.Parameters.AddWithValue("@roomNumber", roomNumber);
                            cmd.Parameters.AddWithValue("@originalNumber", roomNumber);
                            cmd.Parameters.AddWithValue("@shape", wkt);

                            await cmd.ExecuteNonQueryAsync();
                            importedCount++;

                            // 更新进度
                            UpdateProgress((int)((double)importedCount / totalRecords * 100));

                            if (importedCount % 10 == 0)
                            {
                                Log($"已处理: {importedCount}/{totalRecords} 条记录");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"处理记录时出错: {ex.Message}");
                            skippedCount++;
                        }
                    }

                    // 检查并添加空间列到Classroom表
                    Log("检查Classroom表结构...");
                    await new SqlCommand(@"
                IF NOT EXISTS (
                    SELECT * FROM sys.columns 
                    WHERE object_id = OBJECT_ID('Classroom') AND name = 'Shape'
                )
                BEGIN
                    ALTER TABLE Classroom ADD 
                        Shape geometry NULL,
                        CenterPoint geometry NULL;
                END", conn, transaction).ExecuteNonQueryAsync();

                    // 更新主表数据
                    Log("更新主表数据...");
                    var updateResult = await new SqlCommand(@"
                UPDATE c SET
                    c.Shape = t.Shape,
                    c.CenterPoint = t.CenterPoint
                FROM Classroom c
                INNER JOIN TempClassroomSpatial t ON c.RoomNumber = t.RoomNumber",
                        conn, transaction).ExecuteNonQueryAsync();

                    Log($"更新完成: 已导入 {importedCount} 条记录，跳过 {skippedCount} 条记录，更新 {updateResult} 条记录");

                    // 检查未匹配的记录
                    var unmatchedCmd = new SqlCommand(@"
                SELECT t.RoomNumber, t.OriginalNumber
                FROM TempClassroomSpatial t
                LEFT JOIN Classroom c ON c.RoomNumber = t.RoomNumber
                WHERE c.ClassroomID IS NULL", conn, transaction);

                    using (var reader = await unmatchedCmd.ExecuteReaderAsync())
                    {
                        var unmatchedRooms = new List<string>();
                        while (await reader.ReadAsync())
                        {
                            unmatchedRooms.Add($"{reader.GetString(0)} (原始值: {reader.GetString(1)})");
                        }

                        if (unmatchedRooms.Any())
                        {
                            Log("\n未匹配的房间号:");
                            foreach (var room in unmatchedRooms.Take(5))
                            {
                                Log(room);
                            }
                            if (unmatchedRooms.Count > 5)
                            {
                                Log($"... 还有 {unmatchedRooms.Count - 5} 条未显示");
                            }
                        }
                    }

                    transaction.Commit();
                    Log("导入完成，事务已提交");
                }
                catch (Exception ex)
                {
                    Log($"数据库操作失败，正在回滚事务: {ex.Message}");
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Log($"导入过程失败: {ex.Message}");
                throw;
            }
        }

        private int FindFieldIndex(DbaseFieldDescriptor[] fields, string targetFieldName)
        {
            Log($"开始查找字段: {targetFieldName}");

            // 显示所有字段及其位置（从1开始计数）
            for (int i = 0; i < fields.Length; i++)
            {
                Log($"字段 {i + 1}: {fields[i].Name}");

                // 如果找到目标字段，直接返回位置数值（而不是索引）
                if (fields[i].Name.Equals(targetFieldName, StringComparison.OrdinalIgnoreCase))
                {
                    int position = i + 1; // 转换为从1开始的位置
                    Log($"找到字段 '{targetFieldName}' 在第 {position} 个位置");
                    return position; // 直接返回位置数值
                }
            }

            // 如果没有精确匹配，尝试模糊匹配
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].Name.Contains(targetFieldName, StringComparison.OrdinalIgnoreCase))
                {
                    int position = i + 1; // 转换为从1开始的位置
                    Log($"通过模糊匹配找到字段 '{fields[i].Name}' 在第 {position} 个位置");
                    return position;
                }
            }

            // 如果都没找到，记录所有可用字段并返回-1
            Log("未找到匹配字段。可用字段：");
            for (int i = 0; i < fields.Length; i++)
            {
                Log($"第 {i + 1} 个位置: {fields[i].Name}");
            }

            return -1;
        }

        public async Task<(int Matched, int Unmatched)> ValidateDataMapping()
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            Log("开始验证数据匹配情况...");

            var cmd = new SqlCommand(@"
                SELECT 
                    (SELECT COUNT(*) FROM TempClassroomSpatial) as TotalTemp,
                    (SELECT COUNT(*) FROM Classroom) as TotalClassroom,
                    (SELECT COUNT(*) 
                     FROM Classroom c
                     INNER JOIN TempClassroomSpatial t ON c.RoomNumber = t.RoomNumber) as Matched,
                    (SELECT COUNT(*) 
                     FROM TempClassroomSpatial t
                     LEFT JOIN Classroom c ON c.RoomNumber = t.RoomNumber
                     WHERE c.ClassroomID IS NULL) as Unmatched;

                -- 显示未匹配的样例
                SELECT TOP 5 
                    t.RoomNumber as TempNumber, 
                    t.OriginalNumber,
                    t.Shape.STAsText() as ShapeWKT
                FROM TempClassroomSpatial t
                LEFT JOIN Classroom c ON c.RoomNumber = t.RoomNumber
                WHERE c.ClassroomID IS NULL;", conn);

            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var totalTemp = reader.GetInt32(0);
                var totalClassroom = reader.GetInt32(1);
                var matched = reader.GetInt32(2);
                var unmatched = reader.GetInt32(3);

                Log($"临时表总记录数: {totalTemp}");
                Log($"教室表总记录数: {totalClassroom}");
                Log($"匹配记录数: {matched}");
                Log($"未匹配记录数: {unmatched}");

                if (await reader.NextResultAsync())
                {
                    Log("\n未匹配记录样例：");
                    while (await reader.ReadAsync())
                    {
                        Log($"RoomNumber: {reader.GetString(0)}");
                        Log($"Original: {reader.GetString(1)}");
                        Log($"Shape: {reader.GetString(2)}");
                        Log("---");
                    }
                }

                return (matched, unmatched);
            }

            return (0, 0);
        }

        public async Task<List<string>> GetUnmatchedRecords()
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var unmatchedRooms = new List<string>();
            var cmd = new SqlCommand(@"
                SELECT t.RoomNumber, t.OriginalNumber
                FROM TempClassroomSpatial t
                LEFT JOIN Classroom c ON c.RoomNumber = t.RoomNumber
                WHERE c.ClassroomID IS NULL
                ORDER BY t.RoomNumber", conn);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                unmatchedRooms.Add($"{reader.GetString(0)} (原始值: {reader.GetString(1)})");
            }

            return unmatchedRooms;
        }

        private string ConvertTo2DGeometry(Geometry geometry)
        {
            if (geometry == null)
                throw new ArgumentNullException(nameof(geometry));

            try
            {
                switch (geometry)
                {
                    case Polygon polygon:
                        return Convert2DPolygon(polygon);
                    case MultiPolygon multiPolygon:
                        return Convert2DMultiPolygon(multiPolygon);
                    case Point point:
                        return $"POINT ({point.X} {point.Y})";
                    case LineString line:
                        return Convert2DLineString(line);
                    default:
                        throw new NotSupportedException($"不支持的几何类型: {geometry.GeometryType}");
                }
            }
            catch (Exception ex)
            {
                Log($"几何转换失败: {ex.Message}");
                throw;
            }
        }

        private string Convert2DPolygon(Polygon polygon)
        {
            var shell = polygon.ExteriorRing.Coordinates;
            var wktBuilder = new StringBuilder();
            wktBuilder.Append("POLYGON ((");

            for (int i = 0; i < shell.Length; i++)
            {
                if (i > 0) wktBuilder.Append(", ");
                wktBuilder.Append($"{shell[i].X} {shell[i].Y}");
            }

            wktBuilder.Append("))");
            return wktBuilder.ToString();
        }

        private string Convert2DMultiPolygon(MultiPolygon multiPolygon)
        {
            var wktBuilder = new StringBuilder();
            wktBuilder.Append("MULTIPOLYGON (");

            for (int i = 0; i < multiPolygon.NumGeometries; i++)
            {
                if (i > 0) wktBuilder.Append(", ");
                var poly = (Polygon)multiPolygon.GetGeometryN(i);
                var shell = poly.ExteriorRing.Coordinates;

                wktBuilder.Append("((");
                for (int j = 0; j < shell.Length; j++)
                {
                    if (j > 0) wktBuilder.Append(", ");
                    wktBuilder.Append($"{shell[j].X} {shell[j].Y}");
                }
                wktBuilder.Append("))");
            }

            wktBuilder.Append(")");
            return wktBuilder.ToString();
        }

        private string Convert2DLineString(LineString line)
        {
            var coordinates = line.Coordinates;
            var wktBuilder = new StringBuilder();
            wktBuilder.Append("LINESTRING (");

            for (int i = 0; i < coordinates.Length; i++)
            {
                if (i > 0) wktBuilder.Append(", ");
                wktBuilder.Append($"{coordinates[i].X} {coordinates[i].Y}");
            }

            wktBuilder.Append(")");
            return wktBuilder.ToString();
        }
    }
}