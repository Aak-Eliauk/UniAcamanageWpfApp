public class SystemNotification
{
    public int NotificationId { get; set; }
    public string Title { get; set; }
    public string Message { get; set; }
    public DateTime CreatedTime { get; set; }
    public bool IsRead { get; set; }
    public string NotificationType { get; set; }  // 如："选课提醒"、"系统通知"等
}