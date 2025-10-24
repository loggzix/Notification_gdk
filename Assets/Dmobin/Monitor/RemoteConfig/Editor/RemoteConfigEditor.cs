using UnityEngine;
using UnityEditor;
using DSDK.Remote;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using System;
using System.Reflection;
using DSDK.Logger;

namespace DSDK.Editor
{
    public class RemoteConfigEditor : EditorWindow
    {
        private Vector2 _scrollPosition;
        private Dictionary<string, bool> _foldouts = new();
        private Dictionary<string, object> _cachedDefaultValues = new();
        private Dictionary<string, MonoScript> _cachedScripts = new();
        private Dictionary<string, string> _editingDefaultJson = new();
        private string _searchText = "";
        private bool _stylesInitialized;
        private bool _useMonitorForDefaults;
        private const string EDITOR_PREFS_KEY = "DmobinSDK_RemoteConfigEditor_";

        // Styles
        private GUIStyle _buttonStyle;
        private GUIStyle _textAreaStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _searchStyle;
        private GUIStyle _configItemStyle;
        private GUIStyle _helpBoxHighlightStyle;
        private GUIContent _copyKeyIcon;
        private GUIContent _copyValueIcon;
        private GUIContent _editIcon;
        private GUIContent _applyIcon;
        private GUIContent _refreshIcon;

        [MenuItem("Dmobin/Remote Config/Editor")]
        public static void ShowWindow()
        {
            GetWindow<RemoteConfigEditor>("Remote Config").Show();
        }

        private void SaveEditorState()
        {
            // Save search
            EditorPrefs.SetString($"{EDITOR_PREFS_KEY}Search", _searchText ?? "");

            // Save foldouts
            var foldoutStates = JsonConvert.SerializeObject(_foldouts);
            EditorPrefs.SetString($"{EDITOR_PREFS_KEY}Foldouts", foldoutStates);

            // Save editing json
            var editingDefaultJson = JsonConvert.SerializeObject(_editingDefaultJson);
            EditorPrefs.SetString($"{EDITOR_PREFS_KEY}EditingDefaultJson", editingDefaultJson);
        }

        private void LoadEditorState()
        {
            // Load search
            _searchText = EditorPrefs.GetString($"{EDITOR_PREFS_KEY}Search", "");

            // Load foldouts
            var foldoutStates = EditorPrefs.GetString($"{EDITOR_PREFS_KEY}Foldouts", "{}");
            try
            {
                _foldouts = JsonConvert.DeserializeObject<Dictionary<string, bool>>(foldoutStates);
            }
            catch
            {
                _foldouts = new Dictionary<string, bool>();
            }

            // Load editing json
            var editingDefaultJson = EditorPrefs.GetString($"{EDITOR_PREFS_KEY}EditingDefaultJson", "{}");
            try
            {
                _editingDefaultJson = JsonConvert.DeserializeObject<Dictionary<string, string>>(editingDefaultJson);
            }
            catch
            {
                _editingDefaultJson = new Dictionary<string, string>();
            }
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Remote Config - Default");
            LoadEditorState();
            CacheAllConfigs();
        }

        private void OnDisable()
        {
            SaveEditorState();
        }

        private void OnFocus()
        {
            // Tự động refresh khi cửa sổ được focus lại
            ClearCache();
            CacheAllConfigs();
            Repaint();
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            // Button style
            _buttonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fixedWidth = 100,
                margin = new RectOffset(2, 2, 2, 2),
                padding = new RectOffset(5, 5, 3, 3),
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter
            };
            _buttonStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.9f, 0.9f, 0.9f) : new Color(0.2f, 0.2f, 0.2f);

