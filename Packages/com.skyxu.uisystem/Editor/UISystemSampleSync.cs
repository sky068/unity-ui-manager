using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using PackageManagerInfo = UnityEditor.PackageManager.PackageInfo;

namespace Game.UISystem.Editor
{
    internal static class UISystemSampleSync
    {
        private const string MenuPath = "Tools/UISystem/Development/Sync Demo To Package Sample";
        private const string PackageRoot = "Packages/com.skyxu.uisystem";
        private const string SampleRelativePath = "Packages/com.skyxu.uisystem/Samples~/UISystemDemo";
        private const string DemoExamples = "Assets/UISystem/Examples";
        private const string DemoConfig = "Assets/UISystem/Config/UIWindowConfig.asset";
        private const string DemoRootPrefab = "Assets/UISystem/Prefabs/UISystemRoot.prefab";
        private const string DemoScenes = "Assets/UISystem/Scenes";
        private const string DemoWindows = "Assets/Resources/UISystem/Windows";

        [MenuItem(MenuPath)]
        public static void Sync()
        {
            if (!CanSync(out string reason))
                throw new InvalidOperationException(reason);

            if (!Application.isBatchMode && !EditorUtility.DisplayDialog(
                    "同步 UISystem Demo Sample",
                    "将用当前 Assets 中的 Demo 重建包内 Samples~/UISystemDemo。" +
                    "\n\n该操作会替换旧 Sample，但不会修改 Assets。是否继续？",
                    "同步",
                    "取消"))
                return;

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(projectRoot))
                throw new InvalidOperationException("无法确定 Unity 项目根目录");

            string samplePath = FullPath(projectRoot, SampleRelativePath);
            string sampleParent = Path.GetDirectoryName(samplePath);
            string stagingPath = Path.Combine(
                sampleParent ?? throw new InvalidOperationException("Sample 路径无效"),
                ".UISystemDemo.sync-" + Guid.NewGuid().ToString("N"));
            string backupPath = samplePath + ".backup-" + Guid.NewGuid().ToString("N");

            int copiedFileCount = 0;
            try
            {
                Directory.CreateDirectory(stagingPath);
                copiedFileCount += CopyDirectory(
                    FullPath(projectRoot, DemoExamples),
                    Path.Combine(stagingPath, "UISystem/Examples"));
                copiedFileCount += CopyDirectory(
                    FullPath(projectRoot, DemoScenes),
                    Path.Combine(stagingPath, "UISystem/Scenes"));
                copiedFileCount += CopyAssetAndMeta(
                    FullPath(projectRoot, DemoConfig),
                    Path.Combine(stagingPath, "UISystem/Config/UIWindowConfig.asset"));
                copiedFileCount += CopyAssetAndMeta(
                    FullPath(projectRoot, DemoRootPrefab),
                    Path.Combine(stagingPath, "UISystem/Prefabs/UISystemRoot.prefab"));
                copiedFileCount += CopyRegisteredWindowPrefabs(
                    projectRoot,
                    Path.Combine(stagingPath, "Resources/UISystem/Windows"));

                ReplaceDirectory(stagingPath, samplePath, backupPath);
                Debug.Log(
                    $"[UISystem] Demo Sample 同步完成，共复制 {copiedFileCount} 个文件。" +
                    "CommonToast、Frame 和字体继续使用 Runtime 核心资源。");
            }
            catch
            {
                // 清理 staging 时若再抛异常会掩盖真正的失败原因，因此吞掉清理异常、保留原始异常。
                TryDeleteDirectory(stagingPath);
                throw;
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateSyncMenu() => CanSync(out _);

        private static bool CanSync(out string reason)
        {
            var package = PackageManagerInfo.FindForAssetPath(PackageRoot + "/package.json");
            if (package == null || package.source != PackageSource.Embedded)
            {
                reason = "只有 UISystem 源码仓库中的 Embedded Package 才能同步 Sample";
                return false;
            }

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(projectRoot))
            {
                reason = "无法确定 Unity 项目根目录";
                return false;
            }

            string[] requiredPaths =
            {
                DemoExamples,
                DemoConfig,
                DemoRootPrefab,
                DemoScenes,
                DemoWindows
            };
            foreach (string path in requiredPaths)
            {
                string fullPath = FullPath(projectRoot, path);
                if (!Directory.Exists(fullPath) && !File.Exists(fullPath))
                {
                    reason = $"缺少 Demo 源路径：{path}";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }

        private static int CopyRegisteredWindowPrefabs(string projectRoot, string destination)
        {
            var config = AssetDatabase.LoadAssetAtPath<UIWindowConfig>(DemoConfig);
            if (config == null)
                throw new InvalidOperationException($"无法加载 {DemoConfig}");

            var serialized = new SerializedObject(config);
            var entries = serialized.FindProperty("entries");
            if (entries == null || !entries.isArray)
                throw new InvalidOperationException("UIWindowConfig 缺少 entries 列表");

            var addresses = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < entries.arraySize; i++)
            {
                string address = entries.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("contentPrefabAddress")?.stringValue;
                if (string.IsNullOrWhiteSpace(address) || address == "CommonToast")
                    continue;
                if (!IsSafePrefabAddress(address))
                    throw new InvalidOperationException($"非法窗口 Prefab 地址：'{address}'");
                addresses.Add(address);
            }

            if (addresses.Count == 0)
                throw new InvalidOperationException("Demo 配置中没有可同步的示例窗口");

            int copied = 0;
            foreach (string address in addresses)
            {
                string source = FullPath(projectRoot, $"{DemoWindows}/{address}.prefab");
                if (!File.Exists(source))
                    throw new FileNotFoundException($"示例窗口 Prefab 不存在：{source}", source);
                copied += CopyAssetAndMeta(source, Path.Combine(destination, address + ".prefab"));
            }
            return copied;
        }

        private static int CopyDirectory(string source, string destination)
        {
            if (!Directory.Exists(source))
                throw new DirectoryNotFoundException(source);

            int copied = 0;
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string fileName = Path.GetFileName(file);
                if (fileName == ".DS_Store" || fileName.EndsWith("~", StringComparison.Ordinal))
                    continue;

                string relative = file.Substring(source.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string target = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target) ?? destination);
                CopyFile(file, target);
                copied++;
            }
            return copied;
        }

