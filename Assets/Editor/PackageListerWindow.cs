using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

public class PackageListerWindow : EditorWindow
{
    private const string PACKAGES_API_URL = "https://upm.dmobin.studio/-/verdaccio/data/packages";

    private List<PackageInfo> packages = new List<PackageInfo>();
    private Vector2 scrollPosition;
    private string searchText = "";
    private bool isLoading = false;
    private string errorMessage = "";
    private GUIStyle packageBoxStyle;
    private GUIStyle titleStyle;
    private GUIStyle descriptionStyle;
    private GUIStyle highlightStyle;
    private GUIStyle keywordButtonStyle;
    private bool stylesInitialized = false;
    private Dictionary<string, bool> installingPackages = new Dictionary<string, bool>();
    private Dictionary<string, bool> installedPackages = new Dictionary<string, bool>();
    private Dictionary<string, string> installedVersions = new Dictionary<string, string>();
    private Dictionary<string, bool> installedAsDependencies = new Dictionary<string, bool>(); // True nếu cài bởi dependency
    private ListRequest listRequest;
    private string selectedPackageName = "";
    private int selectedTab = 0; // 0 = All Packages, 1 = Installed
    private string[] tabNames = { "📦 All Packages", "✅ Installed" };
    private float loadingAnimationTimer = 0f;
    private int loadingDotsCount = 0;
    private List<string> allKeywords = new List<string>();
    private Dictionary<string, int> keywordCounts = new Dictionary<string, int>();
    private List<string> selectedKeywords = new List<string>();
    private List<string> allAuthors = new List<string>();
    private Dictionary<string, int> authorCounts = new Dictionary<string, int>();
    private List<string> selectedAuthors = new List<string>();
    private bool showFilters = false; // Trạng thái hiển thị filters (keywords và authors)

    [MenuItem("Dmobin/Package Manager")]
    public static void ShowWindow()
    {
        PackageListerWindow window = GetWindow<PackageListerWindow>("Dmobin Packages");
        window.minSize = new Vector2(600, 400);
        window.Show();
    }

    private void OnEnable()
    {
        LoadPackages();
        CheckInstalledPackages();
    }

    private void CheckInstalledPackages()
    {
        // Đọc trực tiếp từ manifest.json để kiểm tra packages đã cài
        LoadInstalledPackagesFromManifest();
        
        // Vẫn giữ việc sử dụng Client.List() để cập nhật thông tin chính xác
        listRequest = Client.List();
        CheckListProgressWrapper();
    }