            // Text area style
            _textAreaStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                richText = true,
                stretchHeight = true,
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(5, 5, 5, 5)
            };

            // Header style
            _headerStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                margin = new RectOffset(10, 10, 10, 10)
            };
            _headerStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.9f, 0.9f, 0.9f) : new Color(0.2f, 0.2f, 0.2f);

            // Search style
            _searchStyle = new GUIStyle(EditorStyles.toolbarSearchField)
            {
                margin = new RectOffset(10, 10, 5, 5),
                fixedHeight = 20
            };

            // Config item style
            _configItemStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(10, 10, 5, 5)
            };

            // Help box highlight style
            _helpBoxHighlightStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(15, 15, 15, 15),
                margin = new RectOffset(10, 10, 8, 8),
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                richText = true
            };
            _helpBoxHighlightStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f) : new Color(0.1f, 0.1f, 0.1f);

            // Icons
            _copyKeyIcon = new GUIContent("Copy Key", EditorGUIUtility.IconContent("Clipboard").image);
            _copyValueIcon = new GUIContent("Copy Value", EditorGUIUtility.IconContent("Clipboard").image);
            _editIcon = EditorGUIUtility.IconContent("d_editicon.sml");
            _editIcon.text = "Edit";
            _applyIcon = EditorGUIUtility.IconContent("d_SaveAs");
            _applyIcon.text = "Apply";
            _refreshIcon = EditorGUIUtility.IconContent("d_Refresh");
            _refreshIcon.text = "Refresh";

            _stylesInitialized = true;
        }

        private bool IsSimpleConfig(Type type)
        {
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                var fieldType = field.FieldType;
                if (fieldType.IsArray)
                {
                    // Allow arrays of primitive types, string, enum, and Unity serializable classes
                    var elementType = fieldType.GetElementType();
                    if (elementType.IsPrimitive || elementType == typeof(string) || elementType.IsEnum)
                        continue;
                    // Allow Unity serializable classes (marked with [Serializable])
                    if (elementType.IsClass && elementType.GetCustomAttributes(typeof(SerializableAttribute), false).Length > 0)
                        continue;
                    return false;
                }
                if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    // Allow List of primitive types, string, enum, and Unity serializable classes
                    var elementType = fieldType.GetGenericArguments()[0];
                    if (elementType.IsPrimitive || elementType == typeof(string) || elementType.IsEnum)
                        continue;
                    // Allow Unity serializable classes (marked with [Serializable])
                    if (elementType.IsClass && elementType.GetCustomAttributes(typeof(SerializableAttribute), false).Length > 0)
                        continue;
                    return false;
                }
                if (!fieldType.IsPrimitive && fieldType != typeof(string) && !fieldType.IsEnum)
                {
                    // Allow Unity serializable classes (marked with [Serializable])
                    if (fieldType.IsClass && fieldType.GetCustomAttributes(typeof(SerializableAttribute), false).Length > 0)
                        continue;
                    return false;
                }
            }
            return true;
        }

        private string GetTypeString(object value)
        {
            if (value == null) return "object";
            if (value is bool) return "bool";
            if (value is int) return "int";
            if (value is long) return "long";
            if (value is float) return "float";
            if (value is double) return "double";
            if (value is string) return "string";

            // Handle JArray - lấy type từ giá trị đầu tiên
            if (value is Newtonsoft.Json.Linq.JArray jArray && jArray.Any())
            {
                var firstItem = jArray.First;
                string elementType = "object";
                switch (firstItem.Type)
                {
                    case Newtonsoft.Json.Linq.JTokenType.Integer:
                        elementType = "long";
                        break;
                    case Newtonsoft.Json.Linq.JTokenType.Float:
                        elementType = "float";
                        break;
                    case Newtonsoft.Json.Linq.JTokenType.String:
                        elementType = "string";
                        break;
                    case Newtonsoft.Json.Linq.JTokenType.Boolean:
                        elementType = "bool";
                        break;
                }
                return $"{elementType}[]";
            }

            // Handle normal arrays
            if (value is Array arr)
            {
                var elementType = arr.GetType().GetElementType();
                return $"{elementType.Name}[]";
            }

            // Handle List
            if (value.GetType().IsGenericType && value.GetType().GetGenericTypeDefinition() == typeof(List<>))
            {
                var elementType = value.GetType().GetGenericArguments()[0];
                return $"List<{elementType.Name}>";
            }

            return value.GetType().Name;
        }
        private void ClearCache()
        {
            _cachedDefaultValues.Clear();
            _cachedScripts.Clear();
            _editingDefaultJson.Clear();
        }

        private void CacheAllConfigs()
        {
            try
            {
                // Đảm bảo cache đã được xóa
                ClearCache();

                var configTypes = RemoteConfigBase.GetAllConfigTypes();
                if (configTypes == null)
                {
                    DLogger.LogWarning("No config types found");
                    return;
                }

                // Cache giá trị GetDefaultFromMonitor một lần
                _useMonitorForDefaults = CheckUseMonitorForDefaults();

                var guids = AssetDatabase.FindAssets("t:MonoScript");
                var scriptPaths = guids.Select(AssetDatabase.GUIDToAssetPath)
                                     .Where(path => path.StartsWith("Assets/"))
                                     .ToList();

                foreach (var type in configTypes)
                {
                    try
                    {
                        if (type == null) continue;

                        // Lấy instance
                        object instance = null;
                        var instanceProperty = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                        if (instanceProperty != null)
                        {
                            try
                            {
                                instance = instanceProperty.GetValue(null);
                            }
                            catch (Exception e)
                            {
                                DLogger.LogWarning($"Failed to get Instance of {type.Name}: {e.Message}");
                            }
                        }

                        // Tạo instance mới nếu cần
                        if (instance == null)
                        {
                            instance = Activator.CreateInstance(type) as RemoteConfigBase;
                        }

                        if (instance == null) continue;

                        // Lấy giá trị
                        object defaultValue = null;
                        string configKey = null;

                        var remoteConfig = instance as RemoteConfigBase;
                        if (remoteConfig != null)
                        {
                            try
                            {
                                // Nếu useMonitorForDefaults = true, lấy default từ monitor
                                if (_useMonitorForDefaults)
                                {
                                    defaultValue = GetDefaultValueFromMonitor(type, remoteConfig);
                                }

                                // Nếu không lấy được từ monitor hoặc useMonitorForDefaults = false, 
                                // lấy từ GetDefaultValue() như bình thường
                                if (defaultValue == null)
                                {
                                    defaultValue = remoteConfig.GetDefaultValue();
                                }

                                configKey = remoteConfig.Key;
                            }
                            catch (Exception e)
                            {
                                DLogger.LogWarning($"Failed to get default value for {type.Name}: {e.Message}");
                                continue;
                            }
                        }

                        if (string.IsNullOrEmpty(configKey))
                        {
                            configKey = type.Name;
                        }

                        // Cache values
                        if (defaultValue != null)
                        {
                            _cachedDefaultValues[configKey] = defaultValue;
                            // Use custom JSON settings to handle Unity objects
                            var jsonSettings = new JsonSerializerSettings
                            {
                                Formatting = Formatting.Indented,
                                NullValueHandling = NullValueHandling.Include,
                                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                                Converters = new List<JsonConverter> { new UnityObjectJsonConverter() }
                            };
                            _editingDefaultJson[configKey] = JsonConvert.SerializeObject(defaultValue, jsonSettings);
                        }

                        // Cache script
                        var scriptPath = scriptPaths.FirstOrDefault(p =>
                            System.IO.Path.GetFileNameWithoutExtension(p) == type.Name);
                        if (!string.IsNullOrEmpty(scriptPath))
                        {
                            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                            if (script != null)
                            {
                                _cachedScripts[configKey] = script;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        DLogger.LogError($"Error caching config {type?.Name}: {e.Message}");
                    }
                }

            }
            catch (Exception e)
            {
                DLogger.LogError($"Error in CacheAllConfigs: {e.Message}");
            }
        }

        /// <summary>
        /// Kiểm tra xem có nên sử dụng Monitor để lấy default values không
        /// </summary>
        private bool CheckUseMonitorForDefaults()
        {
            try
            {
                // Tìm prefab RemoteConfigMonitor
                var prefab = Resources.Load<GameObject>("RemoteConfigMonitor");
                if (prefab == null)
                {
                    DLogger.LogDebug("RemoteConfigMonitor prefab not found in Resources");
                    return false;
                }

                var monitor = prefab.GetComponent<RemoteConfigMonitor>();
                if (monitor == null)
                {
                    DLogger.LogDebug("RemoteConfigMonitor component not found on prefab");
                    return false;
                }

                // Sử dụng reflection để lấy giá trị của _getDefaultFromMonitor
                var field = monitor.GetType().GetField("_getDefaultFromMonitor", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    var value = (bool)field.GetValue(monitor);
                    return value;
                }
                return false;
            }
            catch (Exception e)
            {
                DLogger.LogError($"Error checking RemoteConfigMonitor: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lấy default value từ Monitor cho config type cụ thể
        /// </summary>
        private object GetDefaultValueFromMonitor(Type configType, RemoteConfigBase remoteConfig)
        {
            try
            {
                // Lấy tên config để map với field trong Monitor
                string configName = configType.Name;

                // Kiểm tra RemoteConfigMonitor trước
                var monitorValue = GetValueFromSpecificMonitor("RemoteConfigMonitor", configType, configName);
                if (monitorValue != null)
                {
                    return monitorValue;
                }

                // Nếu không tìm thấy trong RemoteConfigMonitor, thử tìm trong MediationMonitor
                monitorValue = GetValueFromMediationMonitor(configType, configName);
                if (monitorValue != null)
                {
                    return monitorValue;
                }

                DLogger.LogDebug($"No monitor value found for {configName}");
                return null;
            }
            catch (Exception e)
            {
                DLogger.LogError($"Error getting default value from monitor for {configType.Name}: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Lấy value từ một monitor cụ thể
        /// </summary>
        private object GetValueFromSpecificMonitor(string monitorName, Type configType, string configName)
        {
            try
            {
                DLogger.LogDebug($"[DEBUG] GetValueFromSpecificMonitor: monitorName={monitorName}, configType={configType.Name}, configName={configName}");

                // Tìm prefab monitor
                var prefab = Resources.Load<GameObject>(monitorName);
                if (prefab == null)
                {
                    DLogger.LogDebug($"[DEBUG] Prefab {monitorName} not found in Resources");
                    return null;
                }

                // Get component by type instead of by name
                Component monitor = null;
                if (monitorName == "RemoteConfigMonitor")
                {
                    monitor = prefab.GetComponent<RemoteConfigMonitor>();
                }
                else
                {
                    monitor = prefab.GetComponent(monitorName);
                }

                if (monitor == null)
                {
                    DLogger.LogDebug($"[DEBUG] Component {monitorName} not found on prefab");
                    return null;
                }

                // Use SerializedObject to get values from prefab (more reliable in editor)
                var serializedObject = new SerializedObject(monitor);

                // Tìm field tương ứng trong Monitor
                // Ví dụ: DebugConfigSDK -> _debugConfigSDK
                string fieldName = "_" + char.ToLower(configName[0]) + configName.Substring(1);
                DLogger.LogDebug($"[DEBUG] Looking for serialized property: {fieldName}");

                var property = serializedObject.FindProperty(fieldName);
                if (property != null)
                {
                    DLogger.LogDebug($"[DEBUG] Found serialized property {fieldName}");

                    // Convert SerializedProperty to object
                    var monitorValue = GetSerializedPropertyValue(property);
                    if (monitorValue != null)
                    {
                        DLogger.LogDebug($"Found monitor value for {configName} in {monitorName}: {JsonConvert.SerializeObject(monitorValue)}");
                        return monitorValue;
                    }
                    else
                    {
                        DLogger.LogDebug($"[DEBUG] Serialized property {fieldName} value is null");
                    }
                }
                else
                {
                    DLogger.LogDebug($"[DEBUG] Serialized property {fieldName} not found");
                }

                // Fallback to reflection method
                var field = monitor.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    var monitorValue = field.GetValue(monitor);
                    DLogger.LogDebug($"[DEBUG] Found field {fieldName} via reflection, value is null: {monitorValue == null}");
                    if (monitorValue != null)
                    {
                        DLogger.LogDebug($"Found monitor value for {configName} in {monitorName}: {JsonConvert.SerializeObject(monitorValue)}");
                        return monitorValue;
                    }
                }

                // Nếu không tìm thấy field với pattern trên, thử pattern khác
                // Thử tìm field public có tên tương tự
                var allFields = monitor.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                DLogger.LogDebug($"[DEBUG] All fields in monitor: {string.Join(", ", allFields.Select(f => f.Name))}");

                foreach (var candidateField in allFields)
                {
                    // Check nếu field type match với config type
                    if (candidateField.FieldType == configType ||
                        candidateField.FieldType.Name.Equals(configName, StringComparison.OrdinalIgnoreCase))
                    {
                        var monitorValue = candidateField.GetValue(monitor);
                        DLogger.LogDebug($"[DEBUG] Found matching field {candidateField.Name}, value is null: {monitorValue == null}");
                        if (monitorValue != null)
                        {
                            DLogger.LogDebug($"Found monitor value for {configName} in {monitorName} field {candidateField.Name}: {JsonConvert.SerializeObject(monitorValue)}");
                            return monitorValue;
                        }
                    }
                }

                DLogger.LogDebug($"[DEBUG] No matching field found for {configName}");
                return null;
            }
            catch (Exception e)
            {
                DLogger.LogDebug($"Error getting value from {monitorName}: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Lấy value từ MediationMonitor (trong package)
        /// </summary>
        private object GetValueFromMediationMonitor(Type configType, string configName)
        {
            try
            {
                // Tìm MediationMonitor trong Resources
                var prefab = Resources.Load<GameObject>("MediationMonitor");
                if (prefab == null)
                {
                    return null;
                }

                // Tìm component MediationMonitor
                var mediationMonitorType = System.Type.GetType("MediationMonitor");
                if (mediationMonitorType == null)
                {
                    return null;
                }

                var monitor = prefab.GetComponent(mediationMonitorType);
                if (monitor == null)
                {
                    return null;
                }

                // Map từ config name sang field name trong MediationMonitor
                string fieldName = GetMediationMonitorFieldName(configName);
                if (string.IsNullOrEmpty(fieldName))
                {
                    return null;
                }

                var field = mediationMonitorType.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    var monitorValue = field.GetValue(monitor);
                    if (monitorValue != null)
                    {
                        DLogger.LogDebug($"Found monitor value for {configName} in MediationMonitor.{fieldName}: {JsonConvert.SerializeObject(monitorValue)}");
                        return monitorValue;
                    }
                }

                return null;
            }
            catch (Exception e)
            {
                DLogger.LogDebug($"Error getting value from MediationMonitor: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Map từ config name sang field name trong MediationMonitor
        /// </summary>
        private string GetMediationMonitorFieldName(string configName)
        {
            // Map các config names sang field names trong MediationMonitor
            var mappings = new Dictionary<string, string>
            {
                { "BannerConfigSDK", "BannerConfig" },
                { "InterstitialConfigSDK", "InterstitialConfig" },
                { "RewardedVideoConfigSDK", "RewardedVideoConfig" },
                { "NativeFullScreenConfigSDK", "NativeFullScreenConfig" },
                { "AppOpenConfigSDK", "AppOpenConfig" },
                { "LoadAdsConfigSDK", "LoadAdsConfig" },
                { "LoadingAdConfigSDK", "LoadingAdConfig" }
            };

            return mappings.TryGetValue(configName, out var fieldName) ? fieldName : null;
        }

        /// <summary>
        /// Convert SerializedProperty to object value
        /// </summary>
        private object GetSerializedPropertyValue(SerializedProperty property)
        {
            try
            {
                switch (property.propertyType)
                {
                    case SerializedPropertyType.Integer:
                        return property.intValue;
                    case SerializedPropertyType.Boolean:
                        return property.boolValue;
                    case SerializedPropertyType.Float:
                        return property.floatValue;
                    case SerializedPropertyType.String:
                        return property.stringValue;
                    case SerializedPropertyType.Enum:
                        return property.enumValueIndex;
                    case SerializedPropertyType.ObjectReference:
                        return property.objectReferenceValue;
                    case SerializedPropertyType.Generic:
                        // Handle complex objects by creating instance and copying values
                        return GetGenericSerializedPropertyValue(property);
                    default:
                        DLogger.LogDebug($"[DEBUG] Unsupported property type: {property.propertyType}");
                        return null;
                }
            }
            catch (Exception e)
            {
                DLogger.LogError($"Error getting serialized property value: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Handle generic/complex SerializedProperty types
        /// </summary>
        private object GetGenericSerializedPropertyValue(SerializedProperty property)
        {
            try
            {
                // For arrays
                if (property.isArray && property.propertyType == SerializedPropertyType.Generic)
                {
                    var arraySize = property.arraySize;
                    DLogger.LogDebug($"[DEBUG] Processing array property {property.name} with size {arraySize}");

                    if (arraySize == 0)
                    {
                        DLogger.LogDebug($"[DEBUG] Array {property.name} is empty");
                        return new object[0];
                    }

                    var list = new List<object>();
                    for (int i = 0; i < arraySize; i++)
                    {
                        var element = property.GetArrayElementAtIndex(i);
                        var elementValue = GetComplexObjectFromSerializedProperty(element);
                        if (elementValue != null)
                        {
                            list.Add(elementValue);
                        }
                    }

                    DLogger.LogDebug($"[DEBUG] Converted array {property.name} to list with {list.Count} elements");
                    return list.ToArray();
                }

                // For complex objects
                return GetComplexObjectFromSerializedProperty(property);
            }
            catch (Exception e)
            {
                DLogger.LogError($"Error getting generic serialized property value: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Create object from complex SerializedProperty
        /// </summary>
        private object GetComplexObjectFromSerializedProperty(SerializedProperty property)
        {
            try
            {
                // Create a dictionary to hold the property values
                var dict = new Dictionary<string, object>();

                var iterator = property.Copy();
                var endProperty = property.GetEndProperty();

                if (iterator.NextVisible(true))
                {
                    do
                    {
                        if (SerializedProperty.EqualContents(iterator, endProperty))
                            break;

                        var value = GetSerializedPropertyValue(iterator);
                        if (value != null)
                        {
                            dict[iterator.name] = value;
                        }
                    }
                    while (iterator.NextVisible(false));
                }

                return dict;
            }
            catch (Exception e)
            {
                DLogger.LogError($"Error creating complex object from serialized property: {e.Message}");
                return null;
            }
        }

        private void OnGUI()
        {
            InitializeStyles();

            try
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    DrawHeader();
                    DrawSearch();
                }
            }
            catch (Exception e)
            {
                DLogger.LogError($"Error in OnGUI: {e.Message}");
            }
        }
        private void DrawHeader()
        {
            EditorGUILayout.Space(10);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Remote Config Editor - Default Values", _headerStyle);

                // Hiển thị trạng thái GetDefaultFromMonitor - sử dụng cached value
                var statusColor = _useMonitorForDefaults ? Color.green : Color.gray;
                var statusText = _useMonitorForDefaults ? "Monitor ON" : "Monitor OFF";

                var oldColor = GUI.color;
                GUI.color = statusColor;
                GUILayout.Label($"[{statusText}]", EditorStyles.boldLabel, GUILayout.Width(100));
                GUI.color = oldColor;

                if (GUILayout.Button(_refreshIcon, _buttonStyle))
                {
                    ClearCache();
                    CacheAllConfigs();
                    Repaint();
                }
            }
            EditorGUILayout.Space(5);
        }

        private void DrawSearch()
        {
            EditorGUILayout.Space(5);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(10);
                _searchText = EditorGUILayout.TextField("Search", _searchText ?? "", _searchStyle);
            }
            EditorGUILayout.Space(5);

            // Hiển thị thông báo về Monitor status
            if (_useMonitorForDefaults)
            {
                EditorGUILayout.Space(5);

                // Tạo highlight box với màu nền xanh nhạt
                var rect = EditorGUILayout.GetControlRect(false, 45);
                rect.x += 10;
                rect.width -= 20;

                var oldColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.7f, 1f, 0.7f, 0.3f); // Màu xanh nhạt

                GUI.Box(rect, "", _helpBoxHighlightStyle);

                // Vẽ text với icon
                var textRect = new Rect(rect.x + 10, rect.y + 8, rect.width - 20, rect.height - 16);
                var iconRect = new Rect(textRect.x, textRect.y + 2, 16, 16);
                var messageRect = new Rect(textRect.x + 20, textRect.y, textRect.width - 20, textRect.height);

                GUI.DrawTexture(iconRect, EditorGUIUtility.IconContent("d_console.infoicon").image);

                var oldTextColor = GUI.color;
                GUI.color = new Color(0.2f, 0.6f, 0.2f); // Màu xanh đậm cho text

                EditorGUI.LabelField(messageRect,
                    "<b>✓ Đang sử dụng giá trị từ RemoteConfigMonitor prefab (Prefab gốc)</b>\n<size=10>GetDefaultFromMonitor = true</size>",
                    new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true });

                GUI.color = oldTextColor;
                GUI.backgroundColor = oldColor;
            }
            else
            {
                EditorGUILayout.Space(5);

                // Tạo highlight box với màu nền xám nhạt
                var rect = EditorGUILayout.GetControlRect(false, 45);
                rect.x += 10;
                rect.width -= 20;

                var oldColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 0.3f); // Màu xám nhạt

                GUI.Box(rect, "", _helpBoxHighlightStyle);

                // Vẽ text với icon
                var textRect = new Rect(rect.x + 10, rect.y + 8, rect.width - 20, rect.height - 16);
                var iconRect = new Rect(textRect.x, textRect.y + 2, 16, 16);
                var messageRect = new Rect(textRect.x + 20, textRect.y, textRect.width - 20, textRect.height);

                GUI.DrawTexture(iconRect, EditorGUIUtility.IconContent("d_console.warnicon.inactive.sml").image);

                var oldTextColor = GUI.color;
                GUI.color = new Color(0.7f, 0.7f, 0.7f); // Lighter gray color for text

                EditorGUI.LabelField(messageRect,
                    "<b>○ Đang sử dụng giá trị từ constructor của các class config</b>\n<size=10>GetDefaultFromMonitor = false</size>",
                    new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true });

                GUI.color = oldTextColor;
                GUI.backgroundColor = oldColor;
            }

            EditorGUILayout.Space(10);
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            var searchLower = (_searchText ?? "").ToLower();
            var configs = _cachedDefaultValues
                .Where(x => string.IsNullOrEmpty(searchLower) ||
                           x.Key.ToLower().Contains(searchLower) ||
                           JsonConvert.SerializeObject(x.Value).ToLower().Contains(searchLower));

            foreach (var kvp in configs)
            {
                DrawConfigItem(kvp.Key, kvp.Value, _editingDefaultJson);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawConfigItem(string key, object value, Dictionary<string, string> editingJson)
        {
            if (!_foldouts.ContainsKey(key))
                _foldouts[key] = false;

            using (new EditorGUILayout.VerticalScope(_configItemStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var oldFoldout = _foldouts[key];
                    _foldouts[key] = EditorGUILayout.Foldout(_foldouts[key], key, true);
                    if (oldFoldout != _foldouts[key])
                    {
                        SaveEditorState();
                    }

                    if (GUILayout.Button(_copyKeyIcon, _buttonStyle))
                    {
                        EditorGUIUtility.systemCopyBuffer = key;
                        DLogger.LogDebug($"Copied key: {key}");
                    }

                    if (GUILayout.Button(_copyValueIcon, _buttonStyle))
                    {
                        EditorGUIUtility.systemCopyBuffer = editingJson[key];
                        DLogger.LogDebug($"Copied value for {key}");
                    }

                    if (_cachedScripts.TryGetValue(key, out var script))
                    {
                        var type = script.GetClass();
                        bool isSimple = type != null && IsSimpleConfig(type);

                        if (isSimple)
                        {
                            if (GUILayout.Button(_applyIcon, _buttonStyle))
                            {
                                UpdateSimpleConfig(script, editingJson[key]);
                            }
                        }

                        if (GUILayout.Button(_editIcon, _buttonStyle))
                        {
                            if (!isSimple)
                            {
                                if (EditorUtility.DisplayDialog("Manual Edit Required",
                                    "This config has complex structure. Please edit the script manually.",
                                    "Open Script", "Cancel"))
                                {
                                    AssetDatabase.OpenAsset(script);
                                }
                            }
                            else
                            {
                                AssetDatabase.OpenAsset(script);
                            }
                        }
                    }
                }

                if (_foldouts[key])
                {
                    EditorGUI.indentLevel++;
                    if (!editingJson.ContainsKey(key))
                    {
                        // Use custom JSON settings to handle Unity objects
                        var jsonSettings = new JsonSerializerSettings
                        {
                            Formatting = Formatting.Indented,
                            NullValueHandling = NullValueHandling.Include,
                            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                            Converters = new List<JsonConverter> { new UnityObjectJsonConverter() }
                        };
                        editingJson[key] = JsonConvert.SerializeObject(value, jsonSettings);
                    }

                    var newJson = EditorGUILayout.TextArea(editingJson[key], _textAreaStyle);
                    if (newJson != editingJson[key])
                    {
                        try
                        {
                            // Use custom JSON settings for validation
                            var jsonSettings = new JsonSerializerSettings
                            {
                                NullValueHandling = NullValueHandling.Include,
                                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                                Converters = new List<JsonConverter> { new UnityObjectJsonConverter() }
                            };
                            JsonConvert.DeserializeObject(newJson, jsonSettings);
                            editingJson[key] = newJson;
                            SaveEditorState();
                        }
                        catch { }
                    }
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.Space(2);
        }

        private void UpdateSimpleConfig(MonoScript script, string jsonContent)
        {
            try
            {
                var scriptPath = AssetDatabase.GetAssetPath(script);
                var content = System.IO.File.ReadAllText(scriptPath);
                var lines = content.Split('\n');
                var classBodyStart = -1;
                var classBodyEnd = -1;
                var indentation = "";

                // Tìm vị trí bắt đầu của class body
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].TrimEnd();
                    if (line.Contains("class") && line.Contains(":") && !line.Contains("//"))
                    {
                        // Tìm dấu { đầu tiên sau dòng khai báo class
                        for (int j = i; j < lines.Length; j++)
                        {
                            if (lines[j].Contains("{"))
                            {
                                classBodyStart = j + 1;
                                indentation = new string(' ', lines[j].TakeWhile(c => char.IsWhiteSpace(c)).Count() + 4);
                                break;
                            }
                        }
                        break;
                    }
                }

                if (classBodyStart == -1) return;

                // Tìm vị trí kết thúc của class body
                var braceCount = 1; // Đã có 1 { từ class declaration
                for (int i = classBodyStart; i < lines.Length; i++)
                {
                    var line = lines[i].TrimEnd();
                    braceCount += line.Count(c => c == '{');
                    braceCount -= line.Count(c => c == '}');

                    if (braceCount == 0)
                    {
                        classBodyEnd = i;
                        break;
                    }
                }

                if (classBodyEnd == -1) return;

                // Parse JSON và tạo các field mới
                var jsonSettings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Include,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    Converters = new List<JsonConverter> { new UnityObjectJsonConverter() }
                };
                var jsonObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonContent, jsonSettings);
                var newLines = new List<string>();

                // Thêm các field
                foreach (var kvp in jsonObj)
                {
                    var value = FormatValue(kvp.Value);
                    newLines.Add($"{indentation}public {GetTypeString(kvp.Value)} {kvp.Key} = {value};");
                }

                // Kết hợp lại
                var result = new List<string>();
                result.AddRange(lines.Take(classBodyStart));
                result.AddRange(newLines);
                result.AddRange(lines.Skip(classBodyEnd));

                // Ghi file và refresh
                System.IO.File.WriteAllText(scriptPath, string.Join("\n", result));
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                DLogger.LogDebug($"Updated config {script.name} successfully");
            }
            catch (Exception e)
            {
                DLogger.LogError($"Error updating config: {e.Message}");
            }
        }

        private string FormatValue(object value)
        {
            if (value == null) return "null";

            if (value is bool boolValue) return boolValue.ToString().ToLower();
            if (value is string stringValue) return $"\"{stringValue}\"";

            // Handle Unity Object references (converted to JObject by our custom converter)
            if (value is Newtonsoft.Json.Linq.JObject jObject)
            {
                // Check if this is a Unity object representation
                if (jObject.ContainsKey("name") && jObject.ContainsKey("type") && jObject.ContainsKey("instanceID"))
                {
                    return "null"; // Unity objects should be set to null in code generation
                }
                // Handle other JObjects as needed
                return "null";
            }

            // Handle JArray
            if (value is Newtonsoft.Json.Linq.JArray jArray)
            {
                var elementType = GetTypeString(value).TrimEnd(']', '[');
                var elements = jArray.Select(item => FormatJValue(item)).ToList();
                return $"new {elementType}[] {{ {string.Join(", ", elements)} }}";
            }

            // Handle normal arrays
            if (value is Array array)
            {
                var elements = array.Cast<object>().Select(item => FormatValue(item)).ToList();
                return $"new {GetTypeString(value)} {{ {string.Join(", ", elements)} }}";
            }

            // Handle List
            if (value.GetType().IsGenericType && value.GetType().GetGenericTypeDefinition() == typeof(List<>))
            {
                var list = value as System.Collections.IEnumerable;
                var elements = list.Cast<object>().Select(item => FormatValue(item)).ToList();
                return $"new {GetTypeString(value)} {{ {string.Join(", ", elements)} }}";
            }

            return value.ToString();
        }

        private string FormatJValue(Newtonsoft.Json.Linq.JToken token)
        {
            switch (token.Type)
            {
                case Newtonsoft.Json.Linq.JTokenType.String:
                    return $"\"{token.ToString()}\"";
                case Newtonsoft.Json.Linq.JTokenType.Boolean:
                    return token.ToObject<bool>().ToString().ToLower();
                case Newtonsoft.Json.Linq.JTokenType.Integer:
                    var longValue = token.ToObject<long>();
                    return (longValue >= int.MinValue && longValue <= int.MaxValue) ? longValue.ToString() : $"{longValue}";
                case Newtonsoft.Json.Linq.JTokenType.Float:
                    return token.ToObject<double>().ToString();
                case Newtonsoft.Json.Linq.JTokenType.Object:
                    // Handle Unity object references
                    var jObject = token as Newtonsoft.Json.Linq.JObject;
                    if (jObject != null && jObject.ContainsKey("name") && jObject.ContainsKey("type") && jObject.ContainsKey("instanceID"))
                    {
                        return "null"; // Unity objects should be set to null in code generation
                    }
                    return "null";
                case Newtonsoft.Json.Linq.JTokenType.Null:
                    return "null";
                default:
                    return token.ToString();
            }
        }
    }

    /// <summary>
    /// Custom JSON converter to handle Unity Object references (like Sprite, GameObject, etc.)
    /// </summary>
    public class UnityObjectJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(UnityEngine.Object).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            // For reading, we'll return null for Unity objects since we can't deserialize them from JSON
            if (reader.TokenType == JsonToken.Null)
                return null;

            // Skip the value and return null
            reader.Skip();
            return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            var unityObj = value as UnityEngine.Object;
            if (unityObj == null)
            {
                writer.WriteNull();
                return;
            }

            // Write Unity object as a simple object with name and type info
            writer.WriteStartObject();
            writer.WritePropertyName("name");
            writer.WriteValue(unityObj.name);
            writer.WritePropertyName("type");
            writer.WriteValue(unityObj.GetType().Name);
            writer.WritePropertyName("instanceID");
            writer.WriteValue(unityObj.GetInstanceID());
            writer.WriteEndObject();
        }
    }
}