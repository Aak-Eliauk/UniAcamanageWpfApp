using NetTopologySuite.Geometries;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniAcamanageWpfApp.Models
{
    [Table("Classroom")]
    public class ClassroomSpatial
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

        // 改为具体的 Polygon 类型，因为教室形状是多边形
        public Polygon Shape { get; set; }

        // 改为具体的 Point 类型，因为中心点是一个点
        public Point CenterPoint { get; set; }
    }
}