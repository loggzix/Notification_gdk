using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using DSDK.Logger;
using DSDK.Data;

namespace DSDK.Data.Editor
{
    [InitializeOnLoad]
    public class DataMonitorWindow : EditorWindow
    {
        private Vector2 _scrollPosition;
        private DataMonitor _dataSetting;
        private SerializedObject _serializedObject;
        private UnityEditor.Editor _editor;
        private GameObject _prefab;
        private List<System.Type> _dataTypes = new List<System.Type>();
        private bool _stylesInitialized;

        // UI Styles
        private GUIStyle _headerStyle;
        private GUIStyle _sectionHeaderStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _boxStyle;
        private GUIStyle _titleStyle;
        private Color _headerColor = new Color(0.2f, 0.4f, 0.7f);
        private Color _buttonColor = new Color(0.3f, 0.5f, 0.8f);
        private Texture2D _logoTexture;

        private const string PREFAB_PATH = "DataMonitor";
        private const string SCRIPT_TEMPLATE = @"using DSDK.Data;

namespace {0}
{{
    [System.Serializable]
    public partial class {1} : Data<{1}>
    {{
        // TODO: Add your data fields here
    }}
}}";

        private const string SCRIPT_LOGIC_TEMPLATE = @"using DSDK.Data;

namespace {0}
{{
    public partial class {1}
    {{
        // TODO: Add your logic here
    }}
}}";

        [MenuItem("Dmobin/Data Monitor")]
        public static void ShowWindow()
        {
            var window = GetWindow<DataMonitorWindow>("Data Monitor");
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Data Monitor");
            _stylesInitialized = false;

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;

            FindDataSetting();
        }

        private void OnDisable()
        {
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            FindDataSetting();
            Repaint();
        }

        private void OnAfterAssemblyReload()
        {
            FindDataSetting();
            Repaint();
            // CheckForNewDataScripts();
        }

        private void InitializeStyles()
        {
            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                margin = new RectOffset(5, 5, 5, 5)
            };

            _sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                margin = new RectOffset(5, 5, 8, 5)
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(5, 5, 3, 3),
                padding = new RectOffset(8, 8, 4, 4)
            };

