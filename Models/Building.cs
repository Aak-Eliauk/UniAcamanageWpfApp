using NetTopologySuite.Geometries;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniAcamanageWpfApp.Models
{
    [Table("Classroom")] // 实际的数据库表名保持不变
    public class ClassroomSpatial // 更明确的类名
    {
        [Key]
        public int ClassroomID { get; set; }

        [Required]
        [MaxLength(20)]
        public string RoomNumber { get; set; }

        public int Floor { get; set; }

        public int Capacity { get; set; }

        [MaxLength(100)]
        public string SpatialLocation { get; set; }

        public Geometry Shape { get; set; }

        public Geometry CenterPoint { get; set; }
    }
}