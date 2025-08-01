#pragma warning disable CS8632
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using com.MiAO.Unity.MCP.Common;
using com.MiAO.Unity.MCP.Utils;
using com.IvanMurzak.ReflectorNet.Utils;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    public partial class Tool_MenuItem
    {
        [McpPluginTool
        (
            "MenuItem_Execute",
            Title = "Execute Unity Menu Items"
        )]
        [Description(@"Execute Unity Editor menu items by their menu path with security whitelist protection. This tool can:
- Execute built-in Unity menu items (File, Edit, Assets, GameObject, Component, Window, Help menus)
- Execute custom menu items from plugins and scripts
- Handle menu items with validation and security checks
- Support parameterized menu execution
- Enforce whitelist-based security policy")]
        public string Execute
        (
            [Description("The full menu path to execute. Use forward slashes to separate menu levels. Example: 'Assets/Create/C# Script' or 'GameObject/Create Empty'")]
            string menuPath,
            [Description("Optional: Validate the menu item before execution. If true, checks if the menu item is available/enabled. Default is true.")]
            bool validateBeforeExecution = true,
            [Description("Optional: Show detailed information about the execution result. Default is false.")]
            bool verbose = false,
            [Description("Optional: Bypass security whitelist check (use with extreme caution). Default is false.")]
            bool bypassSecurity = false
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrEmpty(menuPath))
                    return Error.EmptyMenuPath();

                try
                {
                    // Clean up the menu path
                    menuPath = menuPath.Trim();
                    
                    // Security whitelist check
                    if (!bypassSecurity)
                    {
                        var securityCheck = CheckMenuItemSecurity(menuPath);
                        if (!securityCheck.IsAllowed)
                        {
                            if (verbose)
                            {
                                Debug.LogWarning($"[MenuItem Execute] Security check failed for: {menuPath}. Reason: {securityCheck.Reason}");
                            }
                            return securityCheck.Message;
                        }
                        
                        if (verbose && securityCheck.WarningMessage != null)
                        {
                            Debug.LogWarning($"[MenuItem Execute] Security warning for: {menuPath}. {securityCheck.WarningMessage}");
                        }
                    }
                    else if (verbose)
                    {
                        Debug.LogWarning($"[MenuItem Execute] Security check bypassed for: {menuPath}. This should only be used by trusted administrators.");
                    }
                    
                    if (verbose)
                        Debug.Log($"[MenuItem Execute] Attempting to execute menu item: {menuPath}");

                    // Validate menu item exists and is enabled if requested
                    if (validateBeforeExecution)
                    {
                        bool menuExists = Menu.GetEnabled(menuPath);
                        bool menuChecked = Menu.GetChecked(menuPath);
                        
                        if (verbose)
                        {
                            Debug.Log($"[MenuItem Execute] Menu validation - Exists/Enabled: {menuExists}, Checked: {menuChecked}");
                        }
                        
                        // Try to find if the menu item exists at all
                        var menuExistsAtAll = DoesMenuItemExist(menuPath);
                        if (!menuExistsAtAll)
                        {
                            return Error.MenuItemNotFound(menuPath);
                        }
                        
                        if (!menuExists)
                        {
                            return Error.MenuItemDisabled(menuPath) + " Attempting execution anyway...";
                        }
                    }

                    // Execute the menu item
                    EditorApplication.ExecuteMenuItem(menuPath);
                    
                    string result = $"[Success] Successfully executed menu item: '{menuPath}'";
                    
                    if (verbose)
                    {
                        result += $"\n- Validation was {(validateBeforeExecution ? "enabled" : "disabled")}";
                        result += $"\n- Security check was {(bypassSecurity ? "bypassed" : "enforced")}";
                        result += $"\n- Execution completed at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                    }
                    
                    return result;
                }
                catch (ArgumentException ex)
                {
                    return Error.InvalidMenuPath(menuPath) + $" Details: {ex.Message}";
                }
                catch (Exception ex)
                {
                    string errorMsg = Error.ExecutionFailed(menuPath, ex.Message);
                    if (verbose)
                    {
                        errorMsg += $"\n- Exception type: {ex.GetType().Name}";
                        errorMsg += $"\n- Stack trace: {ex.StackTrace}";
                    }
                    return errorMsg;
                }
            });
        }

        [McpPluginTool
        (
            "MenuItem_List",
            Title = "List Available Menu Items"
        )]
        [Description(@"List available Unity Editor menu items. This can help you find the correct menu path for execution.")]
        public string List
        (
            [Description("Optional: Filter menu items by category. Examples: 'Assets', 'GameObject', 'Component', 'Window', 'Help', 'Edit', 'File'. Leave empty to show common menu categories.")]
            string? category = null,
            [Description("Optional: Show only enabled menu items. Default is false (shows all).")]
            bool enabledOnly = false
        )
        {
            return MainThread.Instance.Run(() =>
            {
                try
                {
                    var menuItems = new List<string>();
                    
                    if (string.IsNullOrEmpty(category))
                    {
                        // Return common menu categories and some popular items
                        return @"[Success] Common Unity Menu Categories:

**Main Categories:**
- File/ (File operations: New Scene, Open Scene, Save, Build Settings, etc.)
- Edit/ (Edit operations: Undo, Redo, Cut, Copy, Paste, Project Settings, etc.)
- Assets/ (Asset operations: Create, Import, Export, Refresh, etc.)
- GameObject/ (GameObject operations: Create Empty, Create UI, etc.)
- Component/ (Component operations: Add components to GameObjects)
- Window/ (Window management: General, Animation, Audio, etc.)
- Help/ (Help and documentation)

**Popular Menu Items:**
- Assets/Create/C# Script
- Assets/Create/Folder
- Assets/Refresh
- GameObject/Create Empty
- GameObject/3D Object/Cube
- GameObject/UI/Canvas
- File/New Scene
- File/Save Scene
- Edit/Project Settings...
- Window/General/Console
- Window/General/Inspector

Use the category parameter to explore specific menu sections.";
                    }
                    
                    // For specific categories, we can provide some known menu items
                    var commonMenuItems = GetCommonMenuItemsForCategory(category.ToLower());
                    
                    if (commonMenuItems.Count > 0)
                    {
                        var result = $"[Success] Common menu items in '{category}' category:\n\n";
                        foreach (var item in commonMenuItems)
                        {
                            bool isEnabled = enabledOnly ? Menu.GetEnabled(item) : true;
                            if (!enabledOnly || isEnabled)
                            {
                                string status = enabledOnly ? "" : $" (Enabled: {Menu.GetEnabled(item)})";
                                result += $"- {item}{status}\n";
                            }
                        }
                        return result.TrimEnd();
                    }
                    
                    return $"[Info] No predefined menu items found for category '{category}'. Try using common categories like 'Assets', 'GameObject', 'File', 'Edit', 'Window', or 'Component'.";
                }
                catch (Exception ex)
                {
                    return $"[Error] Failed to list menu items: {ex.Message}";
                }
            });
        }

        private bool DoesMenuItemExist(string menuPath)
        {
            try
            {
                // Try to get menu status - this will not throw for non-existent menus but might return false
                // We use reflection to access internal Unity menu system
                var editorApplicationType = typeof(EditorApplication);
                var executeMenuItemMethod = editorApplicationType.GetMethod("ExecuteMenuItem", BindingFlags.Static | BindingFlags.Public);
                
                if (executeMenuItemMethod != null)
                {
                    // If we can call ExecuteMenuItem without exception during validation, the menu likely exists
                    // However, we should be careful not to actually execute it during validation
                    
                    // For now, we'll use a simple approach: check if the menu path follows expected patterns
                    var validPrefixes = new[] { "File/", "Edit/", "Assets/", "GameObject/", "Component/", "Window/", "Help/" };
                    
                    foreach (var prefix in validPrefixes)
                    {
                        if (menuPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    
                    // Also check for custom menu items (usually don't start with standard prefixes)
                    return menuPath.Contains("/");
                }
                
                return true; // Assume it exists if we can't validate properly
            }
            catch
            {
                return false;
            }
        }

        private List<string> GetCommonMenuItemsForCategory(string category)
        {
            return category switch
            {
                "file" => new List<string>
                {
                    "File/New Scene",
                    "File/Open Scene",
                    "File/Save",
                    "File/Save As...",
                    "File/Save Scene",
                    "File/Save Scene As...",
                    "File/Build Settings...",
                    "File/Build And Run"
                },
                "edit" => new List<string>
                {
                    "Edit/Undo",
                    "Edit/Redo", 
                    "Edit/Cut",
                    "Edit/Copy",
                    "Edit/Paste",
                    "Edit/Project Settings...",
                    "Edit/Preferences..."
                },
                "assets" => new List<string>
                {
                    "Assets/Create/Folder",
                    "Assets/Create/C# Script",
                    "Assets/Create/Material",
                    "Assets/Create/Prefab",
                    "Assets/Import New Asset...",
                    "Assets/Refresh",
                    "Assets/Reimport"
                },
                "gameobject" => new List<string>
                {
                    "GameObject/Create Empty",
                    "GameObject/Create Empty Child",
                    "GameObject/3D Object/Cube",
                    "GameObject/3D Object/Sphere",
                    "GameObject/3D Object/Plane",
                    "GameObject/UI/Canvas",
                    "GameObject/UI/Button",
                    "GameObject/UI/Text"
                },
                "component" => new List<string>
                {
                    "Component/Transform",
                    "Component/Mesh/Mesh Filter",
                    "Component/Mesh/Mesh Renderer",
                    "Component/Physics/Rigidbody",
                    "Component/Physics/Collider",
                    "Component/Audio/Audio Source"
                },
                "window" => new List<string>
                {
                    "Window/General/Console",
                    "Window/General/Inspector",
                    "Window/General/Hierarchy",
                    "Window/General/Project",
                    "Window/General/Scene",
                    "Window/Animation/Animation",
                    "Window/Audio/Audio Mixer"
                },
                "help" => new List<string>
                {
                    "Help/About Unity",
                    "Help/Unity Manual",
                    "Help/Scripting Reference"
                },
                _ => new List<string>()
            };
        }

        private SecurityCheckResult CheckMenuItemSecurity(string menuPath)
        {
            try
            {
                var whitelistFilePath = Path.Combine(Application.dataPath, "..", "MenuItem-Whitelist.json");
                
                // If whitelist file doesn't exist, create default configuration
                if (!File.Exists(whitelistFilePath))
                {
                    return new SecurityCheckResult
                    {
                        IsAllowed = false,
                        Message = "[Security] Whitelist configuration not found. Use MenuItem_ManageWhitelist with 'init' operation to create default security configuration.",
                        Reason = "Whitelist file not found"
                    };
                }

                var configText = File.ReadAllText(whitelistFilePath);
                var config = JsonConvert.DeserializeObject<JObject>(configText);
                
                if (config == null)
                {
                    return new SecurityCheckResult
                    {
                        IsAllowed = false,
                        Message = "[Security] Failed to parse whitelist configuration.",
                        Reason = "Invalid configuration file"
                    };
                }

                // Check if whitelist is enabled
                var settings = config["settings"];
                var enableWhitelist = settings?["enableWhitelist"]?.Value<bool>() ?? true;
                
                if (!enableWhitelist)
                {
                    return new SecurityCheckResult
                    {
                        IsAllowed = true,
                        WarningMessage = "Whitelist is disabled - all menu items are allowed"
                    };
                }

                var defaultWhitelist = config["defaultWhitelist"]?.ToObject<List<WhitelistEntry>>() ?? new List<WhitelistEntry>();
                var customWhitelist = config["customWhitelist"]?.ToObject<List<WhitelistEntry>>() ?? new List<WhitelistEntry>();
                
                var allEntries = defaultWhitelist.Concat(customWhitelist).ToList();
                
                // Find matching entry
                var matchingEntry = allEntries.FirstOrDefault(e => e.Path?.Equals(menuPath, StringComparison.OrdinalIgnoreCase) == true);
                
                if (matchingEntry == null)
                {
                    var strictMode = settings?["strictMode"]?.Value<bool>() ?? false;
                    var allowCustomMenuItems = settings?["allowCustomMenuItems"]?.Value<bool>() ?? true;
                    
                    if (strictMode || !allowCustomMenuItems)
                    {
                        return new SecurityCheckResult
                        {
                            IsAllowed = false,
                            Message = $"[Security] Menu item '{menuPath}' is not in the whitelist. Add it using MenuItem_ManageWhitelist.",
                            Reason = "Not in whitelist"
                        };
                    }
                    else
                    {
                        return new SecurityCheckResult
                        {
                            IsAllowed = true,
                            WarningMessage = "Menu item not in whitelist but custom items are allowed"
                        };
                    }
                }

                if (!matchingEntry.Enabled)
                {
                    return new SecurityCheckResult
                    {
                        IsAllowed = false,
                        Message = $"[Security] Menu item '{menuPath}' is disabled in the whitelist. Enable it using MenuItem_ManageWhitelist.",
                        Reason = "Disabled in whitelist"
                    };
                }

                // Log the attempt if enabled
                var logAttempts = settings?["logAttempts"]?.Value<bool>() ?? true;
                if (logAttempts)
                {
                    var riskIcon = GetRiskLevelIcon(matchingEntry.RiskLevel);
                    Debug.Log($"[MenuItem Security] Allowing execution of {riskIcon} {menuPath} (Risk: {matchingEntry.RiskLevel ?? "unknown"})");
                }

                var warningMessage = GetRiskWarningMessage(matchingEntry.RiskLevel);
                
                return new SecurityCheckResult
                {
                    IsAllowed = true,
                    WarningMessage = warningMessage
                };
            }
            catch (Exception ex)
            {
                return new SecurityCheckResult
                {
                    IsAllowed = false,
                    Message = $"[Security] Failed to check whitelist: {ex.Message}",
                    Reason = "Security check error"
                };
            }
        }

        private string? GetRiskWarningMessage(string? riskLevel)
        {
            return riskLevel?.ToLower() switch
            {
                "advanced" => "This is an advanced operation that may modify project settings",
                "restricted" => "This is a potentially dangerous operation that may have significant impact",
                _ => null
            };
        }

        public class SecurityCheckResult
        {
            public bool IsAllowed { get; set; }
            public string? Message { get; set; }
            public string? Reason { get; set; }
            public string? WarningMessage { get; set; }
        }
    }
}