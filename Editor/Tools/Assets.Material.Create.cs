#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System.ComponentModel;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Utils;
using com.MiAO.Unity.MCP.Common;
using UnityEditor;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    public partial class Tool_Assets
    {
        [McpPluginTool
        (
            "Assets_Material_Create",
            Title = "Create Material asset"
        )]
        [Description(@"Create new material asset with default parameters. Right 'shaderName' should be set. To find the shader, use 'Shader.Find' method.")]
        public string Create
        (
            [Description("Asset path. Starts with 'Assets/'. Ends with '.mat'.")]
            string assetPath,
            [Description("Name of the shader that need to be used to create the material.")]
            string shaderName
        )
        => MainThread.Instance.Run(() =>
        {
            if (string.IsNullOrEmpty(assetPath))
                return Error.EmptyAssetPath();

            if (!assetPath.StartsWith("Assets/"))
                return Error.AssetPathMustStartWithAssets(assetPath);

            if (!assetPath.EndsWith(".mat"))
                return Error.AssetPathMustEndWithMat(assetPath);

            var shader = UnityEngine.Shader.Find(shaderName);
            if (shader == null)
                return Error.ShaderNotFound(shaderName);

            // make sure the directory exists
            var directoryPath = System.IO.Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(directoryPath) && !AssetDatabase.IsValidFolder(directoryPath))
            {
                try
                {
                    // create directory structure
                    var parentPath = "Assets";
                    var directories = directoryPath.Replace("Assets/", "").Replace("Assets\\", "").Split('/', '\\');
                    
                    foreach (var dir in directories)
                    {
                        if (string.IsNullOrEmpty(dir)) continue;
                        
                        var newPath = parentPath + "/" + dir;
                        if (!AssetDatabase.IsValidFolder(newPath))
                        {
                            var guid = AssetDatabase.CreateFolder(parentPath, dir);
                            if (string.IsNullOrEmpty(guid))
                            {
                                return $"[Error] Failed to create folder '{newPath}'";
                            }
                        }
                        parentPath = newPath;
                    }
                    AssetDatabase.Refresh();
                }
                catch (System.Exception ex)
                {
                    return $"[Error] Failed to create directory structure for '{directoryPath}': {ex.Message}";
                }
            }

            // check if the asset already exists
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(assetPath) != null)
            {
                return $"[Error] Material already exists at path '{assetPath}'";
            }

            var material = new UnityEngine.Material(shader);
            
            try
            {
                AssetDatabase.CreateAsset(material, assetPath);
            }
            catch (System.Exception ex)
            {
                return $"[Error] Failed to create material asset at '{assetPath}': {ex.Message}";
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var result = Reflector.Instance.Serialize(
                material,
                name: material.name,
                logger: McpPlugin.Instance.Logger
            );
            return $"[Success] Material instanceID '{material.GetInstanceID()}' created at '{assetPath}'.\n{result}";
        });
    }
}