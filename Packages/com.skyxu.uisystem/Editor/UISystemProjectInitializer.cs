using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.UISystem.Editor
{
    internal static class UISystemProjectInitializer
    {
        private const string PackageRoot = "Packages/com.skyxu.uisystem";
        private const string DefaultConfig = PackageRoot + "/Runtime/Defaults/DefaultUIWindowConfig.asset";
        private const string RootTemplate = PackageRoot + "/Editor/Templates/UISystemRoot.prefab";
        private const string ProjectRoot = "Assets/UISystem";
        private const string ProjectConfig = ProjectRoot + "/Config/UIWindowConfig.asset";
        private const string ProjectPrefab = ProjectRoot + "/Prefabs/UISystemRoot.prefab";
        private const string ProjectWindows = "Assets/Resources/UISystem/Windows";

        [MenuItem("Tools/UISystem/Initialize Project Assets")]
        public static void InitializeProjectAssets()
        {
            var created = new List<string>();
            EnsureFolder("Assets", "UISystem");
            EnsureFolder(ProjectRoot, "Config");
            EnsureFolder(ProjectRoot, "Prefabs");
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "UISystem");
            EnsureFolder("Assets/Resources/UISystem", "Windows");

            if (AssetDatabase.LoadAssetAtPath<UIWindowConfig>(ProjectConfig) == null)
            {
                CopyRequired(DefaultConfig, ProjectConfig);
                created.Add(ProjectConfig);
            }

            bool createdPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPrefab) == null;
            if (createdPrefab)
            {
                CopyRequired(RootTemplate, ProjectPrefab);
                created.Add(ProjectPrefab);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            WireProjectConfig(createdPrefab);

            var selected = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPrefab);
            Selection.activeObject = selected;
            EditorGUIUtility.PingObject(selected);

            Debug.Log(created.Count == 0
                ? "[UISystem] 项目资产已存在，未覆盖任何文件。"
                : $"[UISystem] 初始化完成：{string.Join(", ", created)}。请把 UISystemRoot 放入启动场景。");
        }

        [MenuItem("Tools/UISystem/Validate Installation")]
        public static void ValidateInstallation()
        {
            var errors = new List<string>();
            if (AssetDatabase.LoadAssetAtPath<UIWindowConfig>(ProjectConfig) == null)
                errors.Add($"缺少 {ProjectConfig}");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPrefab) == null)
                errors.Add($"缺少 {ProjectPrefab}");
            if (!AssetDatabase.IsValidFolder(ProjectWindows))
                errors.Add($"缺少 {ProjectWindows}");
            if (Resources.Load<GameObject>("UISystem/Frames/UIFrameFullScreen") == null ||
                Resources.Load<GameObject>("UISystem/Frames/UIFrameDialog") == null ||
                Resources.Load<GameObject>("UISystem/Frames/UIFrameNone") == null)
                errors.Add("三种内置 Frame 未能通过 Resources.Load 加载");
            if (Resources.Load<GameObject>("UISystem/Windows/CommonToast") == null)
                errors.Add("内置 CommonToast 未能通过 Resources.Load 加载");
            if (Shader.Find("TextMeshPro/Distance Field") == null)
                errors.Add("缺少 TMP Essential Resources，请先执行 Window > TextMeshPro > Import TMP Essential Resources");

            if (errors.Count == 0)
            {
                Debug.Log("[UISystem] 安装校验通过。");
                return;
            }

            Debug.LogError("[UISystem] 安装校验失败：\n- " + string.Join("\n- ", errors));
        }

        private static void WireProjectConfig(bool createdPrefab)
        {
            var config = AssetDatabase.LoadAssetAtPath<UIWindowConfig>(ProjectConfig);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPrefab);
            if (config == null || prefab == null)
                throw new System.InvalidOperationException("UISystem 项目资产创建失败");

            using (var scope = new PrefabUtility.EditPrefabContentsScope(ProjectPrefab))
            {
                var systemScope = scope.prefabContentsRoot.GetComponentInChildren<UISystemScope>(true);
                if (systemScope == null)
                    throw new System.InvalidOperationException("UISystemRoot 模板缺少 UISystemScope");

                var serialized = new SerializedObject(systemScope);
                var property = serialized.FindProperty("windowConfig");
                string currentPath = property.objectReferenceValue == null
                    ? string.Empty
                    : AssetDatabase.GetAssetPath(property.objectReferenceValue);

                // 新建模板、空引用或仍指向包内默认配置时才重连；绝不覆盖用户已有配置。
                if (createdPrefab || string.IsNullOrEmpty(currentPath) || currentPath.StartsWith(PackageRoot))
                {
                    property.objectReferenceValue = config;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        private static void CopyRequired(string source, string destination)
        {
            if (!AssetDatabase.CopyAsset(source, destination))
                throw new System.InvalidOperationException($"无法复制 '{source}' 到 '{destination}'");
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
