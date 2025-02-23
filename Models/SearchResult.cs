﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UniAcamanageWpfApp.Models
{
    public class SearchResult
    {
        public string DisplayName { get; set; }
        public string Category { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool IsClassroom { get; set; }
        public ClassroomSpatial Classroom { get; set; }
        public string Source { get; set; }  // "Database" 或 "OSM"
        public Dictionary<string, string> AdditionalInfo { get; set; }
        public string IconKind { get; set; }  // MaterialDesign图标名称
    }
}
