// GlobalUserState.cs
namespace UniAcamanageWpfApp
{
    public static class GlobalUserState
    {
        // 当前登录用户的学号或工号（对应登录表中LinkedID）
        public static string LinkedID { get; set; } = string.Empty;

        // 当前登录用户的角色（Student/Teacher/Admin）
        public static string Role { get; set; } = string.Empty;

        // 还可以根据需要添加更多全局信息，例如：Username, DepartmentID, ...
        public static string Username { get; set; } = string.Empty;
    }
}

