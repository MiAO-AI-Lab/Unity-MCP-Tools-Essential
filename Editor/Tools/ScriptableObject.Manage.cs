#pragma warning disable CS8632
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using com.MiAO.Unity.MCP.Common;
using com.MiAO.Unity.MCP.Utils;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Utils;
using com.IvanMurzak.ReflectorNet;
using UnityEngine;
using UnityEditor;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    public partial class Tool_ScriptableObject
    {
        int DETAILED_SERIALIZED_SCRIPTABLE_OBJECT_MAX_DEPTH = 6;

        [McpPluginTool
        (
            "ScriptableObject_Manage",
            Title = "Manage ScriptableObjects - Create, Delete, Find, Modify"
        )]
        [Description(@"Manage comprehensive ScriptableObject operations including:
- create: Create new ScriptableObject asset with default parameters
- delete: Remove ScriptableObject asset from the project
- find: Locate ScriptableObject asset by path and return its data
- modify: Update ScriptableObject asset's fields and properties")]
        public string Management
        (
            [Description("Operation type: 'create', 'delete', 'find', 'modify'")]
            string operation,
            [Description("Asset path. Starts with 'Assets/'. Ends with '.asset'.")]
            string assetPath,
            [Description("For create: Full name of the ScriptableObject type. Should include full namespace path and class name.")]
            string? typeName = null,
            [Description("For find: If true, it will serialize in normal mode. If false, it will serialize in detailed mode. Default is true.")]
            bool normalSerializationMode = true,
            [Description("For find: If true, it will show properties in serialized ScriptableObject. Default is false.")]
            bool serializeWithProperties = false,
            [Description(@"For modify: Asset modification data. For properties like name, use ""props"": {""typeName"": ""UnityEngine.ScriptableObject"", ""props"": [{""name"": ""name"", ""typeName"": ""System.String"", ""value"": ""NewName""}]}. For Transform position/rotation, use: {""typeName"": ""UnityEngine.Transform"", ""props"": [{""name"": ""position"", ""typeName"": ""UnityEngine.Vector3"", ""value"": {""x"": 1, ""y"": 2, ""z"": 3}}]}. For array (such as Transform[]), use: {""typeName"": ""UnityEngine.Transform"", ""fields"": [{""name"": ""publicArray"", ""typeName"": ""UnityEngine.Transform[]"", ""value"": [-42744, -42754, -42768]}]}.")]
            SerializedMember? assetDiff = null
        )
        {
            return operation.ToLower() switch
            {
                "create" => CreateScriptableObject(assetPath, typeName),
                "delete" => DeleteScriptableObject(assetPath),
                "find" => FindScriptableObject(assetPath, normalSerializationMode, serializeWithProperties),
                "modify" => ModifyScriptableObject(assetPath, assetDiff),
                _ => "[Error] Invalid operation. Valid operations: 'create', 'delete', 'find', 'modify'"
            };
        }

        private string CreateScriptableObject(string assetPath, string? typeName)
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrEmpty(assetPath))
                    return Error.AssetPathIsEmpty();

                if (string.IsNullOrEmpty(typeName))
                    return "[Error] Type name is required for create operation.";

                var type = TypeUtils.GetType(typeName);
                if (type == null)
                    return Error.TypeNotFound(typeName);

                if (!typeof(ScriptableObject).IsAssignableFrom(type))
                    return Error.TypeNotScriptableObject(typeName);

                // check if it is an abstract base class
                if (type == typeof(ScriptableObject))
                    return "[Error] Cannot create instance of abstract ScriptableObject base class. Please specify a concrete derived class.";

                if (type.IsAbstract)
                    return $"[Error] Cannot create instance of abstract class '{typeName}'. Please specify a concrete derived class.";

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
                if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath) != null)
                {
                    return $"[Error] ScriptableObject already exists at path '{assetPath}'";
                }

                var instance = ScriptableObject.CreateInstance(type);
                
                try
                {
                    AssetDatabase.CreateAsset(instance, assetPath);
                }
                catch (System.Exception ex)
                {
                    return $"[Error] Failed to create ScriptableObject asset at '{assetPath}': {ex.Message}";
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                return $"[Success] Created ScriptableObject asset at '{assetPath}'";
            });
        }

        private string DeleteScriptableObject(string assetPath)
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrEmpty(assetPath))
                    return Error.AssetPathIsEmpty();

                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                if (asset == null)
                    return Error.AssetNotFound(assetPath);

                AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.Refresh();

                return $"[Success] Deleted ScriptableObject asset at '{assetPath}'";
            });
        }

        private string FindScriptableObject(string assetPath, bool normalSerializationMode, bool serializeWithProperties)
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrEmpty(assetPath))
                    return Error.AssetPathIsEmpty();

                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                if (asset == null)
                    return Error.AssetNotFound(assetPath);


                try
                {
                    var serializedAsset = ObjectSerializationUtils.SerializeToJson(
                    asset,
                    maxDepth: DETAILED_SERIALIZED_SCRIPTABLE_OBJECT_MAX_DEPTH,
                    mode: normalSerializationMode ? "normal" : "detailed",
                    showProperties: serializeWithProperties,
                    prettyPrint: true
                );

                return @$"[Success] Found ScriptableObject asset.
# Data:
```json
{serializedAsset}
```";
                }
                catch (System.Exception ex)
                {
                    string mode = normalSerializationMode ? "normal" : "detailed";
                    var error = $"[Error] Failed to serialize ScriptableObject {asset.name} in {mode} mode at '{assetPath}': {ex.Message}";
                    Debug.LogError(error);

                    if (normalSerializationMode)
                    {
                        error += $"\n You can try to use detailed serialization mode.";
                    }

                    return error;
                }

            });
        }

        private string ModifyScriptableObject(string assetPath, SerializedMember? assetDiff)
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrEmpty(assetPath))
                    return Error.AssetPathIsEmpty();

                if (assetDiff == null)
                    return "[Error] Asset modification data is required for modify operation.";

                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                if (asset == null)
                    return Error.AssetNotFound(assetPath);

                // check if the diff has neither fields nor props
                if ((assetDiff.fields == null || assetDiff.fields.Count == 0) &&
                    (assetDiff.props == null || assetDiff.props.Count == 0))
                {
                    return "[Error] Asset modification data has neither fields nor props - no modifications to apply.";
                }

                var objToModify = (object)asset;
                var modificationResult = TypeConversionUtils.ProcessObjectModifications(objToModify, assetDiff);

                if (modificationResult.Success)
                {
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();

                    return $"[Success] Modified ScriptableObject asset at '{assetPath}'. {modificationResult.Message}";
                }
                else
                {
                    return $"[Error] Failed to modify ScriptableObject asset at '{assetPath}': {modificationResult.Message}";
                }
            });
        }
    }
} 