    private void LoadInstalledPackagesFromManifest()
    {
        installedPackages.Clear();
        installedVersions.Clear();
        installedAsDependencies.Clear();
        
        try
        {
            // 1. Đọc manifest.json để lấy packages cài trực tiếp
            string manifestPath = "Packages/manifest.json";
            if (System.IO.File.Exists(manifestPath))
            {
                string jsonText = System.IO.File.ReadAllText(manifestPath);
                
                // Parse JSON thủ công để lấy dependencies
                int dependenciesStart = jsonText.IndexOf("\"dependencies\"");
                if (dependenciesStart != -1)
                {
                    int braceStart = jsonText.IndexOf('{', dependenciesStart);
                    int braceEnd = FindMatchingBrace(jsonText, braceStart);
                    
                    if (braceEnd != -1)
                    {
                        string dependenciesBlock = jsonText.Substring(braceStart + 1, braceEnd - braceStart - 1);
                        
                        // Parse từng dòng dependency
                        string[] lines = dependenciesBlock.Split(new[] { '\n', '\r' },
                            System.StringSplitOptions.RemoveEmptyEntries);
                        
                        foreach (string line in lines)
                        {
                            string trimmedLine = line.Trim();
                            if (trimmedLine.StartsWith("\"com.dmobin"))
                            {
                                // Parse package name và version
                                // Format: "com.dmobin.sdk.core": "1.1.13",
                                int firstQuote = trimmedLine.IndexOf('"');
                                int secondQuote = trimmedLine.IndexOf('"', firstQuote + 1);
                                int thirdQuote = trimmedLine.IndexOf('"', secondQuote + 1);
                                int fourthQuote = trimmedLine.IndexOf('"', thirdQuote + 1);
                                
                                if (firstQuote != -1 && secondQuote != -1 && thirdQuote != -1 && fourthQuote != -1)
                                {
                                    string packageName =
                                        trimmedLine.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
                                    string version =
                                        trimmedLine.Substring(thirdQuote + 1, fourthQuote - thirdQuote - 1);
                                    
                                    installedPackages[packageName] = true;
                                    installedAsDependencies[packageName] = false; // Cài trực tiếp
                                    
                                    // Chỉ lưu version nếu không phải URL
                                    if (!version.StartsWith("http") && !version.StartsWith("git"))
                                    {
                                        installedVersions[packageName] = version;
                                    }
                                    }
                                }
                            }
                        }
                    }
                }
                
            // 2. Đọc packages-lock.json để lấy tất cả packages (bao gồm dependencies)
            string lockPath = "Packages/packages-lock.json";
            if (System.IO.File.Exists(lockPath))
            {
                string lockJsonText = System.IO.File.ReadAllText(lockPath);
                
                // Parse packages-lock.json để tìm packages với depth > 0 và url = dmobin registry
                int dependenciesStart = lockJsonText.IndexOf("\"dependencies\"");
                if (dependenciesStart != -1)
                {
                    int braceStart = lockJsonText.IndexOf('{', dependenciesStart);
                    int braceEnd = FindMatchingBrace(lockJsonText, braceStart);
                    
                    if (braceEnd != -1)
                    {
                        string dependenciesBlock = lockJsonText.Substring(braceStart + 1, braceEnd - braceStart - 1);
                        
                        // Tách từng package block
                        string[] packageBlocks = SplitPackageBlocks(dependenciesBlock);
                        
                        foreach (string block in packageBlocks)
                        {
                            if (block.Contains("\"com.dmobin") && block.Contains("upm.dmobin.studio"))
                            {
                                // Lấy package name
                                int nameStart = block.IndexOf("\"com.dmobin");
                                int nameEnd = block.IndexOf("\":", nameStart);
                                if (nameStart != -1 && nameEnd != -1)
                                {
                                    string packageName = block.Substring(nameStart + 1, nameEnd - nameStart - 1);
                                    
                                    // Kiểm tra depth
                                    int depthIndex = block.IndexOf("\"depth\":");
                                    bool isDependency = false;
                                    if (depthIndex != -1)
                                    {
                                        string depthStr = block.Substring(depthIndex + 8).Trim();
                                        int commaIndex = depthStr.IndexOf(',');
                                        if (commaIndex != -1)
                                        {
                                            depthStr = depthStr.Substring(0, commaIndex).Trim();
                                        }
                                        
                                        if (int.TryParse(depthStr, out int depth) && depth > 0)
                                        {
                                            isDependency = true;
                                        }
                                    }
                                    
                                    // Lấy version
                                    int versionIndex = block.IndexOf("\"version\":");
                                    if (versionIndex != -1)
                                    {
                                        int versionStart = block.IndexOf('"', versionIndex + 10) + 1;
                                        int versionEnd = block.IndexOf('"', versionStart);
                                        if (versionStart > 0 && versionEnd > versionStart)
                                        {
                                            string version = block.Substring(versionStart, versionEnd - versionStart);
                                            
                                            if (!version.StartsWith("http") && !version.StartsWith("git") && !version.StartsWith("file:"))
                                            {
                                                // Thêm vào installed nếu chưa có (từ manifest)
                                                if (!installedPackages.ContainsKey(packageName))
                                                {
                                                    installedPackages[packageName] = true;
                                                    installedAsDependencies[packageName] = isDependency;
                                                    installedVersions[packageName] = version;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            int directCount = installedAsDependencies.Count(kvp => kvp.Value == false);
            int depCount = installedAsDependencies.Count(kvp => kvp.Value == true);
            Debug.Log($"✅ Đã load {installedPackages.Count} packages ({directCount} trực tiếp, {depCount} dependencies)");
            Repaint();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"⚠️ Không thể đọc manifest/packages-lock: {e.Message}");
        }
    }
    
    private string[] SplitPackageBlocks(string dependenciesBlock)
    {
        List<string> blocks = new List<string>();
        int currentPos = 0;
        int braceLevel = 0;
        int blockStart = 0;
        
        for (int i = 0; i < dependenciesBlock.Length; i++)
        {
            if (dependenciesBlock[i] == '{')
            {
                if (braceLevel == 0)
                {
                    blockStart = i;
                }
                braceLevel++;
            }
            else if (dependenciesBlock[i] == '}')
            {
                braceLevel--;
                if (braceLevel == 0 && i > blockStart)
                {
                    // Tìm tên package trước {
                    int nameEnd = blockStart;
                    while (nameEnd > 0 && dependenciesBlock[nameEnd] != '"')
                    {
                        nameEnd--;
                    }
                    int nameStart = nameEnd - 1;
                    while (nameStart > 0 && dependenciesBlock[nameStart] != '"')
                    {
                        nameStart--;
                    }
                    
                    if (nameStart > 0)
                    {
                        string packageName = dependenciesBlock.Substring(nameStart, nameEnd - nameStart + 1);
                        string content = dependenciesBlock.Substring(blockStart, i - blockStart + 1);
                        blocks.Add(packageName + ": " + content);
                    }
                }
            }
        }
        
        return blocks.ToArray();
    }

    private int FindMatchingBrace(string text, int startIndex)
    {
        int count = 1;
        for (int i = startIndex + 1; i < text.Length; i++)
        {
            if (text[i] == '{')
                count++;
            else if (text[i] == '}')
            {
                count--;
                if (count == 0) return i;
            }
        }

        return -1;
    }

    private void CheckListProgressWrapper()
    {
        EditorApplication.delayCall += () =>
        {
            if (listRequest != null && !listRequest.IsCompleted)
            {
                // Nếu chưa hoàn thành, tiếp tục kiểm tra
                CheckListProgressWrapper();
                return;
            }

            // Request đã hoàn thành hoặc null
            if (listRequest != null && listRequest.Status == StatusCode.Success)
            {
                // Cập nhật thông tin từ Client.List() để có version chính xác
                foreach (var package in listRequest.Result)
                {
                    if (package.name.StartsWith("com.dmobin"))
                    {
                        installedPackages[package.name] = true;
                        installedVersions[package.name] = package.version;
                    }
                }

                Repaint();
            }

            listRequest = null;
        };
    }

    private void InitializeStyles()
    {
        if (stylesInitialized) return;

        packageBoxStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(8, 8, 8, 8),
            margin = new RectOffset(5, 5, 5, 5),
            border = new RectOffset(1, 1, 1, 1),
            normal =
            {
                background = MakeTextureWithBorder(256, 64, new Color(0.2f, 0.2f, 0.2f, 0.8f),
                    new Color(0.4f, 0.4f, 0.4f, 0.5f), 1)
            },
            active =
            {
                background = MakeTextureWithBorder(256, 64, new Color(0.15f, 0.15f, 0.15f, 0.8f),
                    new Color(0.3f, 0.3f, 0.3f, 0.5f), 1)
            }
        };

        titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, wordWrap = false };

        descriptionStyle = new GUIStyle(EditorStyles.wordWrappedLabel) { fontSize = 11 };

        highlightStyle = new GUIStyle(EditorStyles.label)
        {
            normal = { background = MakeTexture(2, 2, new Color(0.65f, 1f, 0.81f, 0.5f)) },
            padding = new RectOffset(2, 2, 0, 0)
        };

        keywordButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 9,
            padding = new RectOffset(4, 4, 2, 2),
            margin = new RectOffset(1, 1, 1, 1),
            normal =
            {
                background = MakeTextureWithBorder(16, 64, new Color(0.3f, 0.3f, 0.3f, 0.8f),
                    new Color(0.5f, 0.5f, 0.5f, 0.5f), 1),
                textColor = Color.white
            }
        };

        stylesInitialized = true;
    }

    private void OnGUI()
    {
        InitializeStyles();

        // Cập nhật animation timer cho loading
        if (isLoading)
        {
            loadingAnimationTimer += Time.deltaTime;
            if (loadingAnimationTimer >= 0.5f) // Đổi dấu chấm mỗi 0.5 giây
            {
                loadingAnimationTimer = 0f;
                loadingDotsCount = (loadingDotsCount + 1) % 4; // 0, 1, 2, 3 dấu chấm
            }
        }

        // Header
        EditorGUILayout.BeginVertical();
        // GUILayout.Label("📦 Dmobin Package Registry", headerStyle);
        // GUILayout.Space(5);

        // Tabs
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        int newSelectedTab = GUILayout.Toolbar(selectedTab, tabNames, GUILayout.Width(400), GUILayout.Height(25));
        if (newSelectedTab != selectedTab)
        {
            selectedTab = newSelectedTab;
            searchText = ""; // Reset search khi đổi tab
            selectedKeywords.Clear(); // Reset keywords khi đổi tab
            selectedAuthors.Clear(); // Reset authors khi đổi tab
            Repaint();
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(5);

        // Toolbar
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        // Search box
        GUILayout.Label("Search", GUILayout.Width(45));
        string newSearchText =
            EditorGUILayout.TextField(searchText, EditorStyles.toolbarSearchField, GUILayout.ExpandWidth(true));
        if (newSearchText != searchText)
        {
            searchText = newSearchText;
            Repaint();
        }

        // Clear search button
        if (!string.IsNullOrEmpty(searchText))
        {
            if (GUILayout.Button(new GUIContent("✖", "Xóa text tìm kiếm"), EditorStyles.toolbarButton,
                    GUILayout.Width(20)))
            {
                searchText = string.Empty;
                GUI.FocusControl(null); // Unfocus text field để clear ngay lập tức
                Repaint();
            }
        }

        if (GUILayout.Button(new GUIContent("↺", "Làm mới danh sách packages từ registry"), EditorStyles.toolbarButton,
                GUILayout.Width(20)))
        {
            LoadPackages();
        }

        // Filter toggle button
        string filterButtonText = showFilters ? "Filter 🔽" : "Filter 🔼";
        string filterTooltip = showFilters ? "Ẩn bộ lọc keywords và authors" : "Hiện bộ lọc keywords và authors";
        if (GUILayout.Button(new GUIContent(filterButtonText, filterTooltip), EditorStyles.toolbarButton,
                GUILayout.Width(60)))
        {
            showFilters = !showFilters;
            Repaint();
        }

        // Context menu button
        GUIStyle menuButtonStyle = new GUIStyle(EditorStyles.toolbarButton)
        {
            fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
        };

        if (GUILayout.Button(new GUIContent("⋮", "More options"), menuButtonStyle, GUILayout.Width(20)))
        {
            ShowMainContextMenu();
        }

        EditorGUILayout.EndHorizontal();

        // Status bar
        if (isLoading)
        {
            string dots = new string('.', loadingDotsCount);
            string loadingText = $"⏳ Đang tải danh sách packages{dots}";
            EditorGUILayout.HelpBox(loadingText, MessageType.Info);
        }
        else if (!string.IsNullOrEmpty(errorMessage))
        {
            EditorGUILayout.HelpBox($"❌ Lỗi: {errorMessage}", MessageType.Error);
            if (GUILayout.Button(new GUIContent("Thử lại", "Thử tải lại danh sách packages"), GUILayout.Width(100)))
            {
                LoadPackages();
            }
        }
        else if (packages.Count > 0 || selectedTab == 1)
        {
            var filteredPackages = GetFilteredPackagesForCurrentTab();
            EditorGUILayout.LabelField($"📊 Tìm thấy {filteredPackages.Count} packages", EditorStyles.miniLabel);

            // Keywords panel
            if (showFilters && selectedTab == 0 && allKeywords.Count > 0)
            {
                GUILayout.Space(5);
                EditorGUILayout.BeginVertical(GUI.skin.box);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("🏷️ Keywords:", EditorStyles.boldLabel, GUILayout.Width(80));

                if (selectedKeywords.Count > 0)
                {
                    if (GUILayout.Button("✖ Clear All", GUILayout.Width(80)))
                    {
                        selectedKeywords.Clear();
                        Repaint();
                    }

                    // Hiển thị các keywords được chọn theo chiều ngang trong cùng một hàng
                    if (selectedKeywords.Count > 0)
                    {
                        GUIStyle yellowBoldLabel = new GUIStyle(EditorStyles.whiteMiniLabel);
                        yellowBoldLabel.normal.textColor = new Color(1f, 0.8f, 0.1f); // Màu vàng
                        yellowBoldLabel.fontStyle = FontStyle.Bold;

                        // Tạo chuỗi keywords ngăn cách bởi dấu phẩy (không có khoảng trắng)
                        string keywordsText = string.Join(", ", selectedKeywords);

                        // Hiển thị tất cả keywords trong một Label duy nhất
                        GUILayout.Label(keywordsText, yellowBoldLabel);
                    }
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(3);

                // Keywords buttons
                EditorGUILayout.BeginHorizontal();
                int buttonCount = 0;
                int maxButtonsPerRow =
                    Mathf.FloorToInt((position.width - 30) /
                                     70); // Tính số button tối đa trên một hàng (giảm kích thước button)

                foreach (var keyword in allKeywords.Take(20)) // Chỉ hiển thị 20 keywords phổ biến nhất
                {
                    int count = keywordCounts[keyword];
                    bool isSelected = selectedKeywords.Contains(keyword);

                    // Tạo style cho button được chọn
                    GUIStyle buttonStyle = keywordButtonStyle;

                    // Tính kích thước button dựa trên độ dài text
                    string buttonText = $"{keyword} ({count})";
                    float textWidth = buttonStyle.CalcSize(new GUIContent(buttonText)).x;
                    float buttonWidth = Mathf.Max(textWidth + 4, 50); // Tối thiểu 50px, cộng thêm 8px padding

                    // Đặt màu nền cho button được chọn
                    Color originalBgColor = GUI.backgroundColor;
                    if (isSelected)
                    {
                        GUI.backgroundColor = new Color(0.8f, 0.79f, 0.16f);
                    }

                    bool buttonClicked = GUILayout.Button(buttonText, buttonStyle, GUILayout.Width(buttonWidth));

                    // Khôi phục màu nền gốc
                    GUI.backgroundColor = originalBgColor;

                    if (buttonClicked)
                    {
                        if (selectedKeywords.Contains(keyword))
                        {
                            // Nếu keyword đã được chọn thì bỏ chọn
                            selectedKeywords.Remove(keyword);
                        }
                        else
                        {
                            // Thêm keyword vào danh sách được chọn
                            selectedKeywords.Add(keyword);
                        }

                        Repaint();
                    }

                    buttonCount++;
                    if (buttonCount >= maxButtonsPerRow)
                    {
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        buttonCount = 0;
                    }
                }

                EditorGUILayout.EndHorizontal();

                // Authors section (nếu có authors)
                if (showFilters && allAuthors.Count > 0)
                {
                    GUILayout.Space(5);

                    EditorGUILayout.BeginVertical(GUI.skin.box);

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("🦸 Authors:", EditorStyles.boldLabel, GUILayout.Width(80));

                    if (selectedAuthors.Count > 0)
                    {
                        if (GUILayout.Button("✖ Clear All", GUILayout.Width(80)))
                        {
                            selectedAuthors.Clear();
                            Repaint();
                        }

                        // Hiển thị các authors được chọn theo chiều ngang trong cùng một hàng
                        if (selectedAuthors.Count > 0)
                        {
                            GUIStyle yellowBoldLabel = new GUIStyle(EditorStyles.whiteMiniLabel);
                            yellowBoldLabel.normal.textColor = new Color(1f, 0.8f, 0.1f); // Màu vàng
                            yellowBoldLabel.fontStyle = FontStyle.Bold;

                            // Tạo chuỗi authors ngăn cách bởi dấu phẩy (không có khoảng trắng)
                            string authorsText = string.Join(",", selectedAuthors);

                            // Hiển thị tất cả authors trong một Label duy nhất
                            GUILayout.Label(authorsText, yellowBoldLabel);
                        }
                    }

                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();

                    // Authors buttons
                    EditorGUILayout.BeginHorizontal();
                    int authorButtonCount = 0;
                    int maxAuthorButtonsPerRow = Mathf.FloorToInt((position.width - 30) / 100);

                    foreach (var author in allAuthors.Take(20)) // Chỉ hiển thị 20 authors phổ biến nhất
                    {
                        int count = authorCounts[author];
                        bool isAuthorSelected = selectedAuthors.Contains(author);

                        // Tạo style cho button được chọn
                        GUIStyle authorButtonStyle = keywordButtonStyle;

                        // Tính kích thước button dựa trên độ dài text
                        string authorButtonText = $"{author} ({count})";
                        float authorTextWidth = authorButtonStyle.CalcSize(new GUIContent(authorButtonText)).x;
                        float authorButtonWidth = Mathf.Max(authorTextWidth + 4, 50); // Tối thiểu 50px

                        // Đặt màu nền cho button được chọn
                        Color originalAuthorBgColor = GUI.backgroundColor;
                        if (isAuthorSelected)
                        {
                            GUI.backgroundColor = new Color(0.8f, 0.79f, 0.16f);
                        }

                        bool authorButtonClicked = GUILayout.Button(authorButtonText, authorButtonStyle,
                            GUILayout.Width(authorButtonWidth));

                        // Khôi phục màu nền gốc
                        GUI.backgroundColor = originalAuthorBgColor;

                        if (authorButtonClicked)
                        {
                            if (selectedAuthors.Contains(author))
                            {
                                // Nếu author đã được chọn thì bỏ chọn
                                selectedAuthors.Remove(author);
                            }
                            else
                            {
                                // Thêm author vào danh sách được chọn
                                selectedAuthors.Add(author);
                            }

                            Repaint();
                        }

                        authorButtonCount++;
                        if (authorButtonCount >= maxAuthorButtonsPerRow)
                        {
                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.BeginHorizontal();
                            authorButtonCount = 0;
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                }

                EditorGUILayout.EndVertical();
            }
        }

        GUILayout.Space(5);

        // Package list
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        if (selectedTab == 0) // All Packages tab
        {
            if (packages.Count > 0)
            {
                var filteredPackages = GetFilteredPackagesForCurrentTab();

                foreach (var package in filteredPackages)
                {
                    DrawPackageItem(package, false);
                }

                if (filteredPackages.Count == 0)
                {
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.HelpBox("🔍 Không tìm thấy package nào phù hợp với tìm kiếm của bạn.",
                        MessageType.Info);
                    GUILayout.FlexibleSpace();
                }
            }
            else if (!isLoading && string.IsNullOrEmpty(errorMessage))
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.HelpBox("📭 Chưa có packages nào được tải.\nNhấn 'Làm mới' để tải danh sách.",
                    MessageType.Info);
                GUILayout.FlexibleSpace();
            }
        }
        else // Installed tab
        {
            var installedPackagesList = GetFilteredPackagesForCurrentTab();

            if (installedPackagesList.Count > 0)
            {
                foreach (var package in installedPackagesList)
                {
                    DrawPackageItem(package, true);
                }
            }
            else if (!isLoading)
            {
                GUILayout.FlexibleSpace();
                if (string.IsNullOrEmpty(searchText))
                {
                    EditorGUILayout.HelpBox("📭 Chưa có package nào từ Dmobin registry được cài đặt.",
                        MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("🔍 Không tìm thấy package nào phù hợp với tìm kiếm của bạn.",
                        MessageType.Info);
                }

                GUILayout.FlexibleSpace();
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private List<PackageInfo> GetFilteredPackages()
    {
        var result = packages.AsEnumerable();

        // Lọc theo nhiều keywords nếu có
        if (selectedKeywords.Count > 0)
        {
            result = result.Where(p => p.keywords != null &&
                selectedKeywords.Any(selectedKeyword => p.keywords.Contains(selectedKeyword)));
        }

        // Lọc theo nhiều authors nếu có
        if (selectedAuthors.Count > 0)
        {
            result = result.Where(p => selectedAuthors.Contains(p.authorName));
        }

        // Lọc theo search text nếu có (kết hợp với keyword và author filter nếu có)
        if (!string.IsNullOrEmpty(searchText))
        {
            string search = searchText.ToLower();
            result = result.Where(p =>
                p.name.ToLower().Contains(search) || p.displayName.ToLower().Contains(search) ||
                p.description.ToLower().Contains(search) || p.authorName.ToLower().Contains(search) ||
                (p.keywords != null && p.keywords.Any(k => k.ToLower().Contains(search))));
        }

        return result.ToList();
    }

    private List<PackageInfo> GetFilteredPackagesForCurrentTab()
    {
        if (selectedTab == 0) // All Packages
        {
            return GetFilteredPackages();
        }
        else // Installed
        {
            // Lấy packages đã cài từ registry Dmobin
            var installedList = packages.Where(p => installedPackages.ContainsKey(p.name)).ToList();

            // KHÔNG override package.version vì chúng ta cần giữ registry version để so sánh update
            // Installed version đã được lưu trong installedVersions dictionary

            if (string.IsNullOrEmpty(searchText)) return installedList;

            string search = searchText.ToLower();
            return installedList.Where(p =>
                    p.name.ToLower().Contains(search) || p.displayName.ToLower().Contains(search) ||
                    p.description.ToLower().Contains(search) || p.authorName.ToLower().Contains(search))
                .ToList();
        }
    }

    private void DrawPackageItem(PackageInfo package, bool isInstalledTab)
    {
        // Kiểm tra trạng thái của package
        bool isPackageInstalled = installedPackages.ContainsKey(package.name) && installedPackages[package.name];
        bool isPackageSelected = selectedPackageName == package.name;

        // Kiểm tra xem có phiên bản mới hơn không
        // Lấy version đã cài từ installedVersions
        string installedVersion = installedVersions.ContainsKey(package.name) ? installedVersions[package.name] : null;

        // Lấy version mới nhất từ registry
        // Nếu đang ở tab Installed, package.version có thể đã bị override thành installedVersion
        // nên ta cần lấy lại từ danh sách packages gốc
        var registryPackage = packages.FirstOrDefault(p => p.name == package.name);
        string registryVersion = registryPackage != null ? registryPackage.version : package.version;

        // So sánh: nếu đã cài và registry version mới hơn installed version
        bool hasUpdateAvailable = isPackageInstalled && !string.IsNullOrEmpty(installedVersion) &&
                                  !string.IsNullOrEmpty(registryVersion) && installedVersion != registryVersion &&
                                  IsNewerVersion(registryVersion, installedVersion);

        // Xác định style phù hợp
        GUIStyle currentBoxStyle = packageBoxStyle;

        // Tạo một rect để phát hiện click
        Rect packageRect = EditorGUILayout.BeginVertical(currentBoxStyle);

        // Đảm bảo package luôn có màu nền đúng theo trạng thái
        if (hasUpdateAvailable)
        {
            if (isPackageSelected)
            {
                // Vẽ background màu vàng đậm hơn cho selected package có update
                EditorGUI.DrawRect(packageRect, new Color(1f, 0.85f, 0.1f, 0.6f));
            }
            else
            {
                // Vẽ background màu vàng cho package có phiên bản mới
                EditorGUI.DrawRect(packageRect, new Color(1f, 0.92f, 0.2f, 0.4f));
            }
        }
        else if (isPackageSelected && isPackageInstalled)
        {
            // Vẽ background màu xanh lá cây đậm hơn cho selected package đã được cài đặt
            EditorGUI.DrawRect(packageRect, new Color(0.39f, 0.7f, 0.19f, 0.66f));
        }
        else if (isPackageSelected)
        {
            // Vẽ background màu xanh dương cho selected package chưa được cài đặt
            EditorGUI.DrawRect(packageRect, new Color(0.3f, 0.6f, 1f, 0.3f));
        }
        else if (isPackageInstalled)
        {
            // Vẽ background màu xanh lá cho installed package
            EditorGUI.DrawRect(packageRect, new Color(0.2f, 0.8f, 0.4f, 0.3f));
        }

        if (Event.current.type == EventType.MouseDown && packageRect.Contains(Event.current.mousePosition))
        {
            if (selectedPackageName != package.name) // Chỉ chọn nếu là package khác
            {
                selectedPackageName = package.name;
                Event.current.Use();
                Repaint();
            }
            // Nếu là package đang được chọn thì không làm gì
        }

        // Title and version
        EditorGUILayout.BeginHorizontal();
        DrawTextWithHighlight($"📦 {package.displayName}", titleStyle, searchText);
        GUILayout.FlexibleSpace();

        if (isPackageSelected)
        {
            // Hiển thị trạng thái selected với màu phù hợp
            GUIStyle selectedStyle = new GUIStyle(EditorStyles.miniLabel);
            selectedStyle.fontStyle = FontStyle.Bold;

            // Hiển thị installed version nếu đã cài, registry version nếu chưa cài
            string displayVersion = isPackageInstalled && !string.IsNullOrEmpty(installedVersion)
                ? installedVersion
                : package.version;

            // Kiểm tra xem package có được cài bởi dependency không
            bool isDependency = installedAsDependencies.ContainsKey(package.name) && installedAsDependencies[package.name];
            string icon = isDependency ? "🔗" : "✅";
            string statusText = $"{icon} v{displayVersion}";

            if (hasUpdateAvailable && isPackageSelected)
            {
                selectedStyle.normal.textColor = new Color(0.1f, 0.7f, 0.2f);
            }
            else if (isPackageSelected && isPackageInstalled)
            {
                // Màu xanh lá cây đậm hơn cho selected package đã được cài đặt
                selectedStyle.normal.textColor = new Color(0.1f, 0.7f, 0.2f);
            }
            else if (isPackageInstalled)
            {
                // Màu xanh lá cây cho installed package
                selectedStyle.normal.textColor = new Color(0.2f, 0.8f, 0.4f);
            }
            else
            {
                // Màu xanh dương cho selected package chưa được cài đặt (mặc định)
                selectedStyle.normal.textColor = new Color(0.34f, 0.56f, 1f);
            }

            GUILayout.Label(statusText, selectedStyle);
        }
        else if (isPackageInstalled)
        {
            // Hiển thị trạng thái installed với icon và màu xanh lá cây
            GUIStyle installedStyle = new GUIStyle(EditorStyles.miniLabel);
            installedStyle.normal.textColor = new Color(0.2f, 0.8f, 0.4f); // Màu xanh lá cây
            installedStyle.fontStyle = FontStyle.Bold;

            // Hiển thị installed version, và registry version nếu có update
            string displayVersion = !string.IsNullOrEmpty(installedVersion) ? installedVersion : package.version;

            // Kiểm tra xem package có được cài bởi dependency không
            bool isDependency = installedAsDependencies.ContainsKey(package.name) && installedAsDependencies[package.name];
            string icon = isDependency ? "🔗" : "✅";
            
            GUILayout.Label($"{icon} v{displayVersion}", installedStyle);
        }
        else
        {
            GUILayout.Label($"v{package.version}", EditorStyles.miniLabel);
        }

        EditorGUILayout.EndHorizontal();

        // Package name with Copy, Download and Install buttons
        EditorGUILayout.BeginHorizontal();
        DrawTextWithHighlightForID($"ID: {package.name}", EditorStyles.miniLabel, searchText);

        GUILayout.FlexibleSpace();

        // Copy button
        if (GUILayout.Button(new GUIContent("📋", "Copy Package ID vào clipboard"), GUILayout.Width(20)))
        {
            EditorGUIUtility.systemCopyBuffer = package.name;
            Debug.Log($"✅ Đã copy package ID: {package.name}");
        }

        // Download button (chỉ hiện khi có tarball URL)
        if (!string.IsNullOrEmpty(package.tarballUrl))
        {
            if (GUILayout.Button(new GUIContent("⬇️", "Download package"), GUILayout.Width(20)))
            {
                Application.OpenURL(package.tarballUrl);
            }
        }

        // Ping location button (chỉ hiện cho packages đã cài đặt)
        if (installedPackages.ContainsKey(package.name) && installedPackages[package.name])
        {
            if (GUILayout.Button(new GUIContent("🔗", "Tìm vị trí package trong Project window"), GUILayout.Width(20)))
            {
                PingPackageLocation(package.name);
            }

            // Import button (chỉ hiện khi có package trong folder)
            var packagesInFolder = GetPackagesInFolder(package.name);
            if (packagesInFolder != null && packagesInFolder.Length > 0)
            {
                if (GUILayout.Button(new GUIContent("📁", "Import Unity package base"), GUILayout.Width(20)))
                {
                    ImportUnityPackage(packagesInFolder[0]);
                }
            }
        }

        // Install/Update/Remove buttons (chỉ hiện trong tab All Packages)
        if (!isInstalledTab)
        {
            bool isInstalling = installingPackages.ContainsKey(package.name) && installingPackages[package.name];
            bool isInstalled = installedPackages.ContainsKey(package.name) && installedPackages[package.name];

            // Hiển thị button Update nếu có phiên bản mới
            if (isInstalled && hasUpdateAvailable)
            {
                GUI.enabled = !isInstalling;
                if (GUILayout.Button(
                        new GUIContent($"⬆️ Update to {registryVersion}",
                            $"Cập nhật package lên phiên bản {registryVersion}"), GUILayout.Width(150)))
                {
                    InstallPackage(package.name, registryVersion);
                }

                GUI.enabled = true;
            }
            else
            {
                GUI.enabled = !isInstalling && !isInstalled;

                // Kiểm tra xem package có được cài bởi dependency không
                bool isDependency = installedAsDependencies.ContainsKey(package.name) && installedAsDependencies[package.name];
                string installedIcon = isDependency ? "🔗" : "✅";
                string buttonText = isInstalled ? $"{installedIcon} Installed" : (isInstalling ? "⏳ Installing..." : "📦 Install");
                
                if (GUILayout.Button(new GUIContent(buttonText, $"Cài đặt package {package.name}"),
                        GUILayout.Width(120)))
            {
                InstallPackage(package.name, package.version);
            }

            GUI.enabled = true;
            }
        }
        else
        {
            // Trong tab Installed, hiển thị button Remove và Update
            if (GUILayout.Button(new GUIContent("🗑️ Remove", "Xóa package khỏi dự án"), GUILayout.Width(120)))
            {
                if (EditorUtility.DisplayDialog("Xác nhận xóa",
                        $"Bạn có chắc muốn xóa package '{package.displayName}'?", "Xóa", "Hủy"))
                {
                    RemovePackage(package.name);
                }
            }

            // Button Update (nếu có version mới hơn)
            if (hasUpdateAvailable)
            {
                if (GUILayout.Button(
                        new GUIContent($"⬆️ Update to {registryVersion}",
                            $"Cập nhật package lên phiên bản {registryVersion}"), GUILayout.Width(150)))
                {
                    InstallPackage(package.name, registryVersion);
                }
            }
        }

        EditorGUILayout.EndHorizontal();

        // Author và Unity version
        EditorGUILayout.BeginHorizontal();
        if (!string.IsNullOrEmpty(package.authorName))
        {
            DrawTextWithHighlight($"🦸 {package.authorName}", EditorStyles.miniLabel, searchText);
        }

        if (!string.IsNullOrEmpty(package.unityVersion))
        {
            GUILayout.Label($"⚙️ Unity {package.unityVersion}+", EditorStyles.miniLabel);
        }

        EditorGUILayout.EndHorizontal();

        // Description
        if (!string.IsNullOrEmpty(package.description))
        {
            GUILayout.Space(5);
            string shortDescription = package.description.Length > 200
                ? package.description.Substring(0, 200) + "..."
                : package.description;
            DrawTextWithHighlight(shortDescription, descriptionStyle, searchText);
        }

        // Keywords
        if (package.keywords != null && package.keywords.Count > 0)
        {
            GUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("🏷️", GUILayout.Width(20));
            GUILayout.Label(string.Join(", ", package.keywords), EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
        GUILayout.Space(1);
    }

    private void ShowMainContextMenu()
    {
        GenericMenu menu = new GenericMenu();
        
        menu.AddItem(new GUIContent("Scoped Registries Initialization"), false, () => { ScopedRegistriesInitialization();});
        menu.AddItem(new GUIContent("Check Define Symbol"), false, () => { CheckDefineSymbols(); });
        
        // Show menu at mouse position
        menu.ShowAsContext();
    }

    private bool IsNewerVersion(string candidateVersion, string currentVersion)
    {
        if (string.IsNullOrEmpty(candidateVersion) || string.IsNullOrEmpty(currentVersion)) return false;

        // Chuẩn hóa version string (loại bỏ metadata như -preview, +build)
        string Normalize(string v)
        {
            int dashIndex = v.IndexOf('-');
            if (dashIndex >= 0) v = v.Substring(0, dashIndex);
            int plusIndex = v.IndexOf('+');
            if (plusIndex >= 0) v = v.Substring(0, plusIndex);
            return v.Trim();
        }

        // Parse version thành mảng số
        int[] Parse(string v)
        {
            var parts = Normalize(v).Split('.');
            int[] nums = new int[Math.Max(3, parts.Length)];
            for (int i = 0; i < nums.Length; i++)
            {
                if (i < parts.Length && int.TryParse(parts[i], out int n))
                    nums[i] = n;
                else
                    nums[i] = 0;
            }

            return nums;
        }

        var candidateNums = Parse(candidateVersion);
        var currentNums = Parse(currentVersion);

        // So sánh từng phần của version
        for (int i = 0; i < Math.Max(candidateNums.Length, currentNums.Length); i++)
        {
            int candidate = i < candidateNums.Length ? candidateNums[i] : 0;
            int current = i < currentNums.Length ? currentNums[i] : 0;

            if (candidate > current) return true;
            if (candidate < current) return false;
        }

        return false; // Nếu bằng nhau thì không phải newer
    }

    /// <summary>
    /// Khởi tạo Dmobin UPM Scoped Registry
    /// </summary>
    private void ScopedRegistriesInitialization()
    {
        Debug.Log("🔧 Bắt đầu khởi tạo Scoped Registries...");

        try
        {
            // Đường dẫn đến manifest.json
            string manifestPath = "Packages/manifest.json";

            // Kiểm tra file có tồn tại không
            if (!System.IO.File.Exists(manifestPath))
            {
                Debug.LogError("❌ Không tìm thấy manifest.json");
                return;
            }

            // Đọc nội dung manifest.json
            string jsonText = System.IO.File.ReadAllText(manifestPath);
            Debug.Log("📖 Đã đọc manifest.json thành công");

            // Parse JSON đơn giản để kiểm tra registry đã tồn tại chưa
            if (jsonText.Contains("\"name\": \"Dmobin UPM\""))
            {
                Debug.Log("✅ Dmobin UPM registry đã tồn tại");
                EditorUtility.DisplayDialog("Thông báo",
                    "Dmobin UPM registry đã được cấu hình sẵn.\n\nRegistry: https://upm.dmobin.studio\nScopes: com.dmobin, com.google, com.applovin",
                    "OK");
                return;
            }

            // Tạo scoped registry JSON
            string dmobinRegistry = @"    {
      ""name"": ""Dmobin UPM"",
      ""url"": ""https://upm.dmobin.studio"",
      ""scopes"": [
        ""com.dmobin"",
        ""com.google"",
        ""com.applovin""
      ]
    }";

            // Kiểm tra xem có scopedRegistries chưa
            if (jsonText.Contains("\"scopedRegistries\""))
            {
                // Tìm vị trí của scopedRegistries array
                int scopedStart = jsonText.IndexOf("\"scopedRegistries\"");
                int arrayStart = jsonText.IndexOf("[", scopedStart);

                if (arrayStart != -1)
                {
                    // Insert registry vào đầu array (sau [)
                    string before = jsonText.Substring(0, arrayStart + 1);
                    string after = jsonText.Substring(arrayStart + 1);
                    jsonText = before + "\n" + dmobinRegistry + ",\n" + after;
                }
            }
            else
            {
                // Tạo mới scopedRegistries array - tìm vị trí cuối của dependencies
                int dependenciesStart = jsonText.IndexOf("\"dependencies\": {");
                if (dependenciesStart != -1)
                {
                    int braceStart = jsonText.IndexOf("{", dependenciesStart);
                    int braceEnd = FindMatchingBrace(jsonText, braceStart);
                    if (braceEnd != -1)
                    {
                        // Insert scopedRegistries sau dependencies
                        string before = jsonText.Substring(0, braceEnd + 1);
                        string after = jsonText.Substring(braceEnd + 1);
                        jsonText = before + ",\n  \"scopedRegistries\": [\n" + dmobinRegistry + "\n  ]" + after;
                    }
                }
            }

            Debug.Log("➕ Đã thêm Dmobin UPM registry");

            // Lưu file
            System.IO.File.WriteAllText(manifestPath, jsonText);
            Debug.Log("💾 Đã lưu manifest.json");

            // Thông báo thành công
            string successMessage = "Đã thêm thành công Dmobin UPM registry!\n\n" +
                                  "Registry: https://upm.dmobin.studio\n" +
                                  "Scopes: com.dmobin, com.google, com.applovin";

            EditorUtility.DisplayDialog("Thành công!", successMessage, "OK");

            // Refresh Package Manager
            UnityEditor.PackageManager.Client.Resolve();
            AssetDatabase.Refresh();
            Debug.Log("✅ Hoàn thành khởi tạo Scoped Registries");

        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Lỗi khi khởi tạo scoped registries: {e.Message}");

            string errorMessage = $"Không thể thêm Dmobin UPM registry:\n\n{e.Message}\n\n" +
                                "Hãy đảm bảo:\n" +
                                "1. Unity Editor có quyền ghi file\n" +
                                "2. File manifest.json không bị khóa\n" +
                                "3. Restart Unity Editor và thử lại";

            EditorUtility.DisplayDialog("Lỗi khởi tạo",
                errorMessage,
                "OK");
        }
    }

    
    private void CheckDefineSymbols()
    {
        // Lấy danh sách các define symbol hiện tại từ Project Settings
        string currentDefines;

#if UNITY_6000_0_OR_NEWER
        var targetGroup =
            UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
        currentDefines = PlayerSettings.GetScriptingDefineSymbols(targetGroup);
#else
            var targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            currentDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
#endif

        List<string> currentDefinesList =
            currentDefines.Split(new char[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries).ToList();

        // Lấy danh sách tất cả define symbol từ GDKDefineSymbolsName
        List<string> allDefineSymbols = GDKDefineSymbolsName.AllDefineSymbols.ToList();

        // Tạo ánh xạ giữa package và define symbol tương ứng
        Dictionary<string, List<string>> packageToDefines = new Dictionary<string, List<string>>();

        // Firebase và các define liên quan
        packageToDefines["com.dmobin.sdk.analytics-platform.firebase.analytics"] =
            new List<string> { "GDK_USE_FIREBASE", "GDK_USE_FIREBASE_ANALYTICS" };
        packageToDefines["com.dmobin.sdk.analytics-platform.firebase.crashlytics"] =
            new List<string> { "GDK_USE_FIREBASE", "GDK_USE_FIREBASE_CRASHLYTICS" };
        packageToDefines["com.dmobin.sdk.analytics-platform.firebase.messaging"] =
            new List<string> { "GDK_USE_FIREBASE", "GDK_USE_FIREBASE_MESSAGING" };
        packageToDefines["com.dmobin.sdk.analytics-platform.firebase.remote-config"] =
            new List<string> { "GDK_USE_FIREBASE", "GDK_USE_FIREBASE_REMOTE_CONFIG" };

        // MMP
        packageToDefines["com.dmobin.sdk.analytics-platform.mmp.adjust"] = new List<string> { "GDK_USE_ADJUST" };
        packageToDefines["com.dmobin.sdk.analytics-platform.mmp.appsflyer"] = new List<string> { "GDK_USE_APPSFLYER" };

        // Ads
        packageToDefines["com.dmobin.sdk.mediation.admob"] = new List<string> { "GDK_USE_ADMOB" };
        packageToDefines["com.dmobin.sdk.mediation.levelplay"] =
            new List<string> { "GDK_USE_LEVEL_PLAY", "LEVELPLAY_DEPENDENCIES_INSTALLED" };
        packageToDefines["com.dmobin.sdk.mediation.max"] = new List<string> { "GDK_USE_MAX" };
        packageToDefines["com.dmobin.sdk.nativeads"] = new List<string> { "GDK_USE_NATIVE_ADMOB" };
        packageToDefines["com.dmobin.sdk.mediation.yandex"] = new List<string> { "GDK_USE_YANDEX" };

        // IAP
        packageToDefines["com.dmobin.sdk.inapppurchase"] = new List<string> { "GDK_USE_IAP" };

        // Tools
        packageToDefines["com.dmobin.pubscale.tool"] = new List<string> { "GDK_USE_PUBSCALE" };
        packageToDefines["com.dmobin.sdk.spine2d"] = new List<string> { "GDK_USE_SPINE" };

        // Sao chép installedPackages để không ảnh hưởng đến bản gốc
        Dictionary<string, bool> packageInstallStatus = new Dictionary<string, bool>(installedPackages);

        // Kiểm tra thêm package được cài đặt thông qua dependency
        foreach (var packageId in packageToDefines.Keys)
        {
            // Nếu chưa được đánh dấu là đã cài đặt, kiểm tra thư mục
            if (!packageInstallStatus.ContainsKey(packageId) || !packageInstallStatus[packageId])
            {
                string packagePath = Path.Combine("Packages", packageId);
                if (Directory.Exists(packagePath))
                {
                    // Đánh dấu package tồn tại
                    packageInstallStatus[packageId] = true;
                }
            }
        }

        // Kiểm tra từng package cài đặt
        List<string> missingDefines = new List<string>();
        List<string> redundantDefines = new List<string>();

        // Kiểm tra các package đã cài đặt và define symbol tương ứng
        foreach (var packageEntry in packageInstallStatus)
        {
            string packageId = packageEntry.Key;
            bool isInstalled = packageEntry.Value;

            if (isInstalled && packageToDefines.ContainsKey(packageId))
            {
                List<string> requiredDefines = packageToDefines[packageId];

                foreach (var define in requiredDefines)
                {
                    if (!currentDefinesList.Contains(define))
                    {
                        missingDefines.Add($"{define} (cần cho {packageId})");
                    }
                }
            }
        }

        // Kiểm tra các define symbol thừa
        foreach (var define in allDefineSymbols)
        {
            bool isNeeded = false;

            // Kiểm tra xem define này có cần cho package nào đã cài đặt không
            foreach (var packageEntry in packageInstallStatus)
            {
                string packageId = packageEntry.Key;
                bool isInstalled = packageEntry.Value;

                if (isInstalled && packageToDefines.ContainsKey(packageId) &&
                    packageToDefines[packageId].Contains(define))
                {
                    isNeeded = true;
                    break;
                }
            }

            // Nếu define có trong project settings nhưng không cần cho package nào đã cài đặt
            if (!isNeeded && currentDefinesList.Contains(define))
            {
                redundantDefines.Add(define);
            }
        }

        // Hiển thị kết quả
        string message = "Kết quả kiểm tra Define Symbol:\n\n";

        if (missingDefines.Count > 0)
        {
            message += "Define Symbol thiếu:\n";
            foreach (var define in missingDefines)
            {
                message += "- " + define + "\n";
            }

            message += "\n";
        }

        if (redundantDefines.Count > 0)
        {
            message += "Define Symbol thừa:\n";
            foreach (var define in redundantDefines)
            {
                message += "- " + define + "\n";
            }

            message += "\n";
        }

        if (missingDefines.Count == 0 && redundantDefines.Count == 0)
        {
            message += "Tất cả Define Symbol đã được cài đặt đúng.\n";
            EditorUtility.DisplayDialog("Kiểm tra Define Symbol", message, "OK");
        }
        else
        {
            message += "Bạn có muốn sửa các vấn đề với Define Symbol không?";

            if (EditorUtility.DisplayDialog("Kiểm tra Define Symbol", message, "Sửa", "Bỏ qua"))
            {
                FixDefineSymbols(missingDefines, redundantDefines);
            }
        }
    }
    
    private void FixDefineSymbols(List<string> missingDefines, List<string> redundantDefines)
    {
        string currentDefines;

#if UNITY_6000_0_OR_NEWER
        var targetGroup = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
        currentDefines = PlayerSettings.GetScriptingDefineSymbols(targetGroup);
#else
            var targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            currentDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
#endif

        // Split the current defines into a list
        List<string> currentDefinesList = currentDefines.Split(new char[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries).ToList();

        // Add missing defines
        foreach (var define in missingDefines)
        {
            string cleanDefine = define.Split(' ')[0]; // Get the part before any space
            if (!currentDefinesList.Contains(cleanDefine))
            {
                currentDefinesList.Add(cleanDefine);
            }
        }

        // Remove redundant defines
        foreach (var define in redundantDefines)
        {
            currentDefinesList.Remove(define);
        }

        // Join the defines back into a string
        string newDefines = string.Join(";", currentDefinesList);

        // Set the defines using the appropriate API
#if UNITY_6000_0_OR_NEWER
        PlayerSettings.SetScriptingDefineSymbols(targetGroup, newDefines);
#else
            PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, newDefines);
#endif

        // Show a dialog to inform the user
        EditorUtility.DisplayDialog("Define Symbol", "Đã cập nhật Define Symbol thành công.", "OK");
    }


    private void LoadPackages()
    {
        isLoading = true;
        errorMessage = "";
        loadingDotsCount = 0; // Reset animation
        loadingAnimationTimer = 0f;
        Repaint();
        FetchPackagesAsync();
    }

    private async void FetchPackagesAsync()
    {
        UnityWebRequest request = UnityWebRequest.Get(PACKAGES_API_URL);

        var operation = request.SendWebRequest();

        // Đợi request hoàn thành mà không block main thread
        while (!operation.isDone)
        {
            await System.Threading.Tasks.Task.Delay(50);
            if (this == null) // Window đã bị đóng
            {
                request.Dispose();
                return;
            }
        }

        if (request.result == UnityWebRequest.Result.Success)
        {
            try
            {
                string jsonText = request.downloadHandler.text;
                ParsePackages(jsonText);
                errorMessage = "";
                Debug.Log($"✅ Đã tải thành công {packages.Count} packages từ Dmobin registry!");
            }
            catch (Exception e)
            {
                errorMessage = $"Không thể parse JSON: {e.Message}";
                Debug.LogError($"❌ Lỗi khi parse packages: {e.Message}");
            }
        }
        else
        {
            errorMessage = $"Không thể tải dữ liệu: {request.error}";
            Debug.LogError($"❌ Lỗi khi tải packages: {request.error}");
        }

        isLoading = false;
        loadingDotsCount = 0; // Dừng animation
        loadingAnimationTimer = 0f;
        request.Dispose();

        if (this != null) // Đảm bảo window vẫn còn tồn tại
        {
            Repaint();
        }
    }

    private void ParsePackages(string jsonText)
    {
        packages.Clear();

        // Parse JSON array
        PackageDataArray dataArray = JsonUtility.FromJson<PackageDataArray>("{\"items\":" + jsonText + "}");

        if (dataArray != null && dataArray.items != null)
        {
            foreach (var data in dataArray.items)
            {
                PackageInfo info = new PackageInfo
                {
                    name = data.name ?? "",
                    version = data.version ?? "",
                    displayName = data.displayName ?? data.name ?? "",
                    description = data.description ?? "",
                    unityVersion = data.unity ?? "",
                    authorName = data.author?.name ?? "",
                    keywords = data.keywords ?? new List<string>(),
                    tarballUrl = data.dist?.tarball ?? ""
                };

                packages.Add(info);
            }
        }

        // Sort by display name
        packages = packages.OrderBy(p => p.displayName).ToList();

        // Thu thập tất cả keywords và authors
        CollectAllKeywords();
        CollectAllAuthors();
    }

    private void CollectAllKeywords()
    {
        allKeywords.Clear();
        keywordCounts.Clear();

        foreach (var package in packages)
        {
            if (package.keywords != null && package.keywords.Count > 0)
            {
                foreach (var keyword in package.keywords)
                {
                    if (!string.IsNullOrEmpty(keyword))
                    {
                        if (keywordCounts.ContainsKey(keyword))
                        {
                            keywordCounts[keyword]++;
                        }
                        else
                        {
                            keywordCounts[keyword] = 1;
                            allKeywords.Add(keyword);
                        }
                    }
                }
            }
        }

        // Sort keywords theo số lượng package sử dụng (nhiều nhất trước)
        allKeywords = allKeywords.OrderByDescending(k => keywordCounts[k]).ToList();
    }

    private void CollectAllAuthors()
    {
        allAuthors.Clear();
        authorCounts.Clear();

        foreach (var package in packages)
        {
            if (!string.IsNullOrEmpty(package.authorName))
            {
                if (authorCounts.ContainsKey(package.authorName))
                {
                    authorCounts[package.authorName]++;
                }
                else
                {
                    authorCounts[package.authorName] = 1;
                    allAuthors.Add(package.authorName);
                }
            }
        }

        // Sort authors theo số lượng package (nhiều nhất trước)
        allAuthors = allAuthors.OrderByDescending(a => authorCounts[a]).ToList();
    }

    private void InstallPackage(string packageName, string version)
    {
        if (installingPackages.ContainsKey(packageName) && installingPackages[packageName])
        {
            return; // Đang cài đặt rồi
        }

        installingPackages[packageName] = true;
        Repaint();

        // Tạo package ID với version
        string packageId = $"{packageName}@{version}";

        Debug.Log($"🚀 Bắt đầu cài đặt package: {packageId}");

        // Sử dụng Unity Package Manager để cài đặt từ git URL hoặc registry
        var request = Client.Add(packageId);

        // Theo dõi tiến trình cài đặt - Sử dụng EditorApplication.delayCall
        CheckInstallProgressWrapper(request, packageName, packageId);
    }

    private void CheckInstallProgressWrapper(AddRequest request, string packageName, string packageId)
    {
        EditorApplication.delayCall += () =>
        {
            if (!request.IsCompleted)
            {
                // Nếu chưa hoàn thành, tiếp tục kiểm tra
                CheckInstallProgressWrapper(request, packageName, packageId);
                return;
            }

            // Request đã hoàn thành
            if (installingPackages.ContainsKey(packageName))
            {
                installingPackages[packageName] = false;
            }

            if (request.Status == StatusCode.Success)
            {
                // Cập nhật ngay lập tức vào danh sách installed
                installedPackages[packageName] = true;
                installedVersions[packageName] = request.Result.version;
                
                Debug.Log($"✅ Cài đặt thành công package: {packageId}");
                // EditorUtility.DisplayDialog("Thành công",
                //     $"Package '{packageName}' v{request.Result.version} đã được cài đặt thành công!", "OK");

                // Refresh lại danh sách packages đã cài từ manifest
                LoadInstalledPackagesFromManifest();
            }
            else if (request.Status >= StatusCode.Failure)
            {
                Debug.LogError($"❌ Lỗi khi cài đặt package {packageId}: {request.Error?.message}");

                string errorMsg = request.Error?.message ?? "Unknown error";

                // Hiển thị thông báo lỗi chi tiết hơn
                if (errorMsg.Contains("Cannot resolve package"))
                {
                    errorMsg = $"Không tìm thấy package '{packageName}'.\n\n" + "Hãy đảm bảo rằng:\n" +
                               "1. Bạn đã thêm Dmobin registry vào manifest.json\n" +
                               "2. Package này tồn tại trong registry\n" + "3. Bạn có quyền truy cập vào registry";
                }

                EditorUtility.DisplayDialog("Lỗi cài đặt", $"Không thể cài đặt package '{packageName}':\n\n{errorMsg}",
                    "OK");
            }

            Repaint();
        };
    }

    private void RemovePackage(string packageName)
    {
        Debug.Log($"🗑️ Bắt đầu xóa package: {packageName}");

        var request = Client.Remove(packageName);
        CheckRemoveProgressWrapper(request, packageName);
    }

    private string[] GetPackagesInFolder(string packageName)
    {
        var packagePath = $"Packages/{packageName}";

        try
        {
            // Kiểm tra thư mục có tồn tại không
            if (!System.IO.Directory.Exists(packagePath))
            {
                return null;
            }

            string[] unityPackageFiles =
                System.IO.Directory.GetFiles(packagePath, "*.unitypackage", System.IO.SearchOption.AllDirectories);

            return unityPackageFiles;
        }
        catch
        {
            return null;
        }
    }

    private void ImportUnityPackage(string packagePath)
    {
        Debug.Log($"📁 Đang import Unity package từ: {packagePath}");

        try
        {
            // Kiểm tra file có tồn tại không
            if (!System.IO.File.Exists(packagePath))
            {
                Debug.LogError($"❌ Không tìm thấy file package: {packagePath}");
                EditorUtility.DisplayDialog("Lỗi import", $"Không thể tìm thấy file package:\n\n{packagePath}", "OK");
                return;
            }

            // Import package với cửa sổ interactive
            AssetDatabase.ImportPackage(packagePath, true);
            Debug.Log($"🚀 Đã bắt đầu import Unity package: {System.IO.Path.GetFileName(packagePath)}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Lỗi khi import package: {e.Message}");
            EditorUtility.DisplayDialog("Lỗi import", $"Có lỗi xảy ra khi import package:\n\n{e.Message}", "OK");
        }
    }

    private void PingPackageLocation(string packageName)
    {
        Debug.Log($"🔗 Đang tìm vị trí package: {packageName}");

        // Tìm package trong Packages folder
        string packagePath = $"Packages/{packageName}";
        UnityEngine.Object packageAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(packagePath);

        if (packageAsset != null)
        {
            // Ping package trong Project window
            EditorGUIUtility.PingObject(packageAsset);
            Debug.Log($"✅ Đã tìm thấy package tại: {packagePath}");
        }
        else
        {
            // Thử tìm trong Library/PackageCache
            string[] guids = AssetDatabase.FindAssets(packageName, new[] { "Library/PackageCache" });
            if (guids.Length > 0)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                UnityEngine.Object cachedPackage = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (cachedPackage != null)
                {
                    EditorGUIUtility.PingObject(cachedPackage);
                    Debug.Log($"✅ Đã tìm thấy package trong cache tại: {assetPath}");
                }
            }
            else
            {
                Debug.LogWarning($"⚠️ Không tìm thấy package '{packageName}' trong Project window");
                EditorUtility.DisplayDialog("Không tìm thấy",
                    $"Không thể tìm thấy package '{packageName}' trong Project window.\n\nPackage có thể chưa được cài đặt hoặc đã bị xóa.",
                    "OK");
            }
        }
    }

    private void CheckRemoveProgressWrapper(RemoveRequest request, string packageName)
    {
        EditorApplication.delayCall += () =>
        {
            if (!request.IsCompleted)
            {
                // Nếu chưa hoàn thành, tiếp tục kiểm tra
                CheckRemoveProgressWrapper(request, packageName);
                return;
            }

            // Request đã hoàn thành
            if (request.Status == StatusCode.Success)
            {
                // Xóa khỏi danh sách installed
                if (installedPackages.ContainsKey(packageName))
                {
                    installedPackages.Remove(packageName);
                }

                if (installedVersions.ContainsKey(packageName))
                {
                    installedVersions.Remove(packageName);
                }

                Debug.Log($"✅ Xóa thành công package: {packageName}");
                // Refresh lại danh sách từ manifest
                LoadInstalledPackagesFromManifest();
            }
            else if (request.Status >= StatusCode.Failure)
            {
                Debug.LogError($"❌ Lỗi khi xóa package {packageName}: {request.Error?.message}");
                EditorUtility.DisplayDialog("Lỗi xóa package",
                    $"Không thể xóa package '{packageName}':\n\n{request.Error?.message}", "OK");
            }

            Repaint();
        };
    }

    private void DrawTextWithHighlight(string text, GUIStyle style, string highlight)
    {
        if (string.IsNullOrEmpty(highlight) || string.IsNullOrEmpty(text))
        {
            GUILayout.Label(text, style);
            return;
        }

        string lowerText = text.ToLower();
        string lowerHighlight = highlight.ToLower();

        EditorGUILayout.BeginHorizontal();

        // Sử dụng rich text để tô màu trong một Label duy nhất
        GUIStyle richTextStyle = new GUIStyle(style);
        richTextStyle.richText = true;

        string displayText = "";
        int startIndex = 0;
        int foundIndex;

        while ((foundIndex = lowerText.IndexOf(lowerHighlight, startIndex, StringComparison.Ordinal)) != -1)
        {
            // Text trước highlight
            if (foundIndex > startIndex)
            {
                string beforeText = text.Substring(startIndex, foundIndex - startIndex);
                displayText += beforeText;
            }

            // Text được highlight với màu xanh và bold
            string highlightedText = text.Substring(foundIndex, highlight.Length);
            displayText += $"<color=#3D8FFF><b>{highlightedText}</b></color>";

            startIndex = foundIndex + highlight.Length;
        }

        // Text còn lại sau highlight cuối cùng
        if (startIndex < text.Length)
        {
            string remainingText = text.Substring(startIndex);
            displayText += remainingText;
        }

        GUILayout.Label(displayText, richTextStyle, GUILayout.ExpandWidth(false));

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawTextWithHighlightForID(string text, GUIStyle style, string highlight)
    {
        if (string.IsNullOrEmpty(highlight) || string.IsNullOrEmpty(text))
        {
            GUILayout.Label(text, style, GUILayout.ExpandWidth(false));
            return;
        }

        string lowerText = text.ToLower();
        string lowerHighlight = highlight.ToLower();

        // Sử dụng rich text để tô màu trong một Label duy nhất
        GUIStyle richTextStyle = new GUIStyle(style);
        richTextStyle.richText = true;

        string displayText = "";
        int startIndex = 0;
        int foundIndex;

        while ((foundIndex = lowerText.IndexOf(lowerHighlight, startIndex, StringComparison.Ordinal)) != -1)
        {
            // Text trước highlight
            if (foundIndex > startIndex)
            {
                string beforeText = text.Substring(startIndex, foundIndex - startIndex);
                displayText += beforeText;
            }

            // Text được highlight với màu xanh và bold
            string highlightedText = text.Substring(foundIndex, highlight.Length);
            displayText += $"<color=#3D8FFF><b>{highlightedText}</b></color>";

            startIndex = foundIndex + highlight.Length;
        }

        // Text còn lại sau highlight cuối cùng
        if (startIndex < text.Length)
        {
            string remainingText = text.Substring(startIndex);
            displayText += remainingText;
        }

        GUILayout.Label(displayText, richTextStyle, GUILayout.ExpandWidth(false));
    }

    private Texture2D MakeTexture(int width, int height, Color color)
    {
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }

        Texture2D texture = new Texture2D(width, height);
        texture.SetPixels(pixels);
        texture.Apply();

        return texture;
    }

    private Texture2D MakeTextureWithBorder(int width, int height, Color backgroundColor, Color borderColor,
        int borderWidth = 2)
    {
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Kiểm tra xem có phải border không
                bool isBorder = x < borderWidth || x >= width - borderWidth || y < borderWidth ||
                                y >= height - borderWidth;

                pixels[y * width + x] = isBorder ? borderColor : backgroundColor;
            }
        }

        Texture2D texture = new Texture2D(width, height);
        texture.SetPixels(pixels);
        texture.Apply();

        return texture;
    }

    [Serializable]
    private class PackageDataArray
    {
        public List<PackageData> items;
    }

    [Serializable]
    private class PackageData
    {
        public string name;
        public string version;
        public string displayName;
        public string description;
        public string unity;
        public AuthorData author;
        public List<string> keywords;
        public DistData dist;
    }

    [Serializable]
    private class AuthorData
    {
        public string name;
        public string email;
        public string url;
    }

    [Serializable]
    private class DistData
    {
        public string tarball;
        public int fileCount;
        public long unpackedSize;
    }

    [Serializable]
    private class PackageInfo
    {
        public string name;
        public string version;
        public string displayName;
        public string description;
        public string unityVersion;
        public string authorName;
        public List<string> keywords;
        public string tarballUrl;
    }
    
    public static class GDKDefineSymbolsName
    {
        public static readonly string[] AllDefineSymbols = new string[]
        {
            "GDK_USE_ADJUST",
            "GDK_USE_ADMOB",
            "GDK_USE_APPMETRICA",
            "GDK_USE_FIREBASE",
            "GDK_USE_FIREBASE_ANALYTICS",
            "GDK_USE_FIREBASE_CRASHLYTICS",
            "GDK_USE_FIREBASE_MESSAGING",
            "GDK_USE_FIREBASE_REMOTE_CONFIG",
            "GDK_USE_IAP",
            "GDK_USE_LEVEL_PLAY",
            "GDK_USE_MAX",
            "GDK_USE_NATIVE_ADMOB",
            "GDK_USE_PUBSCALE",
            "GDK_USE_SPINE",
            "GDK_USE_YANDEX",
            "LEVELPLAY_DEPENDENCIES_INSTALLED"
        };
    }
}