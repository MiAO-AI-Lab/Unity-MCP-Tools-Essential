using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using Newtonsoft.Json;
using Object = UnityEngine.Object;

namespace com.MiAO.Unity.MCP.Utils
{
    /// <summary>
    /// Configuration options for object serialization
    /// </summary>
    public class SerializationConfig
    {
        public int MaxDepth { get; set; } = 6;
        public bool ShowProperties { get; set; } = false;
        public int MaxCollectionSize { get; set; } = 100;
        public bool PrettyPrint { get; set; } = true;
    }

    /// <summary>
    /// Instance-based object serializer that maintains serialization state
    /// </summary>
    public class ObjectSerializer
    {
        private readonly HashSet<object> _visitedObjects = new HashSet<object>();
        private int _currentDepth = 0;
        private SerializationConfig _config;

        public ObjectSerializer(SerializationConfig config = null)
        {
            _config = config ?? new SerializationConfig();
        }

        /// <summary>
        /// Serializes any Unity object to a comprehensive dictionary representation
        /// </summary>
        public Dictionary<string, object> SerializeObject(object obj)
        {
            Reset();
            
            try
            {
                return SerializeObjectInternal(obj);
            }
            finally
            {
                Reset();
            }
        }

        /// <summary>
        /// One-line method to serialize any object to JSON
        /// </summary>
        public string SerializeToJson(object obj, string mode = "detailed")
        {
            if (mode == "normal")
            {
                // Unity's built-in serializer (does not support GameObject, Component)
                return JsonUtility.ToJson(obj);
            }
            else
            {
                var serialized = SerializeObject(obj);
                return ToJsonString(serialized);
            }
        }

        private void Reset()
        {
            _visitedObjects.Clear();
            _currentDepth = 0;
        }

        private Dictionary<string, object> SerializeObjectInternal(object obj)
        {
            var result = new Dictionary<string, object>();
            
            if (obj == null)
            {
                result["value"] = null;
                result["type"] = "null";
                return result;
            }

            var type = obj.GetType();
            result["type"] = type.FullName;

            // Check for circular references and max depth
            if (_visitedObjects.Contains(obj) || _currentDepth >= _config.MaxDepth)
            {
                result["value"] = $"[Circular Reference or Max Depth Reached: {type.Name}]";
                return result;
            }

            _visitedObjects.Add(obj);
            _currentDepth++;

            try
            {
                // Handle primitive types and strings
                if (IsPrimitiveType(type))
                {
                    result["value"] = obj;
                    return result;
                }

                // Handle Unity Object types specially
                if (obj is Object unityObj)
                {
                    SerializeUnityObject(unityObj, result);
                    return result;
                }

                // Handle collections
                if (obj is IEnumerable enumerable && !(obj is string))
                {
                    SerializeCollection(enumerable, result);
                    return result;
                }

                // Handle regular objects with reflection
                SerializeObjectWithReflection(obj, result);
                return result;
            }
            finally
            {
                _visitedObjects.Remove(obj);
                _currentDepth--;
            }
        }

        private bool IsPrimitiveType(Type type)
        {
            return type.IsPrimitive || 
                   type == typeof(string) || 
                   type == typeof(decimal) || 
                   type == typeof(DateTime) || 
                   type == typeof(TimeSpan) ||
                   type.IsEnum;
        }

        private void SerializeUnityObject(Object unityObj, Dictionary<string, object> result)
        {
            result["instanceID"] = unityObj.GetInstanceID();
            result["name"] = unityObj.name;
            result["isDestroyed"] = unityObj == null;

            // Handle different Unity object types
            switch (unityObj)
            {
                case GameObject go:
                    SerializeGameObject(go, result);
                    break;
                case Component comp:
                    SerializeComponent(comp, result);
                    break;
                case ScriptableObject so:
                    SerializeScriptableObject(so, result);
                    break;
                case Material mat:
                    SerializeMaterial(mat, result);
                    break;
                case Texture tex:
                    SerializeTexture(tex, result);
                    break;
                case Mesh mesh:
                    SerializeMesh(mesh, result);
                    break;
                default:
                    SerializeGenericUnityObject(unityObj, result);
                    break;
            }
        }

