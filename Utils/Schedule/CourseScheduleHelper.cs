using System;
using System.Collections.Generic;
using System.Linq;

namespace UniAcamanageWpfApp.Utils.Schedule
{
    public static class CourseScheduleHelper
    {
        public static List<TimeSlot> ParseScheduleTime(string scheduleTime)
        {
            if (string.IsNullOrEmpty(scheduleTime))
                throw new ScheduleException("课程时间不能为空");

            var timeSlots = new List<TimeSlot>();
            var slots = scheduleTime.Split(',');

            foreach (var slot in slots)
            {
                timeSlots.Add(ParseTimeSlot(slot));
            }

            return timeSlots;
        }

        public static TimeSlot ParseTimeSlot(string timeSlot)
        {
            try
            {
                var parts = timeSlot.Split('-');
                if (parts.Length < 5)
                    throw new ScheduleException($"无效的时间格式: {timeSlot}");

                return new TimeSlot
                {
                    WeekDay = int.Parse(parts[0]),
                    StartPeriod = int.Parse(parts[1]),
                    EndPeriod = int.Parse(parts[2]),
                    StartWeek = int.Parse(parts[3]),
                    EndWeek = int.Parse(parts[4]),
                    WeekType = parts.Length > 5 ? parts[5] : null
                };
            }
            catch (Exception ex) when (!(ex is ScheduleException))
            {
                throw new ScheduleException($"解析时间格式失败: {timeSlot}", ex);
            }
        }

        public static string FormatScheduleTime(List<TimeSlot> slots)
        {
            return string.Join(", ", slots.Select(slot =>
            {
                string weekDay = slot.WeekDay switch
                {
                    1 => "周一",
                    2 => "周二",
                    3 => "周三",
                    4 => "周四",
                    5 => "周五",
                    6 => "周六",
                    7 => "周日",
                    _ => throw new ScheduleException($"无效的星期数: {slot.WeekDay}")
                };

                string weekInfo = slot.WeekType == null ?
                    $"{slot.StartWeek}-{slot.EndWeek}周" :
                    $"{slot.StartWeek}-{slot.EndWeek}周({(slot.WeekType == "A" ? "单" : "双")})";

                return $"{weekDay}第{slot.StartPeriod}-{slot.EndPeriod}节({weekInfo})";
            }));
        }

        public static bool HasTimeConflict(TimeSlot slot1, TimeSlot slot2)
        {
            // 不同星期天不会冲突
            if (slot1.WeekDay != slot2.WeekDay)
                return false;

            // 检查周次重叠
            if (slot1.EndWeek < slot2.StartWeek || slot1.StartWeek > slot2.EndWeek)
                return false;

            // 检查单双周
            if (slot1.WeekType != null && slot2.WeekType != null && slot1.WeekType != slot2.WeekType)
                return false;

            // 检查节次重叠
            return !(slot1.EndPeriod < slot2.StartPeriod || slot1.StartPeriod > slot2.EndPeriod);
        }

        public static bool ValidateTimeSlot(TimeSlot slot)
        {
            if (slot.WeekDay < 1 || slot.WeekDay > 7)
                return false;

            if (slot.StartPeriod < 1 || slot.StartPeriod > 11 ||
                slot.EndPeriod < 1 || slot.EndPeriod > 11 ||
                slot.StartPeriod > slot.EndPeriod)
                return false;

            if (slot.StartWeek < 1 || slot.EndWeek > 25 ||
                slot.StartWeek > slot.EndWeek)
                return false;

            if (slot.WeekType != null && slot.WeekType != "A" && slot.WeekType != "B")
                return false;

            return true;
        }
    }
}