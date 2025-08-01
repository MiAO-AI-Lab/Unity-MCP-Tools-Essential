#pragma warning disable CS8632
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using com.MiAO.Unity.MCP.Common;
using com.MiAO.Unity.MCP.Utils;
using com.IvanMurzak.ReflectorNet.Utils;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    public partial class Tool_MenuItem
    {
        private static readonly string WhitelistFilePath = Path.Combine(Application.dataPath, "..", "MenuItem-Whitelist.json");

        [McpPluginTool
        (
            "MenuItem_ManageWhitelist",
            Title = "Manage MenuItem Security Whitelist"
        )]
        [Description(@"Manage the security whitelist for MenuItem execution. Operations include:
- init: Initialize default whitelist configuration
- list: View current whitelist entries
- add: Add menu item to whitelist
- remove: Remove menu item from whitelist
- enable: Enable menu item in whitelist
- disable: Disable menu item in whitelist
- status: Check whitelist status and settings")]
        public string ManageWhitelist
        (
            [Description("Operation type: 'init', 'list', 'add', 'remove', 'enable', 'disable', 'status'")]
            string operation,
            [Description("For add/remove/enable/disable: Menu path to operate on")]
            string? menuPath = null,
            [Description("For add: Description of the menu item")]
            string? description = null,
            [Description("For add: Risk level (safe, moderate, advanced, restricted)")]
            string? riskLevel = "moderate",
            [Description("For add: Category (assets, file, edit, gameobject, component, window, help)")]
            string? category = "custom",
            [Description("For add: Additional notes about the menu item")]
            string? notes = null,
            [Description("For list: Filter by risk level (safe, moderate, advanced, restricted)")]
            string? filterRiskLevel = null,
            [Description("For list: Filter by category")]
            string? filterCategory = null,
            [Description("For list: Show only enabled items")]
            bool enabledOnly = false
        )
        {
            return MainThread.Instance.Run(() =>
            {
                try
                {
                    return operation.ToLower() switch
                    {
                        "init" => InitializeWhitelist(),
                        "list" => ListWhitelistEntries(filterRiskLevel, filterCategory, enabledOnly),
                        "add" => AddToWhitelist(menuPath, description, riskLevel, category, notes),
                        "remove" => RemoveFromWhitelist(menuPath),
                        "enable" => EnableWhitelistEntry(menuPath),
                        "disable" => DisableWhitelistEntry(menuPath),
                        "status" => GetWhitelistStatus(),
                        _ => "[Error] Invalid operation. Valid operations: 'init', 'list', 'add', 'remove', 'enable', 'disable', 'status'"
                    };
                }
                catch (Exception ex)
                {
                    return $"[Error] Failed to manage whitelist: {ex.Message}";
                }
            });
        }

        private string InitializeWhitelist()
        {
            try
            {
                if (File.Exists(WhitelistFilePath))
                {
                    return $"[Info] Whitelist file already exists at: {WhitelistFilePath}. Use 'status' operation to view current configuration.";
                }

                // Read the default configuration template
                var defaultConfigPath = Path.Combine(Application.dataPath, "..", "MenuItem-Whitelist.json");
                
                // Create default configuration if it doesn't exist
                var defaultConfig = CreateDefaultWhitelistConfig();
                
                File.WriteAllText(WhitelistFilePath, JsonConvert.SerializeObject(defaultConfig, Formatting.Indented));
                
                return $"[Success] Initialized default whitelist configuration at: {WhitelistFilePath}";
            }
            catch (Exception ex)
            {
                return $"[Error] Failed to initialize whitelist: {ex.Message}";
            }
        }

        private string ListWhitelistEntries(string? filterRiskLevel, string? filterCategory, bool enabledOnly)
        {
            try
            {
                if (!File.Exists(WhitelistFilePath))
                {
                    return "[Error] Whitelist file not found. Use 'init' operation to create default configuration.";
                }

                var configText = File.ReadAllText(WhitelistFilePath);
                var config = JsonConvert.DeserializeObject<JObject>(configText);
                
                if (config == null)
                {
                    return "[Error] Failed to parse whitelist configuration.";
                }

                var defaultWhitelist = config["defaultWhitelist"]?.ToObject<List<WhitelistEntry>>() ?? new List<WhitelistEntry>();
                var customWhitelist = config["customWhitelist"]?.ToObject<List<WhitelistEntry>>() ?? new List<WhitelistEntry>();
                
                var allEntries = defaultWhitelist.Concat(customWhitelist).ToList();

                // Apply filters
                if (!string.IsNullOrEmpty(filterRiskLevel))
                {
                    allEntries = allEntries.Where(e => e.RiskLevel?.Equals(filterRiskLevel, StringComparison.OrdinalIgnoreCase) == true).ToList();
                }
                
                if (!string.IsNullOrEmpty(filterCategory))
                {
                    allEntries = allEntries.Where(e => e.Category?.Equals(filterCategory, StringComparison.OrdinalIgnoreCase) == true).ToList();
                }
                
                if (enabledOnly)
                {
                    allEntries = allEntries.Where(e => e.Enabled).ToList();
                }

                if (!allEntries.Any())
                {
                    return "[Info] No whitelist entries found matching the specified criteria.";
                }

                var result = $"[Success] Whitelist Entries ({allEntries.Count} items):\n\n";
                
                var groupedEntries = allEntries.GroupBy(e => e.Category).OrderBy(g => g.Key);
                
                foreach (var group in groupedEntries)
                {
                    result += $"**{group.Key?.ToUpper() ?? "UNKNOWN"}**\n";
                    foreach (var entry in group.OrderBy(e => e.Path))
                    {
                        var status = entry.Enabled ? "✅" : "❌";
                        var riskIcon = GetRiskLevelIcon(entry.RiskLevel);
                        result += $"{status} {riskIcon} {entry.Path}\n";
                        result += $"    Description: {entry.Description ?? "No description"}\n";
                        if (!string.IsNullOrEmpty(entry.Notes))
                        {
                            result += $"    Notes: {entry.Notes}\n";
                        }
                        result += "\n";
                    }
                }

                return result.TrimEnd();
            }
            catch (Exception ex)
            {
                return $"[Error] Failed to list whitelist entries: {ex.Message}";
            }
        }

        private string AddToWhitelist(string? menuPath, string? description, string? riskLevel, string? category, string? notes)
        {
            try
            {
                if (string.IsNullOrEmpty(menuPath))
                {
                    return "[Error] Menu path is required for add operation.";
                }

                if (!File.Exists(WhitelistFilePath))
                {
                    var initResult = InitializeWhitelist();
                    if (initResult.StartsWith("[Error]"))
                        return initResult;
                }

                var configText = File.ReadAllText(WhitelistFilePath);
                var config = JsonConvert.DeserializeObject<JObject>(configText);
                
                if (config == null)
                {
                    return "[Error] Failed to parse whitelist configuration.";
                }

                // Check if entry already exists
                var defaultWhitelist = config["defaultWhitelist"]?.ToObject<List<WhitelistEntry>>() ?? new List<WhitelistEntry>();
                var customWhitelist = config["customWhitelist"]?.ToObject<List<WhitelistEntry>>() ?? new List<WhitelistEntry>();
                
                if (defaultWhitelist.Any(e => e.Path == menuPath) || customWhitelist.Any(e => e.Path == menuPath))
                {
                    return $"[Error] Menu item '{menuPath}' already exists in whitelist.";
                }

                var newEntry = new WhitelistEntry
                {
                    Path = menuPath,
                    Description = description ?? $"Custom menu item: {menuPath}",
                    RiskLevel = riskLevel ?? "moderate",
                    Category = category ?? "custom",
                    Enabled = true,
                    Notes = notes ?? "Added by user"
                };

                customWhitelist.Add(newEntry);
                config["customWhitelist"] = JArray.FromObject(customWhitelist);
                config["lastUpdated"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
                config["metadata"]!["lastModifiedBy"] = "user";

                File.WriteAllText(WhitelistFilePath, JsonConvert.SerializeObject(config, Formatting.Indented));
                
                return $"[Success] Added '{menuPath}' to whitelist with risk level '{riskLevel}' in category '{category}'.";
            }
            catch (Exception ex)
            {
                return $"[Error] Failed to add to whitelist: {ex.Message}";
            }
        }

        private string RemoveFromWhitelist(string? menuPath)
        {
            try
            {
                if (string.IsNullOrEmpty(menuPath))
                {
                    return "[Error] Menu path is required for remove operation.";
                }

                if (!File.Exists(WhitelistFilePath))
                {
                    return "[Error] Whitelist file not found.";
                }

                var configText = File.ReadAllText(WhitelistFilePath);
                var config = JsonConvert.DeserializeObject<JObject>(configText);
                
                if (config == null)
                {
                    return "[Error] Failed to parse whitelist configuration.";
                }

                var customWhitelist = config["customWhitelist"]?.ToObject<List<WhitelistEntry>>() ?? new List<WhitelistEntry>();
                
                var entryToRemove = customWhitelist.FirstOrDefault(e => e.Path == menuPath);
                if (entryToRemove == null)
                {
                    // Check if it's in default whitelist (cannot be removed)
                    var defaultWhitelist = config["defaultWhitelist"]?.ToObject<List<WhitelistEntry>>() ?? new List<WhitelistEntry>();
                    if (defaultWhitelist.Any(e => e.Path == menuPath))
                    {
                        return $"[Error] Cannot remove '{menuPath}' as it's a default whitelist entry. Use 'disable' operation instead.";
                    }
                    return $"[Error] Menu item '{menuPath}' not found in custom whitelist.";
                }

                customWhitelist.Remove(entryToRemove);
                config["customWhitelist"] = JArray.FromObject(customWhitelist);
                config["lastUpdated"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
                config["metadata"]!["lastModifiedBy"] = "user";

                File.WriteAllText(WhitelistFilePath, JsonConvert.SerializeObject(config, Formatting.Indented));
                
                return $"[Success] Removed '{menuPath}' from whitelist.";
            }
            catch (Exception ex)
            {
                return $"[Error] Failed to remove from whitelist: {ex.Message}";
            }
        }

        private string EnableWhitelistEntry(string? menuPath)
        {
            return SetWhitelistEntryStatus(menuPath, true);
        }

        private string DisableWhitelistEntry(string? menuPath)
        {
            return SetWhitelistEntryStatus(menuPath, false);
        }

        private string SetWhitelistEntryStatus(string? menuPath, bool enabled)
        {
            try
            {
                if (string.IsNullOrEmpty(menuPath))
                {
                    return "[Error] Menu path is required for enable/disable operation.";
                }

                if (!File.Exists(WhitelistFilePath))
                {
                    return "[Error] Whitelist file not found.";
                }

                var configText = File.ReadAllText(WhitelistFilePath);
                var config = JsonConvert.DeserializeObject<JObject>(configText);
                
                if (config == null)
                {
                    return "[Error] Failed to parse whitelist configuration.";
                }

                bool updated = false;
                var action = enabled ? "enable" : "disable";

                // Check in default whitelist
                var defaultWhitelist = config["defaultWhitelist"]?.ToObject<List<WhitelistEntry>>() ?? new List<WhitelistEntry>();
                var defaultEntry = defaultWhitelist.FirstOrDefault(e => e.Path == menuPath);
                if (defaultEntry != null)
                {
                    defaultEntry.Enabled = enabled;
                    config["defaultWhitelist"] = JArray.FromObject(defaultWhitelist);
                    updated = true;
                }

                // Check in custom whitelist
                var customWhitelist = config["customWhitelist"]?.ToObject<List<WhitelistEntry>>() ?? new List<WhitelistEntry>();
                var customEntry = customWhitelist.FirstOrDefault(e => e.Path == menuPath);
                if (customEntry != null)
                {
                    customEntry.Enabled = enabled;
                    config["customWhitelist"] = JArray.FromObject(customWhitelist);
                    updated = true;
                }

                if (!updated)
                {
                    return $"[Error] Menu item '{menuPath}' not found in whitelist.";
                }

                config["lastUpdated"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
                config["metadata"]!["lastModifiedBy"] = "user";

                File.WriteAllText(WhitelistFilePath, JsonConvert.SerializeObject(config, Formatting.Indented));
                
                return $"[Success] {(enabled ? "Enabled" : "Disabled")} '{menuPath}' in whitelist.";
            }
            catch (Exception ex)
            {
                return $"[Error] Failed to {(enabled ? "enable" : "disable")} whitelist entry: {ex.Message}";
            }
        }

        private string GetWhitelistStatus()
        {
            try
            {
                if (!File.Exists(WhitelistFilePath))
                {
                    return "[Warning] Whitelist file not found. Use 'init' operation to create default configuration.";
                }

                var configText = File.ReadAllText(WhitelistFilePath);
                var config = JsonConvert.DeserializeObject<JObject>(configText);
                
                if (config == null)
                {
                    return "[Error] Failed to parse whitelist configuration.";
                }

                var settings = config["settings"];
                var defaultWhitelist = config["defaultWhitelist"]?.ToObject<List<WhitelistEntry>>() ?? new List<WhitelistEntry>();
                var customWhitelist = config["customWhitelist"]?.ToObject<List<WhitelistEntry>>() ?? new List<WhitelistEntry>();
                
                var totalEntries = defaultWhitelist.Count + customWhitelist.Count;
                var enabledEntries = defaultWhitelist.Count(e => e.Enabled) + customWhitelist.Count(e => e.Enabled);
                
                var result = $"[Success] Whitelist Configuration Status:\n\n";
                result += $"**Configuration File:** {WhitelistFilePath}\n";
                result += $"**Last Updated:** {config["lastUpdated"]}\n";
                result += $"**Version:** {config["version"]}\n\n";
                
                result += $"**Settings:**\n";
                result += $"- Whitelist Enabled: {settings?["enableWhitelist"] ?? "true"}\n";
                result += $"- Strict Mode: {settings?["strictMode"] ?? "false"}\n";
                result += $"- Allow Custom Menu Items: {settings?["allowCustomMenuItems"] ?? "true"}\n";
                result += $"- Log Attempts: {settings?["logAttempts"] ?? "true"}\n\n";
                
                result += $"**Statistics:**\n";
                result += $"- Total Entries: {totalEntries}\n";
                result += $"- Enabled Entries: {enabledEntries}\n";
                result += $"- Disabled Entries: {totalEntries - enabledEntries}\n";
                result += $"- Default Entries: {defaultWhitelist.Count}\n";
                result += $"- Custom Entries: {customWhitelist.Count}\n\n";
                
                var riskCounts = defaultWhitelist.Concat(customWhitelist)
                    .GroupBy(e => e.RiskLevel)
                    .ToDictionary(g => g.Key ?? "unknown", g => g.Count());
                    
                result += $"**Risk Level Distribution:**\n";
                foreach (var risk in new[] { "safe", "moderate", "advanced", "restricted" })
                {
                    var count = riskCounts.GetValueOrDefault(risk, 0);
                    var icon = GetRiskLevelIcon(risk);
                    result += $"- {icon} {risk}: {count}\n";
                }

                return result.TrimEnd();
            }
            catch (Exception ex)
            {
                return $"[Error] Failed to get whitelist status: {ex.Message}";
            }
        }

        private JObject CreateDefaultWhitelistConfig()
        {
            var defaultConfigText = @"{
  ""version"": ""1.0"",
  ""description"": ""MenuItem Execute Security Whitelist Configuration"",
  ""lastUpdated"": """ + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") + @""",
  ""settings"": {
    ""enableWhitelist"": true,
    ""strictMode"": false,
    ""allowCustomMenuItems"": true,
    ""logAttempts"": true
  },
  ""categories"": {
    ""safe"": {
      ""description"": ""Safe operations with no risk"",
      ""color"": ""green""
    },
    ""moderate"": {
      ""description"": ""Operations that modify project state but are generally safe"",
      ""color"": ""yellow""
    },
    ""advanced"": {
      ""description"": ""Operations requiring careful consideration"",
      ""color"": ""orange""
    },
    ""restricted"": {
      ""description"": ""Potentially dangerous operations - disabled by default"",
      ""color"": ""red""
    }
  },
  ""defaultWhitelist"": [],
  ""customWhitelist"": [],
  ""metadata"": {
    ""createdBy"": ""Unity-MCP MenuItem Security System"",
    ""projectPath"": """ + Application.dataPath + @""",
    ""unityVersion"": """ + Application.unityVersion + @""",
    ""lastModifiedBy"": ""system""
  }
}";
            return JsonConvert.DeserializeObject<JObject>(defaultConfigText)!;
        }

        private string GetRiskLevelIcon(string? riskLevel)
        {
            return riskLevel?.ToLower() switch
            {
                "safe" => "🟢",
                "moderate" => "🟡",
                "advanced" => "🟠",
                "restricted" => "🔴",
                _ => "⚪"
            };
        }

        public class WhitelistEntry
        {
            [JsonProperty("path")]
            public string Path { get; set; } = "";
            
            [JsonProperty("description")]
            public string? Description { get; set; }
            
            [JsonProperty("riskLevel")]
            public string? RiskLevel { get; set; }
            
            [JsonProperty("category")]
            public string? Category { get; set; }
            
            [JsonProperty("enabled")]
            public bool Enabled { get; set; }
            
            [JsonProperty("notes")]
            public string? Notes { get; set; }
        }
    }
}