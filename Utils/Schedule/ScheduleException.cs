namespace UniAcamanageWpfApp.Utils.Schedule
{
    public class ScheduleException : Exception
    {
        public ScheduleException(string message) : base(message) { }
        public ScheduleException(string message, Exception inner) : base(message, inner) { }
    }
}