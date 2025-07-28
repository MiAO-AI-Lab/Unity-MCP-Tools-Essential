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
    public static class ObjectSerializationUtils
    {
        private static readonly HashSet<object> _visitedObjects = new HashSet<object>();
        private static int _maxDepth = 5;
        private static int _currentDepth = 0;

        /// <summary>
        /// Serializes any Unity object to a comprehensive dictionary representation
        /// </summary>
        /// <param name="obj">The object to serialize</param>
        /// <param name="maxDepth">Maximum recursion depth to prevent infinite loops</param>
        /// <returns>Dictionary containing serialized object data</returns>
        public static Dictionary<string, object> SerializeObject(object obj, int maxDepth = 5, bool showProperties = false)
        {
            _maxDepth = maxDepth;
            _currentDepth = 0;
            _visitedObjects.Clear();
            
            try
            {
                return SerializeObjectInternal(obj, showProperties);
            }
            finally
            {
                _visitedObjects.Clear();
                _currentDepth = 0;
            }
        }

        private static Dictionary<string, object> SerializeObjectInternal(object obj, bool showProperties = false)
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
            // result["assemblyQualifiedName"] = type.AssemblyQualifiedName;

            // Check for circular references and max depth
            if (_visitedObjects.Contains(obj) || _currentDepth >= _maxDepth)
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
                    SerializeUnityObject(unityObj, result, showProperties);
                    return result;
                }

                // Handle collections
                if (obj is IEnumerable enumerable && !(obj is string))
                {
                    SerializeCollection(enumerable, result);
                    return result;
                }

                // Handle regular objects with reflection
                SerializeObjectWithReflection(obj, result, showProperties);
                return result;
            }
            finally
            {
                _visitedObjects.Remove(obj);
                _currentDepth--;
            }
        }

        private static bool IsPrimitiveType(Type type)
        {
            return type.IsPrimitive || 
                   type == typeof(string) || 
                   type == typeof(decimal) || 
                   type == typeof(DateTime) || 
                   type == typeof(TimeSpan) ||
                   type.IsEnum;
        }

        private static void SerializeUnityObject(Object unityObj, Dictionary<string, object> result, bool showProperties = false)
        {
            result["instanceID"] = unityObj.GetInstanceID();
            result["name"] = unityObj.name;
            result["isDestroyed"] = unityObj == null;

            // Handle different Unity object types
            switch (unityObj)
            {
                case GameObject go:
                    SerializeGameObject(go, result, showProperties);
                    break;
                case Component comp:
                    SerializeComponent(comp, result, showProperties);
                    break;
                case ScriptableObject so:
                    SerializeScriptableObject(so, result, showProperties);
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
                    SerializeGenericUnityObject(unityObj, result, showProperties);
                    break;
            }
        }

        private static void SerializeGameObject(GameObject go, Dictionary<string, object> result, bool showProperties = false)
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
                ["transform"] = SerializeObjectInternal(go.transform, showProperties),
                ["componentCount"] = go.GetComponents<Component>().Length,
                ["components"] = go.GetComponents<Component>()
                    .Where(c => c != null)
                    .Select(c => new Dictionary<string, object>
                    {
                        ["type"] = c.GetType().Name,
                        ["fullType"] = c.GetType().FullName,
                        ["enabled"] = c is Behaviour behaviour ? behaviour.enabled : true,
                        ["data"] = SerializeObjectInternal(c, showProperties)
                    }).ToArray()
            };
        }

        private static void SerializeComponent(Component comp, Dictionary<string, object> result, bool showProperties = false)
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
            SerializeObjectWithReflection(comp, result, showProperties);
        }

        private static void SerializeTransform(Transform transform, Dictionary<string, object> componentData)
        {
            componentData["position"] = VectorToDict(transform.position);
            componentData["localPosition"] = VectorToDict(transform.localPosition);
            componentData["rotation"] = QuaternionToDict(transform.rotation);
            componentData["localRotation"] = QuaternionToDict(transform.localRotation);
            componentData["localScale"] = VectorToDict(transform.localScale);
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

        private static void SerializeRectTransform(RectTransform rectTransform, Dictionary<string, object> componentData)
        {
            SerializeTransform(rectTransform, componentData);
            componentData["anchoredPosition"] = VectorToDict(rectTransform.anchoredPosition);
            componentData["sizeDelta"] = VectorToDict(rectTransform.sizeDelta);
            componentData["anchorMin"] = VectorToDict(rectTransform.anchorMin);
            componentData["anchorMax"] = VectorToDict(rectTransform.anchorMax);
            componentData["pivot"] = VectorToDict(rectTransform.pivot);
            componentData["rect"] = RectToDict(rectTransform.rect);
        }

        private static void SerializeImage(Image image, Dictionary<string, object> componentData)
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

        private static void SerializeText(Text text, Dictionary<string, object> componentData)
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

        private static void SerializeButton(Button button, Dictionary<string, object> componentData)
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

        private static void SerializeScriptableObject(ScriptableObject so, Dictionary<string, object> result, bool showProperties = false)
        {
            // Serialize all fields using reflection
            SerializeObjectWithReflection(so, result, showProperties);
        }

        private static void SerializeMaterial(Material mat, Dictionary<string, object> result)
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

        private static void SerializeTexture(Texture tex, Dictionary<string, object> result)
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

        private static void SerializeMesh(Mesh mesh, Dictionary<string, object> result)
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

        private static void SerializeGenericUnityObject(Object obj, Dictionary<string, object> result, bool showProperties = false)
        {
            // For other Unity objects, use reflection
            SerializeObjectWithReflection(obj, result, showProperties);
        }

        private static void SerializeCollection(IEnumerable enumerable, Dictionary<string, object> result)
        {
            var items = new List<object>();
            var index = 0;
            
            foreach (var item in enumerable)
            {
                if (index >= 100) // Limit collection size
                {
                    items.Add($"[... and more items (truncated at 100)]");
                    break;
                }
                
                items.Add(SerializeObjectInternal(item));
                index++;
            }
            
            result["value"] = items.ToArray();
            result["count"] = index;
        }

        private static void SerializeObjectWithReflection(object obj, Dictionary<string, object> result, bool showProperties = false)
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
            
            result["fields"] = fields;
            if (showProperties)
            {
                result["properties"] = properties;
            }
        }

        // Helper methods for Unity-specific types
        private static Dictionary<string, object> VectorToDict(Vector3 vector)
        {
            return new Dictionary<string, object>
            {
                ["x"] = vector.x,
                ["y"] = vector.y,
                ["z"] = vector.z
            };
        }

        private static Dictionary<string, object> VectorToDict(Vector2 vector)
        {
            return new Dictionary<string, object>
            {
                ["x"] = vector.x,
                ["y"] = vector.y
            };
        }

        private static Dictionary<string, object> QuaternionToDict(Quaternion quaternion)
        {
            return new Dictionary<string, object>
            {
                ["x"] = quaternion.x,
                ["y"] = quaternion.y,
                ["z"] = quaternion.z,
                ["w"] = quaternion.w,
                ["eulerAngles"] = VectorToDict(quaternion.eulerAngles)
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

        private static Dictionary<string, object> BoundsToDict(Bounds bounds)
        {
            return new Dictionary<string, object>
            {
                ["center"] = VectorToDict(bounds.center),
                ["size"] = VectorToDict(bounds.size),
                ["min"] = VectorToDict(bounds.min),
                ["max"] = VectorToDict(bounds.max)
            };
        }

        /// <summary>
        /// Converts the serialized object dictionary to JSON string
        /// </summary>
        public static string ToJsonString(Dictionary<string, object> serializedData, bool prettyPrint = true)
        {
            try
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(serializedData, 
                    prettyPrint ? Newtonsoft.Json.Formatting.Indented : Newtonsoft.Json.Formatting.None,
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

        /// <summary>
        /// One-line method to serialize any object to JSON
        /// </summary>
        public static string SerializeToJson(object obj, string mode = "normal", int maxDepth = 6, bool showProperties = false, bool prettyPrint = true)
        {
            if (mode == "normal")
            {
                // does not support GameObject, Component
                return JsonUtility.ToJson(obj);
            }
            else
            {
                var serialized = SerializeObject(obj, maxDepth, showProperties);
                return ToJsonString(serialized, prettyPrint);
            }
        }
    }
}