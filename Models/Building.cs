using NetTopologySuite.Geometries;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniAcamanageWpfApp.Models
{
    [Table("Classroom")]

    public class ClassroomSpatial
    {
        public int ClassroomID { get; set; }
        public string RoomNumber { get; set; }
        public int Floor { get; set; }
        public int Capacity { get; set; }
        public string SpatialLocation { get; set; }
        public Geometry Shape { get; set; }

        public Point CenterPoint { get; set; }
    }
}