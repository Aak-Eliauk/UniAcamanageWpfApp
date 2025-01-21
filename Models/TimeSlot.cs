using System;

namespace UniAcamanageWpfApp.Models
{
    public class TimeSlot
    {
        public int WeekDay { get; set; }
        public int StartSection { get; set; }
        public int EndSection { get; set; }
        public int StartWeek { get; set; }
        public int EndWeek { get; set; }
        public string WeekType { get; set; }

        // 用于显示在ListView中的文本
        public string DisplayText => ToString();

        public string ToDbFormat()
        {
            return $"{WeekDay}-{StartSection}-{EndSection}-{StartWeek}-{EndWeek}" +
                   (!string.IsNullOrEmpty(WeekType) ? $"-{WeekType}" : "");
        }

        public static bool ValidateFormat(string format)
        {
            var parts = format.Trim().Split('-');
            if (parts.Length < 5 || parts.Length > 6)
                return false;

            // 验证数值范围
            if (!int.TryParse(parts[0], out int weekDay) || weekDay < 1 || weekDay > 7)
                return false;

            if (!int.TryParse(parts[1], out int startSection) || startSection < 1 || startSection > 11)
                return false;

            if (!int.TryParse(parts[2], out int endSection) || endSection < 1 || endSection > 11 || endSection < startSection)
                return false;

            if (!int.TryParse(parts[3], out int startWeek) || startWeek < 1 || startWeek > 25)
                return false;

            if (!int.TryParse(parts[4], out int endWeek) || endWeek < 1 || endWeek > 25 || endWeek < startWeek)
                return false;

            // 如果有第六部分，验证是否为 A 或 B
            if (parts.Length == 6 && parts[5] != "A" && parts[5] != "B")
                return false;

            return true;
        }

        public static TimeSlot FromDbFormat(string format)
        {
            if (!ValidateFormat(format))
                return null;

            var parts = format.Trim().Split('-');
            return new TimeSlot
            {
                WeekDay = int.Parse(parts[0]),
                StartSection = int.Parse(parts[1]),
                EndSection = int.Parse(parts[2]),
                StartWeek = int.Parse(parts[3]),
                EndWeek = int.Parse(parts[4]),
                WeekType = parts.Length > 5 ? parts[5] : ""
            };
        }

        public override string ToString()
        {
            string[] weekDays = { "一", "二", "三", "四", "五", "六", "日" };
            string weekDayStr = $"周{weekDays[WeekDay - 1]}";
            string sectionStr = $"第{StartSection}-{EndSection}节";
            string weekStr = $"{StartWeek}-{EndWeek}周";
            string weekTypeStr = WeekType == "A" ? "单周" : WeekType == "B" ? "双周" : "";

            return $"{weekDayStr} {sectionStr} ({weekStr}{weekTypeStr})";
        }

        public bool Conflicts(TimeSlot other)
        {
            // 如果不是同一天，则不冲突
            if (WeekDay != other.WeekDay) return false;

            // 检查周次是否重叠
            bool weeksOverlap = !(EndWeek < other.StartWeek || StartWeek > other.EndWeek);
            if (!weeksOverlap) return false;

            // 检查单双周是否冲突
            if (WeekType != "" && other.WeekType != "" && WeekType != other.WeekType)
                return false;

            // 检查节次是否重叠
            return !(EndSection < other.StartSection || StartSection > other.EndSection);
        }

        // 添加复制方法，用于编辑时复制时间段
        public TimeSlot Clone()
        {
            return new TimeSlot
            {
                WeekDay = this.WeekDay,
                StartSection = this.StartSection,
                EndSection = this.EndSection,
                StartWeek = this.StartWeek,
                EndWeek = this.EndWeek,
                WeekType = this.WeekType
            };
        }
    }
}