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

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var transaction = conn.BeginTransaction();

                try
                {
                    // 1. 清理和创建临时表
                    Log("清理旧的临时表...");
                    await new SqlCommand(@"
                        IF OBJECT_ID('TempClassroomSpatial', 'U') IS NOT NULL 
                        BEGIN
                            DROP TABLE TempClassroomSpatial;
                        END", conn, transaction).ExecuteNonQueryAsync();

                    Log("创建新的临时表...");
                    await new SqlCommand(@"
                        CREATE TABLE TempClassroomSpatial (
                            ID INT IDENTITY(1,1) PRIMARY KEY,
                            RoomNumber nvarchar(50),
                            OriginalNumber nvarchar(50),
                            Shape geometry,
                            CenterPoint AS Shape.STCentroid() PERSISTED
                        );", conn, transaction).ExecuteNonQueryAsync();

                    // 2. 获取字段信息
                    var fields = shapeFileDataReader.DbaseHeader.Fields;
                    Log($"发现 {fields.Length} 个字段:");
                    for (int i = 0; i < fields.Length; i++)
                    {
                        Log($"字段 {i + 1}: {fields[i].Name}");
                    }

                    // 3. 查找RoomNumber字段
                    int roomNumberIndex = 2; // RoomNumber 字段的实际位置
                    Log($"使用RoomNumber字段，索引为: {roomNumberIndex}");

                    if (roomNumberIndex == -1)
                        throw new Exception("在Shapefile中未找到RoomNumber字段");

                    // 4. 读取并导入数据
                    int totalRecords = 0;
                    while (shapeFileDataReader.Read()) totalRecords++;
                    shapeFileDataReader.Reset();

                    Log($"开始导入 {totalRecords} 条记录...");
                    int importedCount = 0;
                    int skippedCount = 0;

                    while (shapeFileDataReader.Read())
                    {
                        try
                        {
                            var geometry = shapeFileDataReader.Geometry;
                            var values = new object[fields.Length];
                            shapeFileDataReader.GetValues(values);
                            var roomNumber = values[roomNumberIndex]?.ToString();

                            if (string.IsNullOrEmpty(roomNumber))
                            {
                                skippedCount++;
                                continue;
                            }

                            var wkt = ConvertTo2DGeometry(geometry);

                            var cmd = new SqlCommand(@"
                                INSERT INTO TempClassroomSpatial (RoomNumber, OriginalNumber, Shape)
                                VALUES (@roomNumber, @originalNumber, geometry::STGeomFromText(@shape, 4326))",
                                conn, transaction);

                            cmd.Parameters.AddWithValue("@roomNumber", roomNumber.Trim());
                            cmd.Parameters.AddWithValue("@originalNumber", roomNumber);
                            cmd.Parameters.AddWithValue("@shape", wkt);

                            await cmd.ExecuteNonQueryAsync();
                            importedCount++;

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

                    // 5. 检查并添加空间列到Classroom表
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

                    // 6. 更新主表数据
                    Log("更新主表数据...");
                    var updateResult = await new SqlCommand(@"
                        UPDATE c SET
                            c.Shape = t.Shape,
                            c.CenterPoint = t.CenterPoint
                        FROM Classroom c
                        INNER JOIN TempClassroomSpatial t ON c.RoomNumber = t.RoomNumber",
                        conn, transaction).ExecuteNonQueryAsync();

                    Log($"更新完成: 已导入 {importedCount} 条记录，跳过 {skippedCount} 条记录，更新 {updateResult} 条记录");

                    transaction.Commit();
                    Log("导入完成，事务已提交");
                }
                catch (Exception ex)
                {
                    Log($"发生错误，正在回滚事务: {ex.Message}");
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