        private void SerializeGameObject(GameObject go, Dictionary<string, object> result)
        {
            result["gameObjectData"] = new Dictionary<string, object>
            {
                ["active"] = go.activeSelf,
                ["activeInHierarchy"] = go.activeInHierarchy,
                ["tag"] = go.tag,
                ["layer"] = go.layer,
                ["layerName"] = LayerMask.LayerToName(go.layer),
                ["scene"] = go.scene.name,
                ["isStatic"] = go.isStatic,
                ["transform"] = SerializeObjectInternal(go.transform),
                ["componentCount"] = go.GetComponents<Component>().Length,
                ["components"] = go.GetComponents<Component>()
                    .Where(c => c != null)
                    .Select(c => new Dictionary<string, object>
                    {
                        ["type"] = c.GetType().Name,
                        ["fullType"] = c.GetType().FullName,
                        ["enabled"] = c is Behaviour behaviour ? behaviour.enabled : true,
                        ["data"] = SerializeObjectInternal(c)
                    }).ToArray()
            };
        }

        private void SerializeComponent(Component comp, Dictionary<string, object> result)
        {
            var componentData = new Dictionary<string, object>
            {
                ["gameObjectName"] = comp.gameObject.name,
                ["gameObjectInstanceID"] = comp.gameObject.GetInstanceID()
            };

            if (comp is Behaviour behaviour)
                componentData["enabled"] = behaviour.enabled;

            if (comp is Transform transform)
                SerializeTransform(transform, componentData);
            else if (comp is RectTransform rectTransform)
                SerializeRectTransform(rectTransform, componentData);
            else if (comp is Image image)
                SerializeImage(image, componentData);
            else if (comp is Text text)
                SerializeText(text, componentData);
            else if (comp is Button button)
                SerializeButton(button, componentData);

            result["componentData"] = componentData;
            
            // Serialize all fields and properties
            SerializeObjectWithReflection(comp, result);
        }

        private void SerializeTransform(Transform transform, Dictionary<string, object> componentData)
        {
            componentData["position"] = VectorToDict<Vector3>(transform.position);
            componentData["localPosition"] = VectorToDict<Vector3>(transform.localPosition);
            componentData["rotation"] = QuaternionToDict(transform.rotation);
            componentData["localRotation"] = QuaternionToDict(transform.localRotation);
            componentData["localScale"] = VectorToDict<Vector3>(transform.localScale);
            componentData["childCount"] = transform.childCount;
            componentData["siblingIndex"] = transform.GetSiblingIndex();
            
            if (transform.parent != null)
            {
                componentData["parent"] = new Dictionary<string, object>
                {
                    ["name"] = transform.parent.name,
                    ["instanceID"] = transform.parent.GetInstanceID()
                };
            }
        }

        private void SerializeRectTransform(RectTransform rectTransform, Dictionary<string, object> componentData)
        {
            SerializeTransform(rectTransform, componentData);
            componentData["anchoredPosition"] = VectorToDict<Vector2>(rectTransform.anchoredPosition);
            componentData["sizeDelta"] = VectorToDict<Vector2>(rectTransform.sizeDelta);
            componentData["anchorMin"] = VectorToDict<Vector2>(rectTransform.anchorMin);
            componentData["anchorMax"] = VectorToDict<Vector2>(rectTransform.anchorMax);
            componentData["pivot"] = VectorToDict<Vector2>(rectTransform.pivot);
            componentData["rect"] = RectToDict(rectTransform.rect);
        }

        private void SerializeImage(Image image, Dictionary<string, object> componentData)
        {
            componentData["color"] = ColorToDict(image.color);
            componentData["raycastTarget"] = image.raycastTarget;
            componentData["type"] = image.type.ToString();
            componentData["fillMethod"] = image.fillMethod.ToString();
            componentData["fillAmount"] = image.fillAmount;
            componentData["preserveAspect"] = image.preserveAspect;
            
            if (image.sprite != null)
            {
                componentData["sprite"] = new Dictionary<string, object>
                {
                    ["name"] = image.sprite.name,
                    ["instanceID"] = image.sprite.GetInstanceID(),
                    ["textureSize"] = $"{image.sprite.texture.width}x{image.sprite.texture.height}",
                    ["pixelsPerUnit"] = image.sprite.pixelsPerUnit
                };
            }
            
            if (image.material != null)
            {
                componentData["material"] = new Dictionary<string, object>
                {
                    ["name"] = image.material.name,
                    ["instanceID"] = image.material.GetInstanceID(),
                    ["shader"] = image.material.shader?.name
                };
            }
        }

