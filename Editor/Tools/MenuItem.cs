#pragma warning disable CS8632
using com.MiAO.Unity.MCP.Common;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    [McpPluginToolType]
    public partial class Tool_MenuItem
    {
        public static class Error
        {
            public static string EmptyMenuPath()
                => "[Error] Menu path is empty. Please provide a valid menu path. Example: 'Assets/Create/C# Script' or 'GameObject/Create Empty'.";

            public static string MenuItemNotFound(string menuPath)
                => $"[Error] Menu item '{menuPath}' does not exist. Please check the menu path and ensure it's correct.";

            public static string MenuItemDisabled(string menuPath)
                => $"[Warning] Menu item '{menuPath}' exists but is currently disabled.";

            public static string InvalidMenuPath(string menuPath)
                => $"[Error] Invalid menu path '{menuPath}'. Please verify the menu path is correct.";

            public static string ExecutionFailed(string menuPath, string reason)
                => $"[Error] Failed to execute menu item '{menuPath}': {reason}";
        }
    }
}