        private static int CopyAssetAndMeta(string source, string destination)
        {
            if (!File.Exists(source))
                throw new FileNotFoundException($"源资源不存在：{source}", source);
            if (!File.Exists(source + ".meta"))
                throw new FileNotFoundException($"源资源缺少 .meta：{source}.meta", source + ".meta");

            Directory.CreateDirectory(Path.GetDirectoryName(destination) ??
                                      throw new InvalidOperationException("目标路径无效"));
            CopyFile(source, destination);
            CopyFile(source + ".meta", destination + ".meta");
            return 2;
        }

        private static void CopyFile(string source, string destination)
        {
            if (!IsTextAsset(source))
            {
                File.Copy(source, destination, true);
                return;
            }

            string content = File.ReadAllText(source);
            string normalized = Regex.Replace(
                content,
                @"[ \t]+(?=\r?$)",
                string.Empty,
                RegexOptions.Multiline);
            File.WriteAllText(destination, normalized);
        }

        private static bool IsTextAsset(string path)
        {
            string extension = Path.GetExtension(path);
            switch (extension.ToLowerInvariant())
            {
                case ".asset":
                case ".asmdef":
                case ".cs":
                case ".json":
                case ".md":
                case ".meta":
                case ".prefab":
                case ".txt":
                case ".unity":
                    return true;
                default:
                    return false;
            }
        }

        private static void ReplaceDirectory(string staging, string destination, string backup)
        {
            bool hadDestination = Directory.Exists(destination);
            if (hadDestination)
                Directory.Move(destination, backup);

            try
            {
                Directory.Move(staging, destination);
            }
            catch
            {
                if (hadDestination && !Directory.Exists(destination) && Directory.Exists(backup))
                    Directory.Move(backup, destination);
                throw;
            }

            if (Directory.Exists(backup))
            {
                try
                {
                    Directory.Delete(backup, true);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[UISystem] Sample 已更新，但旧备份删除失败：{backup}\n{exception}");
                }
            }
        }

        private static bool IsSafePrefabAddress(string address)
        {
            // 显式拒绝空地址和包含 ".." 的地址，杜绝目录穿越。
            if (string.IsNullOrEmpty(address) ||
                address.Contains("..", StringComparison.Ordinal))
                return false;

            for (int i = 0; i < address.Length; i++)
            {
                char c = address[i];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-' && c != '.')
                    return false;
            }
            return true;
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[UISystem] 清理临时目录失败：{path}\n{exception}");
            }
        }

        private static string FullPath(string projectRoot, string relativePath) =>
            Path.GetFullPath(Path.Combine(projectRoot, relativePath));
    }
}