        private void SerializeText(Text text, Dictionary<string, object> componentData)
        {
            componentData["text"] = text.text;
            componentData["font"] = text.font?.name;
            componentData["fontSize"] = text.fontSize;
            componentData["fontStyle"] = text.fontStyle.ToString();
            componentData["color"] = ColorToDict(text.color);
            componentData["alignment"] = text.alignment.ToString();
            componentData["lineSpacing"] = text.lineSpacing;
            componentData["richText"] = text.supportRichText;
        }

        private void SerializeButton(Button button, Dictionary<string, object> componentData)
        {
            componentData["interactable"] = button.interactable;
            componentData["transition"] = button.transition.ToString();
            
            if (button.targetGraphic != null)
            {
                componentData["targetGraphic"] = new Dictionary<string, object>
                {
                    ["name"] = button.targetGraphic.name,
                    ["type"] = button.targetGraphic.GetType().Name
                };
            }
            
            if (button.onClick != null)
            {
                componentData["onClickListenerCount"] = button.onClick.GetPersistentEventCount();
            }
        }

        private void SerializeScriptableObject(ScriptableObject so, Dictionary<string, object> result)
        {
            // Serialize all fields using reflection
            SerializeObjectWithReflection(so, result);
        }

        private void SerializeMaterial(Material mat, Dictionary<string, object> result)
        {
            result["materialData"] = new Dictionary<string, object>
            {
                ["shader"] = mat.shader?.name,
                ["renderQueue"] = mat.renderQueue,
                ["shaderKeywords"] = mat.shaderKeywords?.ToArray(),
                ["passCount"] = mat.passCount
            };
            
            // Try to get common properties safely
            var properties = new Dictionary<string, object>();
            try
            {
                if (mat.HasProperty("_Color"))
                    properties["_Color"] = ColorToDict(mat.GetColor("_Color"));
                if (mat.HasProperty("_MainTex"))
                    properties["_MainTex"] = mat.GetTexture("_MainTex")?.name;
            }
            catch (Exception ex)
            {
                properties["serializationError"] = ex.Message;
            }
            
            result["materialData"] = properties;
        }

        private void SerializeTexture(Texture tex, Dictionary<string, object> result)
        {
            result["textureData"] = new Dictionary<string, object>
            {
                ["width"] = tex.width,
                ["height"] = tex.height,
                ["dimension"] = tex.dimension.ToString(),
                ["filterMode"] = tex.filterMode.ToString(),
                ["wrapMode"] = tex.wrapMode.ToString(),
                ["anisoLevel"] = tex.anisoLevel
            };
        }

        private void SerializeMesh(Mesh mesh, Dictionary<string, object> result)
        {
            result["meshData"] = new Dictionary<string, object>
            {
                ["vertexCount"] = mesh.vertexCount,
                ["triangleCount"] = mesh.triangles?.Length / 3 ?? 0,
                ["subMeshCount"] = mesh.subMeshCount,
                ["bounds"] = BoundsToDict(mesh.bounds),
                ["isReadable"] = mesh.isReadable
            };
        }

        private void SerializeGenericUnityObject(Object obj, Dictionary<string, object> result)
        {
            // For other Unity objects, use reflection
            SerializeObjectWithReflection(obj, result);
        }

        private void SerializeCollection(IEnumerable enumerable, Dictionary<string, object> result)
        {
            var items = new List<object>();
            var index = 0;
            
            foreach (var item in enumerable)
            {
                if (index >= _config.MaxCollectionSize)
                {
                    items.Add($"[... and more items (truncated at {_config.MaxCollectionSize})]");
                    break;
                }
                
                items.Add(SerializeObjectInternal(item));
                index++;
            }
            
            result["value"] = items.ToArray();
            result["count"] = index;
        }

        private void SerializeObjectWithReflection(object obj, Dictionary<string, object> result)
        {
            var fields = new Dictionary<string, object>();
            var properties = new Dictionary<string, object>();
            
            var type = obj.GetType();
            
            // Serialize fields
            var fieldInfos = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fieldInfos)
            {
                if (field.IsStatic) continue;
                
                try
                {
                    var value = field.GetValue(obj);
                    fields[field.Name] = SerializeObjectInternal(value);
                }
                catch (Exception ex)
                {
                    fields[field.Name] = $"[Error accessing field: {ex.Message}]";
                }
            }
            
