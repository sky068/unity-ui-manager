using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using PackageManagerInfo = UnityEditor.PackageManager.PackageInfo;

namespace Game.UISystem.Installer
{
    internal static class UISystemInstaller
    {
        private const string PendingKey = "Game.UISystem.Installer.Pending";
        private const string IntegrationPendingKey =
            "Game.UISystem.Installer.VContainerPending";
        private const string InstallerPackageName = "com.skyxu.uisystem.installer";
        private const string UISystemPackageName = "com.skyxu.uisystem";
        private const string VContainerIntegrationPackageName =
            "com.skyxu.uisystem.vcontainer";
        private const string UniTaskPackageName = "com.cysharp.unitask";
        private const string VContainerPackageName = "jp.hadashikick.vcontainer";

        private const string RepositoryUrl =
            "https://github.com/sky068/unity-ui-manager.git";
        private const string UniTaskUrl =
            "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask" +
            "#a9e27c03d411d2fca01cc7410c24c97cd77cb539";
        private const string VContainerUrl =
            "https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer" +
            "#49bdeaa1d9b0558b45ecc6f28f6078223d4ca5a4";

        private static AddAndRemoveRequest _request;
        private static bool _installingVContainerIntegration;

        [InitializeOnLoadMethod]
        private static void ResumeAfterDomainReload()
        {
            if (SessionState.GetBool(PendingKey, false))
                EditorApplication.delayCall += ReportPendingResult;
        }

        [MenuItem("Tools/UISystem/Installer/Install Core Packages")]
        public static void InstallMissingPackages()
        {
            BeginInstall(GetMissingCorePackages(), false);
        }

        [MenuItem("Tools/UISystem/Installer/Install VContainer Integration")]
        public static void InstallVContainerIntegration()
        {
            BeginInstall(GetMissingVContainerIntegrationPackages(), true);
        }

        [MenuItem("Tools/UISystem/Installer/Show Package Status")]
        public static void ShowPackageStatus()
        {
            var registered = GetRegisteredPackageNames();
            string status = string.Join("\n", new[]
            {
                FormatStatus(UniTaskPackageName, "UniTask", registered),
                FormatStatus(UISystemPackageName, "UISystem Core", registered),
                FormatStatus(VContainerPackageName, "VContainer（可选）", registered),
                FormatStatus(
                    VContainerIntegrationPackageName,
                    "UISystem VContainer Integration（可选）",
                    registered)
            });
            ShowMessage("UISystem Package Status", status, false);
        }

        private static void BeginInstall(List<PackageSpec> missing, bool integration)
        {
            if (_request != null && !_request.IsCompleted)
            {
                Debug.LogWarning("[UISystem Installer] Package Manager 正在执行安装，请稍候。");
                return;
            }

            if (missing.Count == 0)
            {
                ShowMessage(
                    "UISystem Installer",
                    integration
                        ? "UISystem Core、UniTask、VContainer 和适配包均已安装。"
                        : "UISystem Core 和 UniTask 均已安装。",
                    false);
                return;
            }

            string details = string.Join("\n\n", missing.Select(item =>
                $"{item.DisplayName}\n{item.Url}"));
            if (Application.isBatchMode)
            {
                Debug.LogError(
                    "[UISystem Installer] 批处理模式下拒绝修改 Package 清单。" +
                    "请在 Unity Editor 中执行安装菜单并确认以下地址：\n\n" + details);
                return;
            }

            string title = integration ? "安装 VContainer 集成" : "安装 UISystem Core";
            if (!EditorUtility.DisplayDialog(
                    title,
                    "Package Manager 将批量添加以下缺失包：\n\n" + details +
                    "\n\n安装过程需要访问 GitHub，是否继续？",
                    "安装",
                    "取消"))
                return;

            _installingVContainerIntegration = integration;
            SessionState.SetBool(PendingKey, true);
            SessionState.SetBool(IntegrationPendingKey, integration);
            _request = Client.AddAndRemove(
                missing.Select(item => item.Url).ToArray(),
                Array.Empty<string>());
            EditorApplication.update += PollRequest;
            Debug.Log("[UISystem Installer] 开始安装：" +
                      string.Join(", ", missing.Select(item => item.DisplayName)));
        }

