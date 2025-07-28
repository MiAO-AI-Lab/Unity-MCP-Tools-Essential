#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System.ComponentModel;
using System.Text;
using com.MiAO.Unity.MCP.Common;
using com.IvanMurzak.ReflectorNet.Model.Unity;
using com.MiAO.Unity.MCP.Utils;
using com.IvanMurzak.ReflectorNet.Utils;
using com.IvanMurzak.ReflectorNet;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    public partial class Tool_GameObject
    {
        int  DETAILED_SERIALIZED_GAME_OBJECT_MAX_DEPTH = 6;

        [McpPluginTool
        (
            "GameObject_Find",
            Title = "Find GameObject in opened Prefab or in a Scene"
        )]
        [Description(@"Finds specific GameObject by provided information.
First it looks for the opened Prefab, if any Prefab is opened it looks only there ignoring a scene.
If no opened Prefab it looks into current active scene. 
Returns GameObject information and its children.
Also, it returns Components preview just for the target GameObject.")]
        public string Find
        (
            GameObjectRef gameObjectRef,
            [Description("Determines the depth of the hierarchy to include. 0 - means only the target GameObject. 1 - means to include one layer below.")]
            int includeChildrenDepth = 0,
            [Description("If true, show brief GameObject data including serialized information. Default is false.")]
            bool showBriefSerializedGameObject = false,
            [Description("If true, show detailed GameObject data including serialized information. Default is false.")]
            bool showDetailedSerializedGameObject = false,
            [Description("If true, show properties in serialized GameObject. Default is false.")]
            bool showPropertiesInSerializedGameObject = false,
            [Description("If true, show GameObject bounds information. Default is false.")]
            bool showBounds = false,
            [Description("If true, show layer information. Default is false.")]
            bool showLayer = false,
            [Description("If true, show components preview with detailed serialization. Default is true.")]
            bool showComponentsPreview = true,
            [Description("If true, show related enum information for the GameObject. Default is false.")]
            bool showRelatedEnums = false
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var go = GameObjectUtils.FindBy(gameObjectRef, out var error);
                if (error != null)
                    return error;

                // Check for missing components
                var components = go.GetComponents<UnityEngine.Component>();

                // Initialize a string builder
                StringBuilder result = new StringBuilder();

                result.AppendLine(@$"[Success] Found GameObject. Basic GameObject info: instanceID={go.GetInstanceID()}, name={go.name}, active={go.activeInHierarchy}");
                
                // Show components preview
                showComponentsPreviewMethod(go, components, result, showComponentsPreview);

                if (showBriefSerializedGameObject)
                    showBriefSerializedGameObjectMethod(go, result);


                if (showDetailedSerializedGameObject)
                    showDetailedSerializedGameObjectMethod(go, result, showPropertiesInSerializedGameObject);


                if (showBounds)
                    showBoundsMethod(go, result);


                if (showLayer)
                    showLayerMethod(go, result);


                if (showRelatedEnums)
                    showRelatedEnumsMethod(go, result, components);


                result.AppendLine(@$"

# Hierarchy:
{go.ToMetadata(includeChildrenDepth).Print()}");
                
                return result.ToString();
            });
        }

        private void showBriefSerializedGameObjectMethod(GameObject go, StringBuilder result)
        {
            try
            {
                var serializedGo = Reflector.Instance.Serialize(
                    go,
                    type: go.GetType(),
                    name: go.name,
                    recursive: true,
                    logger: McpPlugin.Instance.Logger
                );
                        
                result.AppendLine(@$"

# GameObject Details (Brief):
```json
{serializedGo}
```");
            }
            catch (System.Exception ex)
            {
                result.AppendLine(@$"

# GameObject Details (Brief):
⚠️ Object {go.name} serialization failed due to: {ex.Message}
You can try to use detailed serialization mode.");
            }
        }

        private void showDetailedSerializedGameObjectMethod(GameObject go, StringBuilder result, bool showPropertiesInSerializedGameObject)
        {
            try
            {
                var serializedGo = ObjectSerializationUtils.SerializeToJson(go, "detailed", DETAILED_SERIALIZED_GAME_OBJECT_MAX_DEPTH, showPropertiesInSerializedGameObject);
                result.AppendLine(@$"

# GameObject Details (Detailed):
```json
{serializedGo}
```");
            }
            catch (System.Exception ex)
            {
                result.AppendLine(@$"

# GameObject Details:
⚠️ Object {go.name} serialization failed due to: {ex.Message}");
            }
        }

        private void showBoundsMethod(GameObject go, StringBuilder result)
        {
            result.AppendLine(@$"

# Bounds:
```json
{JsonUtils.Serialize(go.CalculateBounds())}
```");
        }

        private void showComponentsPreviewMethod(GameObject go, UnityEngine.Component[] components, StringBuilder result, bool showComponentsPreview)
        {
            try
            {
                // Initialize a list for components preview
                var componentsPreview = new List<object>();
                var missingComponents = new List<string>();

                // Check each component and detect missing scripts
                for (int i = 0; i < components.Length; i++)
                {
                    var component = components[i];
                    if (component == null)
                    {
                        // This is a missing component - use GameObjectUtils public method
                        var missingInfo = GameObjectUtils.GetMissingComponentInfo(go, i);
                        missingComponents.Add($"[{i}] Missing Component: {missingInfo}");
                        componentsPreview.Add(new { Name = $"[{i}]", Status = "Missing", Info = missingInfo });
                    }
                    else
                    {
                        // Normal component - serialize it based on showComponentsPreview setting
                        if (showComponentsPreview)
                        {
                            try
                            {
                                var serialized = Reflector.Instance.Serialize(
                                    component,
                                    name: $"[{i}]",
                                    recursive: false,
                                    logger: McpPlugin.Instance.Logger
                                );
                                componentsPreview.Add(serialized);
                            }
                            catch (System.Exception ex)
                            {
                                // If serialization fails, add basic info
                                componentsPreview.Add(new { Name = $"[{i}]", Type = component.GetType().Name, Status = "SerializationError", Error = ex.Message });
                            }
                        }
                        else
                        {
                            // Basic info only
                            componentsPreview.Add(new { Name = $"[{i}]", Type = component.GetType().Name, Status = "OK" });
                        }
                    }
                }

                if (missingComponents.Count > 0)
                {
                    result.AppendLine($"\n\n# ⚠️ Missing Components Detected ({missingComponents.Count}):\n" + string.Join("\n", missingComponents));
                }

                result.AppendLine(@$"

    # Components Preview:
    ```json
    {JsonUtils.Serialize(componentsPreview)}
    ```");
            }
            catch (System.Exception ex)
            {
                result.AppendLine(@$"# Components Preview:
⚠️ Components preview failed due to: {ex.Message}");
            }
        }

        private void showLayerMethod(GameObject go, StringBuilder result)
        {
            result.AppendLine(@$"

# Layer Info:
```json
{JsonUtils.Serialize(new { LayerIndex = go.layer, LayerName = LayerMask.LayerToName(go.layer) })}
```");
        }

        private void showRelatedEnumsMethod(GameObject go, StringBuilder result, UnityEngine.Component[] components)
        {
            try
            {
                var enumInfo = new List<object>();
                
                // Add enum fields from all components
                for (int i = 0; i < components.Length; i++)
                {
                    var component = components[i];
                    if (component == null) continue; // Skip missing components

                    var componentType = component.GetType();
                    var componentEnums = new List<object>();

                    // Get all fields
                    var fields = componentType.GetFields(System.Reflection.BindingFlags.Public | 
                                                        System.Reflection.BindingFlags.NonPublic | 
                                                        System.Reflection.BindingFlags.Instance);
                    
                    foreach (var field in fields)
                    {
                        if (field.FieldType.IsEnum)
                        {
                            try
                            {
                                var currentValue = field.GetValue(component);
                                var enumValues = System.Enum.GetValues(field.FieldType);
                                var enumNames = System.Enum.GetNames(field.FieldType);
                                
                                var enumDetails = new List<object>();
                                for (int j = 0; j < enumValues.Length; j++)
                                {
                                    enumDetails.Add(new { 
                                        Name = enumNames[j], 
                                        Value = (int)enumValues.GetValue(j),
                                        IsCurrent = currentValue.Equals(enumValues.GetValue(j))
                                    });
                                }

                                componentEnums.Add(new {
                                    FieldName = field.Name,
                                    FieldType = field.FieldType.Name,
                                    CurrentValue = currentValue?.ToString(),
                                    CurrentIntValue = (int)currentValue,
                                    AllValues = enumDetails
                                });
                            }
                            catch (System.Exception ex)
                            {
                                componentEnums.Add(new {
                                    FieldName = field.Name,
                                    FieldType = field.FieldType.Name,
                                    Error = $"Failed to read enum: {ex.Message}"
                                });
                            }
                        }
                    }

                    // Get all properties
                    var properties = componentType.GetProperties(System.Reflection.BindingFlags.Public | 
                                                                System.Reflection.BindingFlags.NonPublic | 
                                                                System.Reflection.BindingFlags.Instance);
                    
                    foreach (var property in properties)
                    {
                        if (property.PropertyType.IsEnum && property.CanRead)
                        {
                            try
                            {
                                var currentValue = property.GetValue(component);
                                var enumValues = System.Enum.GetValues(property.PropertyType);
                                var enumNames = System.Enum.GetNames(property.PropertyType);
                                
                                var enumDetails = new List<object>();
                                for (int j = 0; j < enumValues.Length; j++)
                                {
                                    enumDetails.Add(new { 
                                        Name = enumNames[j], 
                                        Value = (int)enumValues.GetValue(j),
                                        IsCurrent = currentValue.Equals(enumValues.GetValue(j))
                                    });
                                }

                                componentEnums.Add(new {
                                    PropertyName = property.Name,
                                    PropertyType = property.PropertyType.Name,
                                    CurrentValue = currentValue?.ToString(),
                                    CurrentIntValue = (int)currentValue,
                                    AllValues = enumDetails
                                });
                            }
                            catch (System.Exception ex)
                            {
                                componentEnums.Add(new {
                                    PropertyName = property.Name,
                                    PropertyType = property.PropertyType.Name,
                                    Error = $"Failed to read enum: {ex.Message}"
                                });
                            }
                        }
                    }

                    // Add component enum info if any enums were found
                    if (componentEnums.Count > 0)
                    {
                        enumInfo.Add(new {
                            ComponentIndex = i,
                            ComponentType = componentType.Name,
                            ComponentFullName = componentType.FullName,
                            EnumFields = componentEnums
                        });
                    }
                }

                result.AppendLine(@$"

# Related Enums:
```json
{JsonUtils.Serialize(enumInfo)}
```");

            }
            catch (System.Exception ex)
            {
                result.AppendLine(@$"# Related Enums:
⚠️ Related enums failed due to: {ex.Message}");
            }
        }
    }
} 