            // Serialize properties
            if (_config.ShowProperties)
            {
                var propertyInfos = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var property in propertyInfos)
                {
                    if (!property.CanRead || property.GetIndexParameters().Length > 0) continue;
                    
                    try
                    {
                        var value = property.GetValue(obj);
                        properties[property.Name] = SerializeObjectInternal(value);
                    }
                    catch (Exception ex)
                    {
                        properties[property.Name] = $"[Error accessing property: {ex.Message}]";
                    }
                }
            }
            
            result["fields"] = fields;
            if (_config.ShowProperties)
            {
                result["properties"] = properties;
            }
        }

        // Helper methods for Unity-specific types
        private static Dictionary<string, object> VectorToDict<T>(T vector) where T : struct
        {
            var result = new Dictionary<string, object>();
            var type = typeof(T);
            
            var xProperty = type.GetProperty("x");
            var yProperty = type.GetProperty("y");
            var zProperty = type.GetProperty("z");
            var wProperty = type.GetProperty("w");
            
            if (xProperty != null) result["x"] = xProperty.GetValue(vector);
            if (yProperty != null) result["y"] = yProperty.GetValue(vector);
            if (zProperty != null) result["z"] = zProperty.GetValue(vector);
            if (wProperty != null) result["w"] = wProperty.GetValue(vector);
            
            return result;
        }
        
        private Dictionary<string, object> QuaternionToDict(Quaternion quaternion)
        {
            return new Dictionary<string, object>
            {
                ["x"] = quaternion.x,
                ["y"] = quaternion.y,
                ["z"] = quaternion.z,
                ["w"] = quaternion.w,
                ["eulerAngles"] = VectorToDict<Vector3>(quaternion.eulerAngles)
            };
        }

        private static Dictionary<string, object> ColorToDict(Color color)
        {
            return new Dictionary<string, object>
            {
                ["r"] = color.r,
                ["g"] = color.g,
                ["b"] = color.b,
                ["a"] = color.a,
                ["hexColor"] = ColorUtility.ToHtmlStringRGBA(color)
            };
        }

        private static Dictionary<string, object> RectToDict(Rect rect)
        {
            return new Dictionary<string, object>
            {
                ["x"] = rect.x,
                ["y"] = rect.y,
                ["width"] = rect.width,
                ["height"] = rect.height
            };
        }

        private Dictionary<string, object> BoundsToDict(Bounds bounds)
        {
            return new Dictionary<string, object>
            {
                ["center"] = VectorToDict<Vector3>(bounds.center),
                ["size"] = VectorToDict<Vector3>(bounds.size),
                ["min"] = VectorToDict<Vector3>(bounds.min),
                ["max"] = VectorToDict<Vector3>(bounds.max)
            };
        }

        /// <summary>
        /// Converts the serialized object dictionary to JSON string
        /// </summary>
        public string ToJsonString(Dictionary<string, object> serializedData)
        {
            try
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(serializedData, 
                    _config.PrettyPrint ? Newtonsoft.Json.Formatting.Indented : Newtonsoft.Json.Formatting.None,
                    new Newtonsoft.Json.JsonSerializerSettings
                    {
                        ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore,
                        NullValueHandling = Newtonsoft.Json.NullValueHandling.Include
                    });
            }
            catch (Exception ex)
            {
                return $"{{\"error\": \"JSON serialization failed: {ex.Message}\"}}";
            }
        }
    }

    /// <summary>
    /// Static utility class for backward compatibility
    /// </summary>
    public static class ObjectSerializationUtils
    {
        /// <summary>
        /// One-line method to serialize any object to JSON (backward compatibility)
        /// </summary>
        public static string SerializeToJson(object obj, string mode = "normal", int maxDepth = 6, bool showProperties = false, bool prettyPrint = true)
        {
            switch (mode)
            {
                case "normal":
                    return JsonUtility.ToJson(obj);
                case "detailed":
                    return new ObjectSerializer(new SerializationConfig { MaxDepth = maxDepth, ShowProperties = showProperties }).SerializeToJson(obj);
                default:
                    return JsonUtility.ToJson(obj);
            }
        }
    }
}