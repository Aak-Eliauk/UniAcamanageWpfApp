// Utils/Schedule/TimeSlot.cs
namespace UniAcamanageWpfApp.Utils.Schedule
{
    public class TimeSlot
    {
        public int WeekDay { get; set; }
        public int StartPeriod { get; set; }
        public int EndPeriod { get; set; }
        public int StartWeek { get; set; }
        public int EndWeek { get; set; }
        public string WeekType { get; set; }
    }
}