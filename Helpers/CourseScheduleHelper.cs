// CourseScheduleHelper.cs
namespace UniAcamanageWpfApp.Helpers
{
    public static class CourseScheduleHelper
    {
        private static readonly string[] WeekDays = { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };

        public static string ConvertToDisplayFormat(string dbScheduleTime)
        {
            if (string.IsNullOrEmpty(dbScheduleTime)) return "";

            var schedules = dbScheduleTime.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var displaySchedules = new List<string>();

            foreach (var schedule in schedules)
            {
                var parts = schedule.Split('-');
                if (parts.Length < 5) continue;

                int weekDay = int.Parse(parts[0]);
                string weekDayStr = WeekDays[weekDay - 1];
                string sections = $"第{parts[1]}-{parts[2]}节";
                string weeks = $"{parts[3]}-{parts[4]}周";
                string weekType = parts.Length > 5 ? (parts[5] == "A" ? "单周" : "双周") : "";

                displaySchedules.Add($"{weekDayStr} {sections} ({weeks}{weekType})");
            }

            return string.Join("\n", displaySchedules);
        }

        public static bool ValidateDbFormat(string scheduleTime)
        {
            if (string.IsNullOrEmpty(scheduleTime)) return false;

            foreach (var schedule in scheduleTime.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = schedule.Trim().Split('-');
                if (!IsValidScheduleParts(parts)) return false;
            }

            return true;
        }

        private static bool IsValidScheduleParts(string[] parts)
        {
            if (parts.Length < 5 || parts.Length > 6) return false;

            if (!int.TryParse(parts[0], out int weekDay) ||
                !int.TryParse(parts[1], out int startSection) ||
                !int.TryParse(parts[2], out int endSection) ||
                !int.TryParse(parts[3], out int startWeek) ||
                !int.TryParse(parts[4], out int endWeek))
                return false;

            if (weekDay < 1 || weekDay > 7 ||
                startSection < 1 || startSection > 11 ||
                endSection < 1 || endSection > 11 ||
                startSection > endSection ||
                startWeek < 1 || startWeek > 52 ||
                endWeek < 1 || endWeek > 52 ||
                startWeek > endWeek)
                return false;

            if (parts.Length == 6 && parts[5] != "A" && parts[5] != "B")
                return false;

            return true;
        }
    }
}