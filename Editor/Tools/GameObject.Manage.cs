#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using com.MiAO.Unity.MCP.Common;
using com.IvanMurzak.ReflectorNet.Model.Unity;
using com.MiAO.Unity.MCP.Utils;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Utils;
using com.IvanMurzak.ReflectorNet;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace com.MiAO.Unity.MCP.Essential.Tools
{



    public partial class Tool_GameObject
    {
        private class ModificationResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = "";
        }
        [McpPluginTool
        (
            "GameObject_Manage",
            Title = "Manage GameObjects - Create, Destroy, Duplicate, Modify, SetParent, SetActive, SetComponentActive"
        )]
        [Description(@"Manage comprehensive GameObject operations including:
- create: Create a new GameObject at specific path
- destroy: Remove a GameObject and all nested GameObjects recursively
- duplicate: Clone GameObjects in opened Prefab or in a Scene
- modify: Update GameObjects and/or attached component's field and properties (IMPORTANT: For GameObject properties like name/tag/layer, use ""props"" array: [{""typeName"": ""UnityEngine.GameObject"", ""props"": [{""name"": ""name"", ""typeName"": ""System.String"", ""value"": ""NewName""}]}]. For Transform position/rotation, use: [{""typeName"": ""UnityEngine.Transform"", ""props"": [{""name"": ""position"", ""typeName"": ""UnityEngine.Vector3"", ""value"": {""x"": 1, ""y"": 2, ""z"": 3}}]}]. For array (such as Transform[]), use: [{""typeName"": ""UnityEngine.Transform"", ""fields"": [{""name"": ""publicArray"", ""typeName"": ""UnityEngine.Transform[]"", ""value"": [-42744, -42754, -42768]}]}]. Always use ""props"" for properties, ""fields"" for public variables. )
- setParent: Assign parent GameObject for target GameObjects
- setActive: Set active state of GameObjects
- setComponentActive: Enable/disable specific components on GameObjects")]
        public string Operations
        (
            [Description("Operation type: 'create', 'destroy', 'duplicate', 'modify', 'setParent', 'setActive', 'setComponentActive'")]
            string operation,
            [Description("GameObject reference for operations (required for destroy, duplicate, modify, setParent, setActive, setComponentActive)")]
            GameObjectRef? gameObjectRef = null,
            [Description("List of GameObject references for operations that support multiple objects")]
            GameObjectRefList? gameObjectRefs = null,
            [Description("For create: Name of the new GameObject")]
            string? name = null,
            [Description("For create/setParent: Parent GameObject reference")]
            GameObjectRef? parentGameObjectRef = null,
            [Description("For create: Transform position of the GameObject")]
            Vector3? position = null,
            [Description("For create: Transform rotation of the GameObject in Euler angles")]
            Vector3? rotation = null,
            [Description("For create: Transform scale of the GameObject")]
            Vector3? scale = null,
            [Description("For create: Array of positions for batch creation")]
            Vector3[]? positions = null,
            [Description("For create: Array of rotations for batch creation")]
            Vector3[]? rotations = null,
            [Description("For create: World or Local space of transform")]
            bool isLocalSpace = false,
            [Description("For create: -1 - No primitive type; 0 - Cube; 1 - Sphere; 2 - Capsule; 3 - Cylinder; 4 - Plane; 5 - Quad")]
            int primitiveType = -1,
            [Description("For modify: GameObject modification data")]
            SerializedMemberList? gameObjectDiffs = null,
            [Description("For setParent: Whether GameObject's world position should remain unchanged when setting parent")]
            bool worldPositionStays = true,
            [Description("For setActive: Whether to set GameObject active (true) or inactive (false)")]
            bool active = true,
            [Description("For setComponentActive: Full component type name to enable/disable (e.g., 'UnityEngine.MeshRenderer')")]
            string? componentTypeName = null,
            [Description("For setComponentActive: Whether to enable (true) or disable (false) the component")]
            bool? componentActive = true
        )
        {
            return operation.ToLower() switch
            {
                "create" => CreateGameObject(name, parentGameObjectRef, position, rotation, scale, positions, rotations, isLocalSpace, primitiveType),
                "destroy" => DestroyGameObject(gameObjectRef),
                "duplicate" => DuplicateGameObjects(gameObjectRefs ?? (gameObjectRef != null ? new GameObjectRefList { gameObjectRef } : new GameObjectRefList())),
                "modify" => ModifyGameObjects(gameObjectDiffs, gameObjectRefs ?? (gameObjectRef != null ? GenerateGameObjectRefListFromSingleGameObjectRef(gameObjectRef, gameObjectDiffs?.Count ?? 0) : new GameObjectRefList())),
                "setparent" => SetParentGameObjects(gameObjectRefs ?? (gameObjectRef != null ? new GameObjectRefList { gameObjectRef } : new GameObjectRefList()), parentGameObjectRef, worldPositionStays),
                "setactive" => SetActiveGameObjects(gameObjectRefs ?? (gameObjectRef != null ? new GameObjectRefList { gameObjectRef } : new GameObjectRefList()), active),
                "setcomponentactive" => SetComponentActiveGameObjects(gameObjectRefs ?? (gameObjectRef != null ? new GameObjectRefList { gameObjectRef } : new GameObjectRefList()), componentTypeName, componentActive),
                _ => "[Error] Invalid operation. Valid operations: 'create', 'destroy', 'duplicate', 'modify', 'setParent', 'setActive', 'setComponentActive'"
            };
        }

        private GameObjectRefList GenerateGameObjectRefListFromSingleGameObjectRef(GameObjectRef gameObjectRef, int copiesCount)
        {
            var list = new GameObjectRefList();
            for (int i = 0; i < copiesCount; i++)
            {
                list.Add(gameObjectRef);
            }
            return list;
        }



        private GameObject FindGameObjectByPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
                
            var parts = path.Split('/');
            GameObject current = null;
            
            // Find root object
            foreach (var rootGo in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (rootGo.name == parts[0])
                {
                    current = rootGo;
                    break;
                }
            }
            
            if (current == null)
                return null;
                
            // Find child objects in sequence
            for (int i = 1; i < parts.Length; i++)
            {
                var childTransform = current.transform.Find(parts[i]);
                if (childTransform == null)
                    return null;
                current = childTransform.gameObject;
            }
            
            return current;
        }

        private GameObject FindGameObjectByName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
                
            // First try Unity's Find method (can only find root-level objects)
            var rootObj = GameObject.Find(name);
            if (rootObj != null)
                return rootObj;
                
            // If not found, traverse all objects in the scene
            foreach (var rootGo in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var found = FindGameObjectByNameRecursive(rootGo, name);
                if (found != null)
                    return found;
            }
            
            return null;
        }

        private GameObject FindGameObjectByNameRecursive(GameObject obj, string name)
        {
            if (obj.name == name)
                return obj;
                
            foreach (Transform child in obj.transform)
            {
                var found = FindGameObjectByNameRecursive(child.gameObject, name);
                if (found != null)
                    return found;
            }
            
            return null;
        }

        private string CreateGameObject(string? name, GameObjectRef? parentGameObjectRef, Vector3? position, Vector3? rotation, Vector3? scale, Vector3[]? positions, Vector3[]? rotations, bool isLocalSpace, int primitiveType)
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrEmpty(name))
                    return Error.GameObjectNameIsEmpty();

                var parentGo = default(GameObject);
                if (parentGameObjectRef?.IsValid ?? false)
                {
                    parentGo = GameObjectUtils.FindBy(parentGameObjectRef, out var error);
                    if (error != null)
                        return error;
                }

                // Determine if this is batch creation
                bool isBatchCreate = (positions != null && positions.Length > 0) || (rotations != null && rotations.Length > 0);
                
                if (isBatchCreate)
                {
                    // Batch creation logic
                    var createdObjects = new List<GameObject>();
                    var stringBuilder = new StringBuilder();

                    // If using positions + rotation, and rotations is null, then rotations = [rotation * positions.Length]
                    if (rotation != null && rotations == null)
                    {
                        rotations = new Vector3[positions.Length];
                        for (int i = 0; i < positions.Length; i++)
                        {
                            rotations[i] = rotation ?? Vector3.zero;
                        }
                    }

                    // If using position + rotations, and positions is null, then positions = [position * rotations.Length]
                    if (positions == null && rotations != null)
                    {
                        positions = new Vector3[rotations.Length];
                        for (int i = 0; i < rotations.Length; i++)
                        {
                            positions[i] = position ?? Vector3.zero;
                        }
                    }
                    
                    // Check if rotations array length matches positions
                    if (rotations != null && positions != null && rotations.Length != positions.Length)
                    {
                        return $"[Error] The number of rotations ({rotations.Length}) must match the number of positions ({positions.Length}) or be null.";
                    }
                    
                    for (int i = 0; i < positions.Length; i++)
                    {
                        var objectName = positions.Length > 1 ? $"{name}_{i + 1}" : name;
                        var objectPosition = positions[i];
                        var objectRotation = rotations?[i] ?? Vector3.zero;
                        
                        var go = primitiveType switch
                        {
                            0 => GameObject.CreatePrimitive(PrimitiveType.Cube),
                            1 => GameObject.CreatePrimitive(PrimitiveType.Sphere),
                            2 => GameObject.CreatePrimitive(PrimitiveType.Capsule),
                            3 => GameObject.CreatePrimitive(PrimitiveType.Cylinder),
                            4 => GameObject.CreatePrimitive(PrimitiveType.Plane),
                            5 => GameObject.CreatePrimitive(PrimitiveType.Quad),
                            _ => new GameObject(objectName)
                        };
                        
                        go.name = objectName;
                        go.SetTransform(objectPosition, objectRotation, scale, isLocalSpace);
                        
                        if (parentGo != null)
                            go.transform.SetParent(parentGo.transform, false);
                        
                        EditorUtility.SetDirty(go);
                        createdObjects.Add(go);
                        
                        stringBuilder.AppendLine($"Created: {go.name} (ID: {go.GetInstanceID()})");
                    }
                    
                    EditorApplication.RepaintHierarchyWindow();
                    
                    var result = $"[Success] Created {createdObjects.Count} GameObjects in batch.\n{stringBuilder}";
                    
                    // Register undo for batch creation
                    McpUndoHelper.RegisterCreatedObjects(createdObjects, "Create GameObject");
                    
                    return result;
                }
                else
                {
                    // Single object creation
                    var go = primitiveType switch
                    {
                        0 => GameObject.CreatePrimitive(PrimitiveType.Cube),
                        1 => GameObject.CreatePrimitive(PrimitiveType.Sphere),
                        2 => GameObject.CreatePrimitive(PrimitiveType.Capsule),
                        3 => GameObject.CreatePrimitive(PrimitiveType.Cylinder),
                        4 => GameObject.CreatePrimitive(PrimitiveType.Plane),
                        5 => GameObject.CreatePrimitive(PrimitiveType.Quad),
                        _ => new GameObject(name)
                    };
                    go.name = name;
                    go.SetTransform(position, rotation, scale, isLocalSpace);

                    if (parentGo != null)
                        go.transform.SetParent(parentGo.transform, false);

                    EditorUtility.SetDirty(go);
                    EditorApplication.RepaintHierarchyWindow();

                    var result = $"[Success] Created GameObject.\n{go.Print()}";
                    
                    // Register undo for single GameObject creation
                    McpUndoHelper.RegisterCreatedObject(go, "Create GameObject");
                    
                    return result;
                }
            });
        }



        private string DestroyGameObject(GameObjectRef? gameObjectRef)
        {
            return MainThread.Instance.Run(() =>
            {
                if (gameObjectRef == null)
                    return "[Error] GameObject reference is required for destroy operation.";

                var go = GameObjectUtils.FindBy(gameObjectRef, out var error);
                if (error != null)
                    return error;

                try
                {
                    var goName = go.name;
                    
                    // Register undo for GameObject deletion
                    McpUndoHelper.RegisterDestroyedObject(go, "Delete GameObject", true);
                    
                    // Refresh the hierarchy
                    EditorApplication.RepaintHierarchyWindow();
                    
                    return $"[Success] Destroy GameObject: {goName} (using Unity native Undo)";
                }
                catch (Exception ex)
                {
                    return $"[Error] Failed to destroy GameObject: {ex.Message}";
                }
            });
        }

        private string DuplicateGameObjects(GameObjectRefList gameObjectRefs)
        {
            return MainThread.Instance.Run(() =>
            {
                if (gameObjectRefs.Count == 0)
                    return "[Error] No GameObject references provided for duplicate operation.";

                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                var gos = new List<GameObject>(gameObjectRefs.Count);

                for (int i = 0; i < gameObjectRefs.Count; i++)
                {
                    var go = GameObjectUtils.FindBy(gameObjectRefs[i], out var error);
                    if (error != null)
                        return error;

                    gos.Add(go);
                }

                Selection.instanceIDs = gos
                    .Select(go => go.GetInstanceID())
                    .ToArray();

                // Register undo and perform duplication
                McpUndoHelper.StartUndoGroup($"Duplicate {gos.Count} GameObjects");
                Unsupported.DuplicateGameObjectsUsingPasteboard();

                var modifiedScenes = Selection.gameObjects
                    .Select(go => go.scene)
                    .Distinct()
                    .ToList();

                foreach (var scene in modifiedScenes)
                    EditorSceneManager.MarkSceneDirty(scene);

                var location = prefabStage != null ? "Prefab" : "Scene";
                var result = @$"[Success] Duplicated {gos.Count} GameObjects in opened {location}.
Duplicated instanceIDs:
{string.Join(", ", Selection.instanceIDs)}";
                
                return result;
            });
        }

        private string ModifyGameObjects(SerializedMemberList? gameObjectDiffs, GameObjectRefList gameObjectRefs)
        {
            return MainThread.Instance.Run(() =>
            {
                if (gameObjectRefs.Count == 0)
                    return "[Error] No GameObject references provided for modify operation.";

                if (gameObjectDiffs == null || gameObjectDiffs.Count == 0)
                    return "[Error] No modification data provided for modify operation.";

                if (gameObjectDiffs.Count != gameObjectRefs.Count)
                    return $"[Error] The number of gameObjectDiffs and gameObjectRefs should be the same. " +
                        $"gameObjectDiffs: {gameObjectDiffs.Count}, gameObjectRefs: {gameObjectRefs.Count}";

                var stringBuilder = new StringBuilder();
                var successCount = 0;
                var errorCount = 0;
                
                // Create separate undo groups for each GameObject to ensure each operation is recorded separately
                var modifiedObjects = new List<string>(); // Track object names for summary

                for (int i = 0; i < gameObjectRefs.Count; i++)
                {
                    var go = GameObjectUtils.FindBy(gameObjectRefs[i], out var error);
                    if (error != null)
                    {
                        stringBuilder.AppendLine($"[Error] GameObject {i}: {error}");
                        errorCount++;
                        continue;
                    }

                    try
                    {
                        var objToModify = (object)go;
                        var type = TypeUtils.GetType(gameObjectDiffs[i].typeName);
                        if (typeof(UnityEngine.Component).IsAssignableFrom(type))
                        {
                            var component = go.GetComponent(type);
                            if (component == null)
                            {
                                stringBuilder.AppendLine($"[Error] GameObject {i}: Component '{type.FullName}' not found on GameObject '{go.name}'.");
                                errorCount++;
                                continue;
                            }
                            objToModify = component;
                        }

                        // Check if the diff has neither fields nor props
                        if ((gameObjectDiffs[i].fields == null || gameObjectDiffs[i].fields.Count == 0) &&
                            (gameObjectDiffs[i].props == null || gameObjectDiffs[i].props.Count == 0))
                        {
                            stringBuilder.AppendLine($"[Error] GameObject {i}: Diff has neither fields nor props - no modifications to apply for GameObject '{go.name}'.");
                            errorCount++;
                            continue;
                        }

                        // Enhanced array handling - process fields and props separately
                        var modificationResult = TypeConversionUtils.ProcessObjectModifications(objToModify, gameObjectDiffs[i]);
                                                
                        bool currentOpSuccess = false;
                        string modificationDetails = ""; // Store detailed modification information

                        if (modificationResult.Success)
                        {
                            stringBuilder.AppendLine($"[Success] GameObject {i}: '{go.name}' - {modificationResult.Message}");
                            successCount++;
                            modifiedObjects.Add(go.name);
                            currentOpSuccess = true;

                            // Extract modification details from the populate result
                            modificationDetails = ExtractModificationDetails(modificationResult.Message.Trim());
                            if (string.IsNullOrEmpty(modificationDetails))
                            {
                                modificationDetails = "modified";
                            }
                        }
                        else
                        {
                            stringBuilder.AppendLine($"[Error] GameObject {i}: '{go.name}' - {modificationResult.Message}");
                            errorCount++;
                        }

                        // Register undo and mark the object as modified
                        if (currentOpSuccess && objToModify is UnityEngine.Object unityObj)
                        {
                            string operationName;
                            string details = null;
                            
                            if (string.IsNullOrEmpty(modificationDetails) || modificationDetails == "modified")
                            {
                                operationName = "Modify GameObject";
                            }
                            else
                            {
                                // Extract property name from modification details for more specific operation name
                                var propertyMatch = System.Text.RegularExpressions.Regex.Match(modificationDetails, @"Property '(\w+)'");
                                if (propertyMatch.Success)
                                {
                                    var propertyName = propertyMatch.Groups[1].Value;
                                    operationName = $"Modify {propertyName}";
                                }
                                else
                                {
                                    operationName = "Modify GameObject";
                                }
                                details = McpUndoHelper.SimplifyValueForUndo(modificationDetails);
                            }
                            
                            McpUndoHelper.RegisterModifiedObject(unityObj, operationName, details);
                            EditorUtility.SetDirty(unityObj);
                        }
                    }
                    catch (Exception ex)
                    {
                        stringBuilder.AppendLine($"[Error] GameObject {i}: Exception occurred - {ex.Message}");
                        errorCount++;
                    }
                }

                // Generate summary
                var summary = new StringBuilder();
                if (successCount > 0 && errorCount == 0)
                {
                    summary.AppendLine($"[Success] All {successCount} GameObject(s) modified successfully.");
                }
                else if (successCount > 0 && errorCount > 0)
                {
                    summary.AppendLine($"[Partial Success] {successCount} GameObject(s) modified successfully, {errorCount} failed.");
                }
                else if (errorCount > 0)
                {
                    summary.AppendLine($"[Error] All {errorCount} GameObject(s) failed to modify.");
                }

                summary.AppendLine();
                summary.Append(stringBuilder);

                var result = summary.ToString();
                
                return result;
            });
        }


        /// <summary>
        /// Extract modification details from the populate result string for better undo group naming
        /// </summary>
        private string ExtractModificationDetails(string populateResultString)
        {
            if (string.IsNullOrEmpty(populateResultString))
                return "";

            var details = new List<string>();
            var lines = populateResultString.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Look for success patterns with property/field modifications
                if (trimmedLine.StartsWith("[Success]"))
                {
                    // Extract property/field modification info
                    // Pattern: "[Success] Property 'position' modified to '(0.10, 0.00, 5.10)'"
                    // Pattern: "[Success] Field 'someField' changed to 'newValue'"
                    // Pattern: "[Success] GameObject property 'name' changed to 'NewName'"
                    
                    if (trimmedLine.Contains("Property '") && trimmedLine.Contains("' modified to '"))
                    {
                        var propStart = trimmedLine.IndexOf("Property '") + "Property '".Length;
                        var propEnd = trimmedLine.IndexOf("'", propStart);
                        if (propEnd > propStart)
                        {
                            var propertyName = trimmedLine.Substring(propStart, propEnd - propStart);
                            
                            var valueStart = trimmedLine.IndexOf("' modified to '") + "' modified to '".Length;
                            var valueEnd = trimmedLine.LastIndexOf("'");
                            if (valueEnd > valueStart)
                            {
                                var newValue = trimmedLine.Substring(valueStart, valueEnd - valueStart);
                                // Simplify the value display for readability
                                var simplifiedValue = SimplifyValue(newValue);
                                details.Add($"{propertyName} → {simplifiedValue}");
                            }
                            else
                            {
                                details.Add($"{propertyName} modified");
                            }
                        }
                    }
                    else if (trimmedLine.Contains("Field '") && trimmedLine.Contains("' changed to '"))
                    {
                        var fieldStart = trimmedLine.IndexOf("Field '") + "Field '".Length;
                        var fieldEnd = trimmedLine.IndexOf("'", fieldStart);
                        if (fieldEnd > fieldStart)
                        {
                            var fieldName = trimmedLine.Substring(fieldStart, fieldEnd - fieldStart);
                            
                            var valueStart = trimmedLine.IndexOf("' changed to '") + "' changed to '".Length;
                            var valueEnd = trimmedLine.LastIndexOf("'");
                            if (valueEnd > valueStart)
                            {
                                var newValue = trimmedLine.Substring(valueStart, valueEnd - valueStart);
                                var simplifiedValue = SimplifyValue(newValue);
                                details.Add($"{fieldName} → {simplifiedValue}");
                            }
                            else
                            {
                                details.Add($"{fieldName} modified");
                            }
                        }
                    }
                    else if (trimmedLine.Contains("GameObject property '") && trimmedLine.Contains("' changed to '"))
                    {
                        var propStart = trimmedLine.IndexOf("GameObject property '") + "GameObject property '".Length;
                        var propEnd = trimmedLine.IndexOf("'", propStart);
                        if (propEnd > propStart)
                        {
                            var propertyName = trimmedLine.Substring(propStart, propEnd - propStart);
                            
                            var valueStart = trimmedLine.IndexOf("' changed to '") + "' changed to '".Length;
                            var valueEnd = trimmedLine.LastIndexOf("'");
                            if (valueEnd > valueStart)
                            {
                                var newValue = trimmedLine.Substring(valueStart, valueEnd - valueStart);
                                var simplifiedValue = SimplifyValue(newValue);
                                details.Add($"{propertyName} → {simplifiedValue}");
                            }
                            else
                            {
                                details.Add($"{propertyName} modified");
                            }
                        }
                    }
                }
            }

            if (details.Count == 0)
                return "modified";

            // Combine details, but limit length for readability
            var combined = string.Join(", ", details);
            if (combined.Length > 60) // Limit to avoid too long names
            {
                // Show first few modifications and indicate there are more
                var firstDetails = details.Take(2).ToList();
                var firstCombined = string.Join(", ", firstDetails);
                if (details.Count > 2)
                {
                    return $"{firstCombined}... (+{details.Count - 2} more)";
                }
                else if (firstCombined.Length > 60)
                {
                    return $"{firstCombined.Substring(0, 57)}...";
                }
                return firstCombined;
            }

            return combined;
        }

        /// <summary>
        /// Simplify complex values for better readability in undo group names
        /// </summary>
        private string SimplifyValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            // Simplify Vector3 format: "(1.50, 2.00, 3.00)" -> "(1.5, 2, 3)"
            if (value.StartsWith("(") && value.EndsWith(")") && value.Contains(","))
            {
                var cleaned = value.Trim('(', ')');
                var parts = cleaned.Split(',');
                if (parts.Length == 3)
                {
                    var simplifiedParts = parts.Select(p => 
                    {
                        if (float.TryParse(p.Trim(), out var floatVal))
                        {
                            // Remove unnecessary trailing zeros
                            if (floatVal == Math.Round(floatVal))
                                return Math.Round(floatVal).ToString();
                            else
                                return floatVal.ToString("0.##");
                        }
                        return p.Trim();
                    });
                    return $"({string.Join(", ", simplifiedParts)})";
                }
            }

            // Limit string length
            if (value.Length > 20)
            {
                return $"{value.Substring(0, 17)}...";
            }

            return value;
        }

        private string SetParentGameObjects(GameObjectRefList gameObjectRefs, GameObjectRef? parentGameObjectRef, bool worldPositionStays)
        {
            return MainThread.Instance.Run(() =>
            {
                if (gameObjectRefs.Count == 0)
                    return "[Error] No GameObject references provided for setParent operation.";

                if (parentGameObjectRef == null)
                    return "[Error] Parent GameObject reference is required for setParent operation.";

                var stringBuilder = new StringBuilder();
                int changedCount = 0;
                
                // Get parent GameObject once
                var parentGo = GameObjectUtils.FindBy(parentGameObjectRef, out var parentError);
                if (parentError != null)
                    return $"[Error] Parent GameObject: {parentError}";

                // Collect transforms for batch parent change
                var targetTransforms = new List<Transform>();
                
                for (var i = 0; i < gameObjectRefs.Count; i++)
                {
                    var targetGo = GameObjectUtils.FindBy(gameObjectRefs[i], out var error);
                    if (error != null)
                    {
                        stringBuilder.AppendLine(error);
                        continue;
                    }

                    targetTransforms.Add(targetGo.transform);
                    changedCount++;
                    stringBuilder.AppendLine(@$"[Success] Set parent of {gameObjectRefs[i]} to {parentGameObjectRef}.");
                }

                // Register undo for parent changes
                if (targetTransforms.Count > 0)
                {
                    McpUndoHelper.RegisterParentChanges(targetTransforms, parentGo.transform);
                }

                if (changedCount > 0)
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

                var result = stringBuilder.ToString();
                
                return result;
            });
        }

    private string SetActiveGameObjects(GameObjectRefList gameObjectRefs, bool active)
    {
        return MainThread.Instance.Run(() =>
        {
            if (gameObjectRefs.Count == 0)
                return "[Error] No GameObject references provided for setActive operation.";

            var stringBuilder = new StringBuilder();
            int changedCount = 0;

            for (var i = 0; i < gameObjectRefs.Count; i++)
            {
                var targetGo = GameObjectUtils.FindBy(gameObjectRefs[i], out var error);
                if (error != null)
                {
                    stringBuilder.AppendLine(error);
                    continue;
                }

                targetGo.SetActive(active);
                changedCount++;

                stringBuilder.AppendLine($"[Success] Set active state of '{targetGo.name}' to {active}.");
            }

            if (changedCount > 0)
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            return stringBuilder.ToString();
        });
    }

    private string SetComponentActiveGameObjects(GameObjectRefList gameObjectRefs, string? componentTypeName, bool? componentActive)
    {
        return MainThread.Instance.Run(() =>
        {
            if (gameObjectRefs.Count == 0)
                return "[Error] No GameObject references provided for setComponentActive operation.";

            if (string.IsNullOrEmpty(componentTypeName))
                return "[Error] Component type name is required for setComponentActive operation.";

            bool activeState = componentActive ?? true;

            var stringBuilder = new StringBuilder();
            int changedCount = 0;

            for (var i = 0; i < gameObjectRefs.Count; i++)
            {
                var targetGo = GameObjectUtils.FindBy(gameObjectRefs[i], out var error);
                if (error != null)
                {
                    stringBuilder.AppendLine(error);
                    continue;
                }

                var componentType = TypeUtils.GetType(componentTypeName);
                if (componentType == null)
                {
                    stringBuilder.AppendLine($"[Error] GameObject {i}: Component type '{componentTypeName}' not found.");
                    continue;
                }

                var component = targetGo.GetComponent(componentType);
                if (component == null)
                {
                    stringBuilder.AppendLine($"[Error] GameObject {i}: Component '{componentType.FullName}' not found on GameObject '{targetGo.name}'.");
                    continue;
                }

                try
                {
                    // First try to get the enabled property
                    var enabledProperty = componentType.GetProperty("enabled");
                    if (enabledProperty != null && enabledProperty.CanWrite)
                    {
                        enabledProperty.SetValue(component, activeState);
                        changedCount++;
                        stringBuilder.AppendLine($"[Success] Set component '{componentType.FullName}' active state of '{targetGo.name}' to {activeState}.");
                    }
                    else
                    {
                        // If no enabled property, try to use the component as a Behaviour
                        if (component is Behaviour behaviour)
                        {
                            behaviour.enabled = activeState;
                            changedCount++;
                            stringBuilder.AppendLine($"[Success] Set component '{componentType.FullName}' active state of '{targetGo.name}' to {activeState}.");
                        }
                        else
                        {
                            stringBuilder.AppendLine($"[Error] GameObject {i}: Component '{componentType.FullName}' does not support enabling/disabling.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    stringBuilder.AppendLine($"[Error] GameObject {i}: Exception occurred - {ex.Message}");
                }
                }

                if (changedCount > 0)
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

                return stringBuilder.ToString();
            });
        }
    }
} 