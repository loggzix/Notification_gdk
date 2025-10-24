// using UnityEngine;
// using UnityEditor;

// /// <summary>
// /// Script Unity Editor để refresh assets khi nhấn Ctrl+R
// /// Tác giả: Unity Developer
// /// Mô tả: Cho phép refresh Unity Asset Database bằng phím tắt Ctrl+R
// /// </summary>
// public class RefreshAssetsScript
// {
//     // Thêm menu item với phím tắt Ctrl+R
//     [MenuItem("Tools/Refresh Assets %r")] // %r = Ctrl+R (% là Ctrl trên Windows/Linux, # là Cmd trên Mac)
//     public static void RefreshAssets()
//     {
//         AssetDatabase.Refresh();
//     }
    
//     // Validate menu item (luôn có thể sử dụng khi không ở Play Mode)``
//     [MenuItem("Tools/Refresh Assets %r", true)]
//     public static bool ValidateRefreshAssets()
//     {
//         return !EditorApplication.isPlaying;
//     }
// }

// /// <summary>
// /// Script bổ sung để hỗ trợ phím tắt trong Scene View
// /// </summary>
// [InitializeOnLoad]
// public class SceneViewRefreshShortcut
// {
//     static SceneViewRefreshShortcut()
//     {
//         SceneView.duringSceneGui += OnSceneGUI;
//     }
    
//     static void OnSceneGUI(SceneView sceneView)
//     {
//         Event e = Event.current;
        
//         // Kiểm tra phím tắt Ctrl+R trong Scene View
//         if (e != null && e.type == EventType.KeyDown && e.control && e.keyCode == KeyCode.R)
//         {
//             if (!EditorApplication.isPlaying)
//             {
//                 e.Use();
//                 RefreshAssetsScript.RefreshAssets();
//             }
//         }
//     }
// }