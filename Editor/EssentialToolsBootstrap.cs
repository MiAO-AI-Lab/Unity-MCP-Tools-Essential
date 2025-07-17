using System.Reflection;
using UnityEngine;
using UnityEditor;
using com.MiAO.Unity.MCP.Bootstrap;
using com.MiAO.Unity.MCP.Common;

namespace com.MiAO.Unity.MCP.Essential
{
    /// <summary>
    /// Essential Tools Bootstrap - Simplified bootstrap using Universal Package Bootstrap Framework
    /// Automatically initializes and registers essential tools when the package is loaded
    /// </summary>
    [InitializeOnLoad]
    public static class EssentialToolsBootstrap
    {
        // Package configuration
        private const string PackageName = "com.miao.unity.mcp.essential";
        private const string DisplayName = "Essential Tools";

        /// <summary>
        /// Static constructor - automatically called when Unity loads this assembly
        /// </summary>
        static EssentialToolsBootstrap()
        {
            // Create package configuration using the simplified method
            var config = UniversalPackageBootstrap.CreateSimpleConfig(
                PackageName, 
                DisplayName, 
                Assembly.GetExecutingAssembly()
            );
            
            // Bootstrap using Universal Package Bootstrap Framework
            UniversalPackageBootstrap.Bootstrap(config);
        }

    }
} 