            _boxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                margin = new RectOffset(5, 5, 5, 5),
                padding = new RectOffset(10, 10, 10, 10)
            };

            _titleStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(0, 0, 10, 15)
            };

            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            // Đảm bảo styles đã được khởi tạo
            if (!_stylesInitialized)
            {
                if (EditorStyles.boldLabel != null)
                {
                    InitializeStyles();
                }
                else
                {
                    EditorGUILayout.LabelField("Đang khởi tạo...");
                    return;
                }
            }

            // Draw Banner with Logo
            DrawBanner();

            // Main content
            using (new EditorGUILayout.VerticalScope())
            {
                DrawHeader();

                EditorGUILayout.Space(10);

                if (_dataSetting == null || _serializedObject == null)
                {
                    DrawNoMonitorState();
                    return;
                }

                EditorGUILayout.Space(10);
                using (new EditorGUILayout.VerticalScope(_boxStyle))
                {
                    EditorGUILayout.LabelField("Cấu hình Data Monitor", _sectionHeaderStyle);
                    EditorGUILayout.Space(5);

                    using (var scrollView = new EditorGUILayout.ScrollViewScope(_scrollPosition))
                    {
                        _scrollPosition = scrollView.scrollPosition;
                        DrawDataSettingInfo();

                        if (Application.isPlaying)
                        {
                            EditorGUILayout.Space(10);
                            DrawDataStats();
                        }
                    }
                }

                EditorGUILayout.Space(10);
                DrawFooter();
            }

            // Auto repaint để cập nhật realtime
            if (Application.isPlaying)
            {
                Repaint();
            }
        }

        private void DrawBanner()
        {
            Rect bannerRect = EditorGUILayout.GetControlRect(false, 40);
            EditorGUI.DrawRect(bannerRect, _headerColor);

            GUI.color = Color.white;
            EditorGUI.LabelField(bannerRect, "DATA MONITOR", _titleStyle);
            GUI.color = Color.white;

            EditorGUILayout.Space(5);
        }

        private void DrawNoMonitorState()
        {
            using (new EditorGUILayout.VerticalScope(_boxStyle))
            {
                EditorGUILayout.HelpBox($"Không tìm thấy DataMonitor Prefab tại {PREFAB_PATH}", MessageType.Warning);

                EditorGUILayout.Space(10);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Mở thư mục Resources", _buttonStyle, GUILayout.Width(200)))
                    {
                        EditorGUIUtility.PingObject(_dataSetting);
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                string playModeInfo = Application.isPlaying
                    ? "Chế độ: Play - Sử dụng Instance trong Scene"
                    : "Chế độ: Edit - Sử dụng Prefab từ Resources";

                EditorGUILayout.LabelField(playModeInfo, EditorStyles.centeredGreyMiniLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.backgroundColor = _buttonColor;

                    if (GUILayout.Button("Thêm Data", _buttonStyle, GUILayout.ExpandWidth(true)))
                    {
                        CreateNewDataScript();
                    }

                    if (GUILayout.Button("Tạo Instance", _buttonStyle, GUILayout.ExpandWidth(true)))
                    {
                        GenerateMonitorInstances();
                    }

                    GUI.backgroundColor = Color.red;
                    if (GUILayout.Button("Xoá hết Instance", _buttonStyle, GUILayout.ExpandWidth(true)))
                    {
                        ClearAllInstances();
                    }

                    GUI.backgroundColor = _buttonColor;
                    if (GUILayout.Button("Ping", _buttonStyle, GUILayout.Width(80)))
                    {
                        if (Application.isPlaying)
                            EditorGUIUtility.PingObject(_dataSetting.gameObject);
                        else
                            EditorGUIUtility.PingObject(_prefab);
                    }

                    GUI.backgroundColor = Color.white;
                }
            }
        }

        private void DrawGenerateInstanceButton()
        {
            // Chức năng này đã được chuyển lên phần DrawHeader
        }

        private void GenerateMonitorInstances()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Đang xử lý", "Đang tìm kiếm các DataMonitor.cs file...", 0.1f);

                // Find the DataMonitor.cs file
                string[] guids = AssetDatabase.FindAssets("DataMonitor t:Script");
                string dataMonitorPath = "";

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.EndsWith("DataMonitor.cs"))
                    {
                        dataMonitorPath = path;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(dataMonitorPath))
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("Lỗi", "Không tìm thấy file DataMonitor.cs", "OK");
                    return;
                }

                EditorUtility.DisplayProgressBar("Đang xử lý", "Đang đọc file DataMonitor.cs...", 0.2f);
                // Read the DataMonitor.cs file
                string scriptContent = System.IO.File.ReadAllText(dataMonitorPath);

                var dataTypes = new List<System.Type>();
                EditorUtility.DisplayProgressBar("Đang xử lý", "Đang tìm kiếm các lớp Data...", 0.3f);
                var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();

                foreach (var assembly in assemblies)
                {
                    try
                    {
                        var types = assembly.GetTypes()
                            .Where(t => t.IsClass && !t.IsAbstract)
                            .Where(t =>
                            {
                                try
                                {
                                    var baseType = t.BaseType;
                                    // Check if type inherits from any Data<> generic class
                                    while (baseType != null)
                                    {
                                        if (baseType.IsGenericType &&
                                            baseType.GetGenericTypeDefinition().Name.StartsWith("Data`"))
                                        {
                                            return true;
                                        }
                                        baseType = baseType.BaseType;
                                    }
                                    return false;
                                }
                                catch
                                {
                                    return false;
                                }
                            });

                        dataTypes.AddRange(types);
                    }
                    catch (Exception)
                    {
                        // Skip problematic assemblies
                        continue;
                    }
                }

                // Add any additional types from DataBase if that method exists and finds different types
                try
                {
                    EditorUtility.DisplayProgressBar("Đang xử lý", "Đang lấy dữ liệu từ DataBase...", 0.5f);
                    var dbTypes = DataBase.GetAllDataTypes();
                    if (dbTypes != null)
                    {
                        foreach (var type in dbTypes)
                        {
                            if (!dataTypes.Contains(type))
                            {
                                dataTypes.Add(type);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log lỗi nhưng vẫn tiếp tục thực hiện với các kiểu dữ liệu đã tìm thấy
                    DLogger.LogWarning($"Không thể lấy dữ liệu từ DataBase.GetAllDataTypes(): {ex.Message}");
                }

                // Log the found data types
                DLogger.LogDebug($"Found {dataTypes.Count} data types to add to DataMonitor:");
                foreach (var type in dataTypes)
                {
                    DLogger.LogDebug($"- {type.FullName} (Namespace: {type.Namespace ?? "None"})");
                }

                if (dataTypes.Count == 0)
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("Cảnh báo", "Không tìm thấy lớp Data nào. Hãy kiểm tra lại các script của bạn.", "OK");
                    return;
                }

                EditorUtility.DisplayProgressBar("Đang xử lý", "Đang cập nhật file DataMonitor.cs...", 0.7f);

                try
                {
                    // Add using statements for custom namespaces
                    UpdateUsingStatements(scriptContent, dataTypes, out scriptContent);

                    // replace #region Monitor Fields
                    UpdateMonitorFields(scriptContent, dataTypes, out scriptContent);

                    // replace #region Get Default
                    UpdateGetDefault(scriptContent, dataTypes, out scriptContent);

                    // replace #region Load All Data
                    UpdateLoadAllData(scriptContent, dataTypes, out scriptContent);

                    // replace #region Refresh Instances
                    UpdateRefreshInstances(scriptContent, dataTypes, out scriptContent);

                    // replace #region Save All Data
                    UpdateSaveAllData(scriptContent, dataTypes, out scriptContent);

                    EditorUtility.DisplayProgressBar("Đang xử lý", "Đang lưu file...", 0.9f);
                    // Write the updated content back to the file
                    File.WriteAllText(dataMonitorPath, scriptContent);
                }
                catch (Exception ex)
                {
                    EditorUtility.ClearProgressBar();
                    DLogger.LogError($"Lỗi khi cập nhật file: {ex.Message}");
                    EditorUtility.DisplayDialog("Lỗi", $"Không thể cập nhật file DataMonitor.cs: {ex.Message}", "OK");
                    return;
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Thành công", $"DataMonitor.cs đã được cập nhật với {dataTypes.Count} data types", "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                DLogger.LogError($"Error generating instance: {ex.Message}");
                EditorUtility.DisplayDialog("Lỗi", $"Không thể tạo instance: {ex.Message}", "OK");
            }
        }

        private void CreateNewDataScript()
        {
            string path = EditorUtility.SaveFilePanel(
                "Tạo Data Script Mới",
                "Assets/Dmobin/Monitor/Data/Scripts/Monitors",
                "NewData.cs",
                "cs");

            if (string.IsNullOrEmpty(path)) return;

            // Convert to relative path
            path = path.Replace(Application.dataPath, "Assets");

            // Get namespace and class name
            string className = Path.GetFileNameWithoutExtension(path);
            string namespaceName = "DSDK.Data";

            // Generate script content
            string scriptContent = string.Format(SCRIPT_TEMPLATE, namespaceName, className);

            // Create the script file
            File.WriteAllText(path, scriptContent);

            // Create logic script
            string logicPath = path.Replace(".cs", ".Logic.cs");
            string logicContent = string.Format(SCRIPT_LOGIC_TEMPLATE, namespaceName, className);
            File.WriteAllText(logicPath, logicContent);


            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            // Select the new script
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private void FindDataSetting()
        {
            try
            {
                // Cleanup old references
                if (_editor != null) DestroyImmediate(_editor);
                _editor = null;
                _serializedObject = null;
                _dataSetting = null;
                _prefab = null;

                // Nếu đang play thì lấy từ scene
                if (Application.isPlaying)
                {
                    if (DataMonitor.Instance != null)
                    {
                        _dataSetting = DataMonitor.Instance;
                    }
                    else
                    {
                        _prefab = Resources.Load<GameObject>(PREFAB_PATH);
                        if (_prefab != null)
                        {
                            _dataSetting = _prefab.GetComponent<DataMonitor>();
                        }
                    }
                }
                // Không thì lấy từ prefab
                else
                {
                    _prefab = Resources.Load<GameObject>(PREFAB_PATH);
                    if (_prefab != null)
                    {
                        _dataSetting = _prefab.GetComponent<DataMonitor>();
                    }
                }

                if (_dataSetting != null)
                {
                    _serializedObject = new SerializedObject(_dataSetting);
                    _editor = UnityEditor.Editor.CreateEditor(_dataSetting);

                    try
                    {
                        var dataTypes = DataBase.GetAllDataTypes();
                        if (dataTypes != null)
                        {
                            _dataTypes = dataTypes.ToList();
                        }
                        else
                        {
                            _dataTypes = new List<System.Type>();
                            DLogger.LogWarning("DataBase.GetAllDataTypes() trả về null");
                        }
                    }
                    catch (Exception ex)
                    {
                        _dataTypes = new List<System.Type>();
                        DLogger.LogError($"Lỗi khi lấy danh sách data types: {ex.Message}");
                    }
                }
            }
            catch (System.Exception e)
            {
                DLogger.LogError($"[DataSetting] Lỗi khi tìm DataSetting: {e}");
            }
        }

        private void DrawDataSettingInfo()
        {
            using (new EditorGUILayout.VerticalScope(_boxStyle))
            {
                // Draw inspector
                EditorGUI.BeginChangeCheck();
                _serializedObject.Update();

                _editor.OnInspectorGUI();

                if (EditorGUI.EndChangeCheck())
                {
                    _serializedObject.ApplyModifiedProperties();

                    if (!Application.isPlaying)
                    {
                        // Đánh dấu prefab là dirty
                        EditorUtility.SetDirty(_dataSetting);
                        if (_prefab != null)
                        {
                            PrefabUtility.RecordPrefabInstancePropertyModifications(_dataSetting);
                            AssetDatabase.SaveAssets();
                        }
                    }
                    else
                    {
                        _dataSetting = DataMonitor.Instance;
                    }
                }
            }
        }

        private void DrawDataStats()
        {
            if (!Application.isPlaying) return;

            try
            {
                // Kiểm tra xem DataMonitor.Instance có tồn tại không
                if (DataMonitor.Instance == null)
                {
                    EditorGUILayout.HelpBox("DataMonitor.Instance không tồn tại", MessageType.Warning);
                    return;
                }

                var monitorInfo = DataMonitor.Instance.MonitorInfo;
                if (monitorInfo == null)
                {
                    EditorGUILayout.HelpBox("MonitorInfo không tồn tại", MessageType.Warning);
                    return;
                }

                if (monitorInfo.StatsMap == null || monitorInfo.StatsMap.Count == 0)
                {
                    EditorGUILayout.HelpBox("Chưa có dữ liệu thống kê", MessageType.Info);
                    return;
                }

                using (new EditorGUILayout.VerticalScope(_boxStyle))
                {
                    EditorGUILayout.LabelField("Thống Kê Dữ Liệu", _sectionHeaderStyle);
                    EditorGUILayout.Space(5);

                    // Header
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField("Loại", EditorStyles.boldLabel, GUILayout.Width(150));
                        EditorGUILayout.LabelField("Lưu", EditorStyles.boldLabel, GUILayout.Width(50));
                        EditorGUILayout.LabelField("Tải", EditorStyles.boldLabel, GUILayout.Width(50));
                        EditorGUILayout.LabelField("Truy Cập Cuối", EditorStyles.boldLabel);
                    }

                    // Data rows
                    foreach (var stat in monitorInfo.StatsMap.Values)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(stat.TypeName, GUILayout.Width(150));
                            EditorGUILayout.LabelField(stat.SaveCount.ToString(), GUILayout.Width(50));
                            EditorGUILayout.LabelField(stat.LoadCount.ToString(), GUILayout.Width(50));
                            EditorGUILayout.LabelField(stat.LastSavedTime ?? "Chưa bao giờ");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                EditorGUILayout.HelpBox($"Lỗi khi hiển thị thống kê: {ex.Message}", MessageType.Error);
            }
        }

        private void DrawFooter()
        {
            // Kiểm tra xem có DataMonitor không (Play mode cần Instance, Edit mode cần prefab)
            if (Application.isPlaying)
            {
                if (DataMonitor.Instance == null)
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.HelpBox("DataMonitor Instance không được tìm thấy trong Scene", MessageType.Warning);
                    }
                    return;
                }
            }
            else
            {
                if (_dataSetting == null)
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.HelpBox("DataMonitor Prefab không được tìm thấy", MessageType.Warning);
                    }
                    return;
                }
            }

            using (new EditorGUILayout.VerticalScope(_boxStyle))
            {
                EditorGUILayout.LabelField("Thao Tác Dữ Liệu", _sectionHeaderStyle);
                EditorGUILayout.Space(5);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    GUI.backgroundColor = new Color(0.3f, 0.7f, 0.3f);
                    if (GUILayout.Button("Tải", _buttonStyle, GUILayout.Width(120)))
                    {
                        if (Application.isPlaying)
                        {
                            DataMonitor.Instance.LoadAllData();
                        }
                        else
                        {
                            LoadDataInEditMode();
                        }
                        EditorApplication.delayCall += RefreshInspector;
                    }

                    GUI.backgroundColor = new Color(0.3f, 0.5f, 0.8f);
                    if (GUILayout.Button("Lưu", _buttonStyle, GUILayout.Width(120)))
                    {
                        if (Application.isPlaying)
                        {
                            DataMonitor.Instance.SaveAllData();
                        }
                        else
                        {
                            SaveDataInEditMode();
                        }
                        EditorApplication.delayCall += RefreshInspector;
                    }

                    GUI.backgroundColor = new Color(0.8f, 0.3f, 0.3f);
                    if (GUILayout.Button("Xóa", _buttonStyle, GUILayout.Width(120)))
                    {
                        if (EditorUtility.DisplayDialog("Xác nhận", "Bạn có chắc muốn xóa tất cả dữ liệu?", "Có", "Không"))
                        {
                            if (Application.isPlaying)
                            {
                                DataMonitor.Instance.DeleteAllData();
                            }
                            else
                            {
                                DeleteDataInEditMode();
                            }
                            EditorApplication.delayCall += RefreshInspector;
                        }
                    }

                    GUI.backgroundColor = Color.white;

                    GUILayout.FlexibleSpace();
                }
            }
        }

        /// <summary>
        /// Load data in Edit mode - update prefab fields directly
        /// </summary>
        private void LoadDataInEditMode()
        {
            try
            {
                if (_dataSetting == null)
                {
                    DLogger.LogError("DataSetting is null in Edit mode", channel: "DataMonitorWindow");
                    return;
                }

                // Setup FileDataHandler with settings from prefab
                var profileId = GetPrivateField<string>(_dataSetting, "_profileId") ?? "main";
                var saveType = GetPrivateField<DataSaveType>(_dataSetting, "_saveType");
                var debug = GetPrivateField<bool>(_dataSetting, "_debug");

                FileDataHandler.Instance.Setup(profileId, Application.persistentDataPath, debug, saveType);

                // Find all data fields and load them
                var dataFields = GetDataFields(_dataSetting);
                int loadedCount = 0;

                foreach (var field in dataFields)
                {
                    try
                    {
                        var fieldType = field.FieldType;
                        var keyProperty = fieldType.GetProperty("Key");

                        if (keyProperty != null)
                        {
                            // Create temporary instance to get key
                            var tempInstance = Activator.CreateInstance(fieldType);
                            var key = keyProperty.GetValue(tempInstance) as string;

                            if (!string.IsNullOrEmpty(key))
                            {
                                // Use reflection to call generic Load method
                                var loadMethod = typeof(FileDataHandler).GetMethod("Load").MakeGenericMethod(fieldType);
                                var loadedData = loadMethod.Invoke(FileDataHandler.Instance, new object[] { key, profileId, false });

                                // Set the loaded data to the field
                                field.SetValue(_dataSetting, loadedData);
                                loadedCount++;

                                DLogger.LogInfo($"Loaded {fieldType.Name} with key {key}", channel: "DataMonitorWindow");
                            }
                        }
                    }
                    catch (Exception fieldEx)
                    {
                        DLogger.LogError($"Error loading field {field.Name}: {fieldEx.Message}", channel: "DataMonitorWindow");
                    }
                }

                DLogger.LogInfo($"Data loaded in Edit mode - {loadedCount} fields processed", channel: "DataMonitorWindow");
            }
            catch (Exception ex)
            {
                DLogger.LogError($"Error loading data in Edit mode: {ex.Message}", channel: "DataMonitorWindow");
            }
        }

        /// <summary>
        /// Save data in Edit mode - save from prefab fields
        /// </summary>
        private void SaveDataInEditMode()
        {
            try
            {
                if (_dataSetting == null)
                {
                    DLogger.LogError("DataSetting is null in Edit mode", channel: "DataMonitorWindow");
                    return;
                }

                // Setup FileDataHandler with settings from prefab
                var profileId = GetPrivateField<string>(_dataSetting, "_profileId") ?? "main";
                var saveType = GetPrivateField<DataSaveType>(_dataSetting, "_saveType");
                var debug = GetPrivateField<bool>(_dataSetting, "_debug");

                FileDataHandler.Instance.Setup(profileId, Application.persistentDataPath, debug, saveType);

                // Find all data fields and save them
                var dataFields = GetDataFields(_dataSetting);
                int savedCount = 0;

                foreach (var field in dataFields)
                {
                    try
                    {
                        var dataInstance = field.GetValue(_dataSetting);

                        if (dataInstance != null)
                        {
                            // Call Save method on the data instance
                            var saveMethod = dataInstance.GetType().GetMethod("Save");
                            if (saveMethod != null)
                            {
                                saveMethod.Invoke(dataInstance, null);
                                savedCount++;

                                DLogger.LogInfo($"Saved {field.FieldType.Name}", channel: "DataMonitorWindow");
                            }
                        }
                    }
                    catch (Exception fieldEx)
                    {
                        DLogger.LogError($"Error saving field {field.Name}: {fieldEx.Message}", channel: "DataMonitorWindow");
                    }
                }

                DLogger.LogInfo($"Data saved in Edit mode - {savedCount} fields processed", channel: "DataMonitorWindow");
            }
            catch (Exception ex)
            {
                DLogger.LogError($"Error saving data in Edit mode: {ex.Message}", channel: "DataMonitorWindow");
            }
        }

        /// <summary>
        /// Delete data in Edit mode - delete saved data and reset prefab fields
        /// </summary>
        private void DeleteDataInEditMode()
        {
            try
            {
                if (_dataSetting == null)
                {
                    DLogger.LogError("DataSetting is null in Edit mode", channel: "DataMonitorWindow");
                    return;
                }

                // Setup FileDataHandler with settings from prefab
                var profileId = GetPrivateField<string>(_dataSetting, "_profileId") ?? "main";
                var saveType = GetPrivateField<DataSaveType>(_dataSetting, "_saveType");
                var debug = GetPrivateField<bool>(_dataSetting, "_debug");

                FileDataHandler.Instance.Setup(profileId, Application.persistentDataPath, debug, saveType);

                // Delete saved data
                FileDataHandler.Instance.DeleteProfile(profileId);

                // Reset prefab fields to default values
                var dataFields = GetDataFields(_dataSetting);
                int resetCount = 0;

                foreach (var field in dataFields)
                {
                    try
                    {
                        // Create new instance of the data type
                        var newInstance = Activator.CreateInstance(field.FieldType);
                        field.SetValue(_dataSetting, newInstance);
                        resetCount++;

                        DLogger.LogInfo($"Reset {field.FieldType.Name} to default", channel: "DataMonitorWindow");
                    }
                    catch (Exception fieldEx)
                    {
                        DLogger.LogError($"Error resetting field {field.Name}: {fieldEx.Message}", channel: "DataMonitorWindow");
                    }
                }

                DLogger.LogInfo($"Data deleted in Edit mode - {resetCount} fields reset", channel: "DataMonitorWindow");
            }
            catch (Exception ex)
            {
                DLogger.LogError($"Error deleting data in Edit mode: {ex.Message}", channel: "DataMonitorWindow");
            }
        }

        /// <summary>
        /// Get all data fields from DataMonitor using reflection
        /// </summary>
        private FieldInfo[] GetDataFields(object dataMonitor)
        {
            var fields = dataMonitor.GetType()
                .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => f.FieldType.IsClass &&
                           f.FieldType != typeof(string) &&
                           !f.FieldType.IsEnum &&
                           f.FieldType.GetProperty("Key") != null &&
                           f.Name.StartsWith("_") &&
                           !f.Name.Equals("_profileId") &&
                           !f.Name.Equals("_debug") &&
                           !f.Name.Equals("_saveType") &&
                           !f.Name.Equals("_getDefaultFromMonitor"))
                .ToArray();

            return fields;
        }

        /// <summary>
        /// Get private field value using reflection
        /// </summary>
        private TField GetPrivateField<TField>(object obj, string fieldName)
        {
            var field = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                return (TField)field.GetValue(obj);
            }
            return default(TField);
        }

        /// <summary>
        /// Set private field value using reflection
        /// </summary>
        private void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(obj, value);
            }
        }

        /// <summary>
        /// Refresh inspector after data operations to show updated values
        /// </summary>
        private void RefreshInspector()
        {
            try
            {
                if (_dataSetting != null && _serializedObject != null)
                {
                    // Refresh all data instances first
                    if (Application.isPlaying && DataMonitor.Instance != null)
                    {
                        DataMonitor.Instance.RefreshAllInstances();
                    }

                    // Update serialized object to reflect changes
                    _serializedObject.Update();

                    // Mark the object as dirty to show changes
                    EditorUtility.SetDirty(_dataSetting);

                    // If we have a prefab reference, record modifications
                    if (_prefab != null && !Application.isPlaying)
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(_dataSetting);
                        AssetDatabase.SaveAssets();
                    }

                    // Force repaint to update UI
                    Repaint();

                    // Refresh scene view if needed
                    SceneView.RepaintAll();

                    DLogger.LogInfo("Inspector refreshed after data operation", channel: "DataMonitorWindow");
                }
            }
            catch (Exception ex)
            {
                DLogger.LogError($"Error refreshing inspector: {ex.Message}");
            }
        }

        // Cập nhật using statements cho các namespace tùy chỉnh
        private void UpdateUsingStatements(string scriptContent, List<System.Type> dataTypes, out string updatedContent)
        {
            try
            {
                // Lấy danh sách các namespace duy nhất từ các data types
                var namespaces = dataTypes
                    .Where(t => !string.IsNullOrEmpty(t.Namespace))
                    .Select(t => t.Namespace)
                    .Where(ns => ns != "DSDK.Data") // Loại trừ namespace mặc định
                    .Distinct()
                    .OrderBy(ns => ns)
                    .ToList();

                if (namespaces.Count == 0)
                {
                    updatedContent = scriptContent;
                    return;
                }

                // Tìm vị trí để chèn using statements (sau using UnityEngine;)
                var usingUnityEngine = scriptContent.IndexOf("using UnityEngine;");
                if (usingUnityEngine < 0)
                {
                    DLogger.LogWarning("Không tìm thấy 'using UnityEngine;' trong DataMonitor.cs");
                    updatedContent = scriptContent;
                    return;
                }

                var insertPosition = scriptContent.IndexOf('\n', usingUnityEngine) + 1;

                // Tạo các using statements mới
                var newUsingStatements = "";
                foreach (var ns in namespaces)
                {
                    var usingStatement = $"using {ns};";
                    // Kiểm tra xem using statement đã tồn tại chưa
                    if (!scriptContent.Contains(usingStatement))
                    {
                        newUsingStatements += usingStatement + "\n";
                        DLogger.LogDebug($"Thêm using statement: {usingStatement}");
                    }
                }

                // Chèn các using statements mới
                if (!string.IsNullOrEmpty(newUsingStatements))
                {
                    updatedContent = scriptContent.Insert(insertPosition, newUsingStatements);
                    DLogger.LogInfo($"Đã thêm {namespaces.Count} using statements cho các namespace tùy chỉnh");
                }
                else
                {
                    updatedContent = scriptContent;
                    DLogger.LogInfo("Tất cả using statements đã tồn tại");
                }
            }
            catch (Exception ex)
            {
                DLogger.LogError($"Lỗi khi cập nhật using statements: {ex.Message}");
                updatedContent = scriptContent;
            }
        }

        // Cập nhật phần Monitor Fields
        private void UpdateMonitorFields(string scriptContent, List<System.Type> dataTypes, out string updatedContent)
        {
            try
            {
                var monitorFields = scriptContent.IndexOf("#region Monitor Fields");
                if (monitorFields < 0)
                {
                    DLogger.LogError("Không tìm thấy #region Monitor Fields trong DataMonitor.cs");
                    updatedContent = scriptContent;
                    return;
                }

                var endMonitorFields = scriptContent.IndexOf("#endregion", monitorFields);
                if (endMonitorFields < 0)
                {
                    DLogger.LogError("Không tìm thấy #endregion cho Monitor Fields trong DataMonitor.cs");
                    updatedContent = scriptContent;
                    return;
                }

                var bodyMonitorFields = scriptContent.Substring(monitorFields, endMonitorFields - monitorFields);
                var newBodyMonitorFields = "#region Monitor Fields";
                foreach (var type in dataTypes)
                {
                    newBodyMonitorFields += $"\n        [SerializeField] private {type.Name} _{char.ToLowerInvariant(type.Name[0]) + type.Name.Substring(1)};";
                }
                newBodyMonitorFields += "\n        ";
                updatedContent = scriptContent.Replace(bodyMonitorFields, newBodyMonitorFields);
            }
            catch (Exception ex)
            {
                DLogger.LogError($"Lỗi khi cập nhật Monitor Fields: {ex.Message}");
                updatedContent = scriptContent;
            }
        }

        // Cập nhật phần Get Default
        private void UpdateGetDefault(string scriptContent, List<System.Type> dataTypes, out string updatedContent)
        {
            try
            {
                var getDefaultMethod = scriptContent.IndexOf("#region Get Default");
                if (getDefaultMethod < 0)
                {
                    DLogger.LogError("Không tìm thấy #region Get Default trong DataMonitor.cs");
                    updatedContent = scriptContent;
                    return;
                }

                var endGetDefaultMethod = scriptContent.IndexOf("#endregion", getDefaultMethod);
                if (endGetDefaultMethod < 0)
                {
                    DLogger.LogError("Không tìm thấy #endregion cho Get Default trong DataMonitor.cs");
                    updatedContent = scriptContent;
                    return;
                }

                var methodBody = scriptContent.Substring(getDefaultMethod, endGetDefaultMethod - getDefaultMethod);
                var newMethodBody = "#region Get Default";
                foreach (var type in dataTypes)
                {
                    string varName = $"_{char.ToLowerInvariant(type.Name[0]) + type.Name.Substring(1)}";
                    newMethodBody += $"\n\n                if (!FileDataHandler.Instance.IsExist({varName}.Key))";
                    newMethodBody += $"\n                {{";
                    newMethodBody += $"\n                    {type.Name}.SetInstance({varName});";
                    newMethodBody += $"\n";
                    newMethodBody += $"\n                    {varName}.Save();";
                    newMethodBody += $"\n                }}";
                }
                newMethodBody += "\n\n                ";
                updatedContent = scriptContent.Replace(methodBody, newMethodBody);
            }
            catch (Exception ex)
            {
                DLogger.LogError($"Lỗi khi cập nhật Get Default: {ex.Message}");
                updatedContent = scriptContent;
            }
        }

        // Cập nhật phần Load All Data
        private void UpdateLoadAllData(string scriptContent, List<System.Type> dataTypes, out string updatedContent)
        {
            try
            {
                var loadAllDataMethod = scriptContent.IndexOf("#region Load All Data");
                if (loadAllDataMethod < 0)
                {
                    DLogger.LogError("Không tìm thấy #region Load All Data trong DataMonitor.cs");
                    updatedContent = scriptContent;
                    return;
                }

                var endLoadAllDataMethod = scriptContent.IndexOf("#endregion", loadAllDataMethod);
                if (endLoadAllDataMethod < 0)
                {
                    DLogger.LogError("Không tìm thấy #endregion cho Load All Data trong DataMonitor.cs");
                    updatedContent = scriptContent;
                    return;
                }

                var methodBody = scriptContent.Substring(loadAllDataMethod, endLoadAllDataMethod - loadAllDataMethod);
                var newMethodBody = "#region Load All Data";
                foreach (var type in dataTypes)
                {
                    newMethodBody += $"\n            {type.Name}.Instance.Load();";
                }
                newMethodBody += "\n            ";
                updatedContent = scriptContent.Replace(methodBody, newMethodBody);
            }
            catch (Exception ex)
            {
                DLogger.LogError($"Lỗi khi cập nhật Load All Data: {ex.Message}");
                updatedContent = scriptContent;
            }
        }

        // Cập nhật phần Refresh Instances
        private void UpdateRefreshInstances(string scriptContent, List<System.Type> dataTypes, out string updatedContent)
        {
            try
            {
                var refreshInstancesMethod = scriptContent.IndexOf("#region Refresh Instances");
                if (refreshInstancesMethod < 0)
                {
                    DLogger.LogError("Không tìm thấy #region Refresh Instances trong DataMonitor.cs");
                    updatedContent = scriptContent;
                    return;
                }

                var endRefreshInstancesMethod = scriptContent.IndexOf("#endregion", refreshInstancesMethod);
                if (endRefreshInstancesMethod < 0)
                {
                    DLogger.LogError("Không tìm thấy #endregion cho Refresh Instances trong DataMonitor.cs");
                    updatedContent = scriptContent;
                    return;
                }

                var methodBody = scriptContent.Substring(refreshInstancesMethod, endRefreshInstancesMethod - refreshInstancesMethod);
                var newMethodBody = "#region Refresh Instances";
                foreach (var type in dataTypes)
                {
                    string varName = $"_{char.ToLowerInvariant(type.Name[0]) + type.Name.Substring(1)}";
                    newMethodBody += $"\n            {varName} = {type.Name}.Instance;";
                }
                newMethodBody += "\n            ";
                updatedContent = scriptContent.Replace(methodBody, newMethodBody);
            }
            catch (Exception ex)
            {
                DLogger.LogError($"Lỗi khi cập nhật Refresh Instances: {ex.Message}");
                updatedContent = scriptContent;
            }
        }

        // Cập nhật phần Save All Data
        private void UpdateSaveAllData(string scriptContent, List<System.Type> dataTypes, out string updatedContent)
        {
            try
            {
                var saveAllDataMethod = scriptContent.IndexOf("#region Save All Data");
                if (saveAllDataMethod < 0)
                {
                    DLogger.LogError("Không tìm thấy #region Save All Data trong DataMonitor.cs");
                    updatedContent = scriptContent;
                    return;
                }

                var endSaveAllDataMethod = scriptContent.IndexOf("#endregion", saveAllDataMethod);
                if (endSaveAllDataMethod < 0)
                {
                    DLogger.LogError("Không tìm thấy #endregion cho Save All Data trong DataMonitor.cs");
                    updatedContent = scriptContent;
                    return;
                }

                var methodBody = scriptContent.Substring(saveAllDataMethod, endSaveAllDataMethod - saveAllDataMethod);
                var newMethodBody = "#region Save All Data";
                foreach (var type in dataTypes)
                {
                    string varName = $"_{char.ToLowerInvariant(type.Name[0]) + type.Name.Substring(1)}";
                    newMethodBody += $"\n            {varName}.Save();";
                }
                newMethodBody += "\n            ";
                updatedContent = scriptContent.Replace(methodBody, newMethodBody);
            }
            catch (Exception ex)
            {
                DLogger.LogError($"Lỗi khi cập nhật Save All Data: {ex.Message}");
                updatedContent = scriptContent;
            }
        }

        private void ClearAllInstances()
        {
            if (!EditorUtility.DisplayDialog("Xác nhận",
                "Bạn có chắc chắn muốn xóa tất cả các instance đã được generate?\n\nHành động này không thể hoàn tác!",
                "Xóa", "Hủy"))
            {
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Đang xử lý", "Đang tìm file DataMonitor.cs...", 0.1f);

                // Find the DataMonitor.cs file
                string[] guids = AssetDatabase.FindAssets("DataMonitor t:Script");
                string dataMonitorPath = "";

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.EndsWith("DataMonitor.cs"))
                    {
                        dataMonitorPath = path;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(dataMonitorPath))
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("Lỗi", "Không tìm thấy file DataMonitor.cs", "OK");
                    return;
                }

                EditorUtility.DisplayProgressBar("Đang xử lý", "Đang đọc file DataMonitor.cs...", 0.3f);
                string scriptContent = File.ReadAllText(dataMonitorPath);

                EditorUtility.DisplayProgressBar("Đang xử lý", "Đang xóa các region...", 0.5f);

                // Clear all regions
                ClearMonitorFields(scriptContent, out scriptContent);
                ClearGetDefault(scriptContent, out scriptContent);
                ClearLoadAllData(scriptContent, out scriptContent);
                ClearRefreshInstances(scriptContent, out scriptContent);
                ClearSaveAllData(scriptContent, out scriptContent);

                // Clear custom using statements
                ClearCustomUsingStatements(scriptContent, out scriptContent);

                EditorUtility.DisplayProgressBar("Đang xử lý", "Đang lưu file...", 0.9f);
                File.WriteAllText(dataMonitorPath, scriptContent);

                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Thành công", "Đã xóa tất cả các instance trong DataMonitor.cs", "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                DLogger.LogError($"Lỗi khi xóa instances: {ex.Message}");
                EditorUtility.DisplayDialog("Lỗi", $"Không thể xóa instances: {ex.Message}", "OK");
            }
        }

        // Các hàm Clear để xóa nội dung các region
        private void ClearMonitorFields(string scriptContent, out string updatedContent)
        {
            try
            {
                var monitorFields = scriptContent.IndexOf("#region Monitor Fields");
                if (monitorFields < 0)
                {
                    updatedContent = scriptContent;
                    return;
                }

                var endMonitorFields = scriptContent.IndexOf("#endregion", monitorFields);
                if (endMonitorFields < 0)
                {
                    updatedContent = scriptContent;
                    return;
                }

                var bodyMonitorFields = scriptContent.Substring(monitorFields, endMonitorFields - monitorFields);
                var newBodyMonitorFields = "#region Monitor Fields\n        ";
                updatedContent = scriptContent.Replace(bodyMonitorFields, newBodyMonitorFields);
            }
            catch (Exception ex)
            {
                DLogger.LogError($"Lỗi khi xóa Monitor Fields: {ex.Message}");
                updatedContent = scriptContent;
            }
        }

        private void ClearGetDefault(string scriptContent, out string updatedContent)
        {
            try
            {
                var getDefaultMethod = scriptContent.IndexOf("#region Get Default");
                if (getDefaultMethod < 0)
                {
                    updatedContent = scriptContent;
                    return;
                }

                var endGetDefaultMethod = scriptContent.IndexOf("#endregion", getDefaultMethod);
                if (endGetDefaultMethod < 0)
                {
                    updatedContent = scriptContent;
                    return;
                }

                var methodBody = scriptContent.Substring(getDefaultMethod, endGetDefaultMethod - getDefaultMethod);
                var newMethodBody = "#region Get Default\n                ";
                updatedContent = scriptContent.Replace(methodBody, newMethodBody);
            }
            catch (Exception ex)
            {
                DLogger.LogError($"Lỗi khi xóa Get Default: {ex.Message}");
                updatedContent = scriptContent;
            }
        }

        private void ClearLoadAllData(string scriptContent, out string updatedContent)
        {
            try
            {
                var loadAllDataMethod = scriptContent.IndexOf("#region Load All Data");
                if (loadAllDataMethod < 0)
                {
                    updatedContent = scriptContent;
                    return;
                }

                var endLoadAllDataMethod = scriptContent.IndexOf("#endregion", loadAllDataMethod);
                if (endLoadAllDataMethod < 0)
                {
                    updatedContent = scriptContent;
                    return;
                }

                var methodBody = scriptContent.Substring(loadAllDataMethod, endLoadAllDataMethod - loadAllDataMethod);
                var newMethodBody = "#region Load All Data\n            ";
                updatedContent = scriptContent.Replace(methodBody, newMethodBody);
            }
            catch (Exception ex)
            {
                DLogger.LogError($"Lỗi khi xóa Load All Data: {ex.Message}");
                updatedContent = scriptContent;
            }
        }

        private void ClearRefreshInstances(string scriptContent, out string updatedContent)
        {
            try
            {
                var refreshInstancesMethod = scriptContent.IndexOf("#region Refresh Instances");
                if (refreshInstancesMethod < 0)
                {
                    updatedContent = scriptContent;
                    return;
                }

                var endRefreshInstancesMethod = scriptContent.IndexOf("#endregion", refreshInstancesMethod);
                if (endRefreshInstancesMethod < 0)
                {
                    updatedContent = scriptContent;
                    return;
                }

                var methodBody = scriptContent.Substring(refreshInstancesMethod, endRefreshInstancesMethod - refreshInstancesMethod);
                var newMethodBody = "#region Refresh Instances\n            ";
                updatedContent = scriptContent.Replace(methodBody, newMethodBody);
            }
            catch (Exception ex)
            {
                DLogger.LogError($"Lỗi khi xóa Refresh Instances: {ex.Message}");
                updatedContent = scriptContent;
            }
        }

        private void ClearSaveAllData(string scriptContent, out string updatedContent)
        {
            try
            {
                var saveAllDataMethod = scriptContent.IndexOf("#region Save All Data");
                if (saveAllDataMethod < 0)
                {
                    updatedContent = scriptContent;
                    return;
                }

                var endSaveAllDataMethod = scriptContent.IndexOf("#endregion", saveAllDataMethod);
                if (endSaveAllDataMethod < 0)
                {
                    updatedContent = scriptContent;
                    return;
                }

                var methodBody = scriptContent.Substring(saveAllDataMethod, endSaveAllDataMethod - saveAllDataMethod);
                var newMethodBody = "#region Save All Data\n            ";
                updatedContent = scriptContent.Replace(methodBody, newMethodBody);
            }
            catch (Exception ex)
            {
                DLogger.LogError($"Lỗi khi xóa Save All Data: {ex.Message}");
                updatedContent = scriptContent;
            }
        }

        private void ClearCustomUsingStatements(string scriptContent, out string updatedContent)
        {
            try
            {
                // Danh sách các using statements mặc định cần giữ lại
                var defaultUsings = new HashSet<string>
                {
                    "using UnityEngine;"
                };

                var lines = scriptContent.Split('\n').ToList();
                var linesToRemove = new List<int>();

                // Tìm các using statements tùy chỉnh để xóa
                for (int i = 0; i < lines.Count; i++)
                {
                    var line = lines[i].Trim();
                    if (line.StartsWith("using ") && line.EndsWith(";"))
                    {
                        // Nếu không phải using statement mặc định thì đánh dấu để xóa
                        if (!defaultUsings.Contains(line))
                        {
                            linesToRemove.Add(i);
                            DLogger.LogDebug($"Đánh dấu xóa using statement: {line}");
                        }
                    }
                }

                // Xóa các dòng từ cuối lên đầu để không ảnh hưởng index
                for (int i = linesToRemove.Count - 1; i >= 0; i--)
                {
                    lines.RemoveAt(linesToRemove[i]);
                }

                updatedContent = string.Join("\n", lines);

                if (linesToRemove.Count > 0)
                {
                    DLogger.LogInfo($"Đã xóa {linesToRemove.Count} using statements tùy chỉnh");
                }
            }
            catch (Exception ex)
            {
                DLogger.LogError($"Lỗi khi xóa custom using statements: {ex.Message}");
                updatedContent = scriptContent;
            }
        }

        private void OnDestroy()
        {
            if (_editor != null)
            {
                DestroyImmediate(_editor);
                _editor = null;
            }
        }
    }
}