        private static void PollRequest()
        {
            if (_request == null)
            {
                EditorApplication.update -= PollRequest;
                return;
            }

            if (!_request.IsCompleted)
            {
                EditorUtility.DisplayProgressBar(
                    "UISystem Installer",
                    "正在通过 Unity Package Manager 安装依赖……",
                    0.5f);
                return;
            }

            EditorApplication.update -= PollRequest;
            EditorUtility.ClearProgressBar();
            SessionState.SetBool(PendingKey, false);

            if (_request.Status == StatusCode.Success)
            {
                string installed = _installingVContainerIntegration
                    ? "UISystem、UniTask、VContainer 和适配包已安装。"
                    : "UISystem Core 和 UniTask 已安装。";
                string nextSteps = _installingVContainerIntegration
                    ? "1. Window > TextMeshPro > Import TMP Essential Resources\n" +
                      "2. Tools > UISystem > Initialize Project Assets\n" +
                      "3. 在 UISystemScope 所在对象添加 VContainerUISystemAdapter\n" +
                      "4. Tools > UISystem > Validate Installation"
                    : "1. Window > TextMeshPro > Import TMP Essential Resources\n" +
                      "2. Tools > UISystem > Initialize Project Assets\n" +
                      "3. Tools > UISystem > Validate Installation";
                ShowMessage(
                    "UISystem 安装完成",
                    installed + "\n\n接下来执行：\n" + nextSteps,
                    false);
            }
            else
            {
                string message = _request.Error?.message ?? "未知 Package Manager 错误";
                ShowMessage("UISystem 安装失败", message, true);
            }

            _request = null;
        }

        private static void ReportPendingResult()
        {
            if (!SessionState.GetBool(PendingKey, false))
                return;

            bool integration = SessionState.GetBool(IntegrationPendingKey, false);
            List<PackageSpec> missing = integration
                ? GetMissingVContainerIntegrationPackages()
                : GetMissingCorePackages();
            if (missing.Count == 0)
            {
                SessionState.SetBool(PendingKey, false);
                Debug.Log("[UISystem Installer] 域重载后确认：所选包均已安装。");
            }
            else
            {
                Debug.LogWarning(
                    "[UISystem Installer] 安装尚未完整结束或被中断，仍缺少：" +
                    string.Join(", ", missing.Select(item => item.DisplayName)) +
                    "。可重新执行安装菜单。");
            }
        }

        private static List<PackageSpec> GetMissingCorePackages()
        {
            var registered = GetRegisteredPackageNames();
            var missing = new List<PackageSpec>();
            AddCorePackages(missing, registered);
            return missing;
        }

        private static List<PackageSpec> GetMissingVContainerIntegrationPackages()
        {
            var registered = GetRegisteredPackageNames();
            var missing = new List<PackageSpec>();
            AddCorePackages(missing, registered);
            if (!registered.Contains(VContainerPackageName))
                missing.Add(new PackageSpec("VContainer", VContainerUrl));
            if (!registered.Contains(VContainerIntegrationPackageName))
                missing.Add(new PackageSpec(
                    "Skyxu UI System VContainer Integration",
                    BuildRepositoryPackageUrl("com.skyxu.uisystem.vcontainer")));
            return missing;
        }

        private static void AddCorePackages(
            List<PackageSpec> missing,
            HashSet<string> registered)
        {
            if (!registered.Contains(UniTaskPackageName))
                missing.Add(new PackageSpec("UniTask", UniTaskUrl));
            if (!registered.Contains(UISystemPackageName))
                missing.Add(new PackageSpec(
                    "Skyxu UI System Core",
                    BuildRepositoryPackageUrl("com.skyxu.uisystem")));
        }

        private static HashSet<string> GetRegisteredPackageNames() =>
            new HashSet<string>(
                PackageManagerInfo.GetAllRegisteredPackages().Select(package => package.name),
                StringComparer.Ordinal);

        private static string BuildRepositoryPackageUrl(string packageDirectory)
        {
            var installer = PackageManagerInfo.FindForAssetPath(
                $"Packages/{InstallerPackageName}/package.json");
            string revision = installer?.git?.revision;
            if (string.IsNullOrWhiteSpace(revision))
                revision = installer?.source == PackageSource.Embedded ? "main" : "v1.1.0";
            return RepositoryUrl + "?path=/Packages/" + packageDirectory + "#" +
                   Uri.EscapeDataString(revision);
        }

        private static string FormatStatus(
            string packageName,
            string displayName,
            HashSet<string> registered) =>
            $"{(registered.Contains(packageName) ? "已安装" : "缺失")}  {displayName}";

        private static void ShowMessage(string title, string message, bool isError)
        {
            if (isError)
                Debug.LogError($"[UISystem Installer] {message}");
            else
                Debug.Log($"[UISystem Installer] {message}");

            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog(title, message, "确定");
        }

        private readonly struct PackageSpec
        {
            public readonly string DisplayName;
            public readonly string Url;

            public PackageSpec(string displayName, string url)
            {
                DisplayName = displayName;
                Url = url;
            }
        }
    }
}
