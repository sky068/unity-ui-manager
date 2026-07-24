using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.UISystem.Example
{
    /// <summary>
    /// 测试场景入口：每个按钮固定打开一个窗口用例。
    /// 本类故意不使用 UIManager.Instance 兜底，用于验证场景对象的 VContainer 注入链路；
    /// 如果注入失败，Start 会明确报错并停止绑定按钮。
    /// </summary>
    public class WindowTestScenePresenter : MonoBehaviour
    {
        [Header("Dialog")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button dialogCompactButton;
        [SerializeField] private Button dialogContentButton;

        [Header("FullScreen")]
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button fullScreenInfoButton;
        [SerializeField] private Button fullScreenListButton;

        [Header("None")]
        [SerializeField] private Button tipsButton;
        [SerializeField] private Button noneToastButton;
        [SerializeField] private Button noneLoadingButton;
        [SerializeField] private Button commonToastButton;

        [Header("Occlusion")]
        [SerializeField] private Button dialogOcclusionButton;
        [SerializeField] private Button reverseDialogOcclusionButton;
        [SerializeField] private Button fullScreenOcclusionButton;

        [SerializeField] private TMP_Text resultText;

        private IUIManager _ui;
        private bool _occlusionTestRunning;
        private bool _secondaryWindowOpening;

        [Inject]
        private void Construct(IUIManager uiManager)
        {
            // 初始场景由 UISystemScope.Awake 扫描注入；后续场景由 sceneLoaded 注入。
            // 对后续加载场景，Construct 保证在 Start 前调用，但不会早于 Awake/OnEnable。
            _ui = uiManager;
        }

        private void Start()
        {
            // 不使用静态单例兜底，避免测试场景掩盖注入配置或时序错误。
            if (_ui == null)
            {
                Debug.LogError("[WindowTestScenePresenter] IUIManager 未注入，请确认 UISystemScope 已初始化场景对象。");
                return;
            }

            confirmButton.onClick.AddListener(() => OpenConfirm().Forget());
            dialogCompactButton.onClick.AddListener(
                () => OpenFrame(UIWindowId.DialogCompactTest, "Dialog · 紧凑内容").Forget());
            dialogContentButton.onClick.AddListener(
                () => OpenFrame(UIWindowId.DialogContentTest, "Dialog · 长内容自适应").Forget());

            settingsButton.onClick.AddListener(() => OpenSettings().Forget());
            fullScreenInfoButton.onClick.AddListener(
                () => OpenNoMaskFullScreenTest().Forget(Debug.LogException));
            fullScreenListButton.onClick.AddListener(
                () => OpenFrame(UIWindowId.FullScreenListTest, "FullScreen · 列表页").Forget());

            tipsButton.onClick.AddListener(() => OpenTips().Forget());
            noneToastButton.onClick.AddListener(
                () => OpenFrame(UIWindowId.NoneToastTest, "None · 轻提示").Forget());
            noneLoadingButton.onClick.AddListener(
                () => OpenFrame(UIWindowId.NoneLoadingTest, "None · 加载提示").Forget());
            commonToastButton.onClick.AddListener(ShowCommonToast);
            dialogOcclusionButton.onClick.AddListener(
                () => OpenDialogOcclusionTest(largeOverSmall: true).Forget(Debug.LogException));
            reverseDialogOcclusionButton.onClick.AddListener(
                () => OpenDialogOcclusionTest(largeOverSmall: false).Forget(Debug.LogException));
            fullScreenOcclusionButton.onClick.AddListener(
                () => OpenFullScreenOcclusionTest().Forget(Debug.LogException));
        }

        private async UniTaskVoid OpenConfirm()
        {
            SetResult("Dialog · 删除确认：等待选择");
            bool confirmed = await _ui.OpenAsync<ConfirmWindow, ConfirmParam, bool>(
                UIWindowId.ConfirmWindow,
                new ConfirmParam
                {
                    Title = "删除确认",
                    Message = "确定要删除这条测试存档吗？\n此操作仅用于验证弹窗返回值。",
                    Confirm = "删除",
                    Cancel = "取消"
                });
            SetResult(confirmed ? "Dialog · 删除确认：已确认" : "Dialog · 删除确认：已取消");
        }

        private async UniTaskVoid OpenSettings()
        {
            SetResult("FullScreen · 设置页：已打开");
            await _ui.OpenAsync<SettingsWindow>(UIWindowId.SettingWindow);
            SetResult("FullScreen · 设置页：已关闭");
        }

        private async UniTaskVoid OpenTips()
        {
            SetResult("None · 新手提示：等待关闭");
            await _ui.OpenAsync<TipsWindow, TipsParam, Unit>(
                UIWindowId.TipsWindow,
                new TipsParam
                {
                    Title = "新手提示",
                    Message = "这是无底框窗口的参数与完成回调测试。",
                    OkText = "明白了"
                });
            SetResult("None · 新手提示：已关闭");
        }

        private async UniTaskVoid OpenFrame(UIWindowId windowId, string label)
        {
            SetResult(label + "：已打开");
            await _ui.OpenAsync<FrameTestWindow>(windowId);
            SetResult(label + "：已关闭");
        }

        private void ShowCommonToast()
        {
            _ui.ShowToast("这是一条不带图标的 Common Toast", null, ToastDuration.Normal);
            SetResult("Toast · CommonToast：已显示，将自动关闭");
        }

        private async UniTask OpenDialogOcclusionTest(bool largeOverSmall)
        {
            if (!TryBeginOcclusionTest()) return;
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            try
            {
                await _ui.CloseAllAsync();
                // Destroy 会在帧末真正移除节点；必须等旧 Frame/Mask 清零后再打开 A，
                // 否则上一轮的 B 可能在 A 的首帧短暂残留。
                await WaitForConditionAsync(
                    () => _ui.OpenCount == 0 && CountFrames() == 0 && CountMasks(false) == 0,
                    "清理旧测试窗口", token);

                UIWindowId primaryId = largeOverSmall
                    ? UIWindowId.DialogCompactTest
                    : UIWindowId.DialogContentTest;
                UIWindowId secondaryId = largeOverSmall
                    ? UIWindowId.DialogContentTest
                    : UIWindowId.DialogCompactTest;
                string testLabel = largeOverSmall ? "大窗口覆盖小窗口" : "小窗口覆盖大窗口";
                int expectedCulledFrames = largeOverSmall ? 1 : 0;

                SetResult($"遮挡测试 · 正在打开{testLabel}的一级窗口");
                _ui.OpenAsync<FrameTestWindow>(primaryId)
                    .Forget(Debug.LogException);
                await WaitForConditionAsync(
                    () => _ui.OpenCount == 1 && CountVisibleMasks() == 1 &&
                          AreFramesFullyOpen(1),
                    "窗口 A 完整打开", token);
                ConfigurePrimaryWindow(
                    largeOverSmall ? "打开大 Dialog" : "打开小 Dialog",
                    secondaryId,
                    testLabel,
                    expectedCulledFrames);
                SetResult($"遮挡测试 · {testLabel}：一级窗口已打开，请点击窗口内按钮");
            }
            finally
            {
                _occlusionTestRunning = false;
            }
        }

        private async UniTask OpenFullScreenOcclusionTest()
        {
            if (!TryBeginOcclusionTest()) return;
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            try
            {
                await _ui.CloseAllAsync();
                await WaitForConditionAsync(
                    () => _ui.OpenCount == 0 && CountFrames() == 0 && CountMasks(false) == 0,
                    "清理旧测试窗口", token);

                SetResult("遮挡测试 · 正在打开窗口 A");
                _ui.OpenAsync<FrameTestWindow>(UIWindowId.DialogCompactTest)
                    .Forget(Debug.LogException);
                await WaitForConditionAsync(
                    () => _ui.OpenCount == 1 && AreFramesFullyOpen(1),
                    "窗口 A 完整打开", token);
                ConfigurePrimaryWindow(
                    "打开全屏二级窗口",
                    UIWindowId.FullScreenListTest,
                    "全屏窗口覆盖 Dialog",
                    expectedCulledFrames: 1);
                SetResult("遮挡测试 · 一级 Dialog 已打开，请点击窗口内按钮打开全屏二级窗口");
            }
            finally
            {
                _occlusionTestRunning = false;
            }
        }

        private void ConfigurePrimaryWindow(
            string buttonLabel,
            UIWindowId secondaryWindowId,
            string secondaryLabel,
            int expectedCulledFrames)
        {
            var windows = _ui.UICanvas.GetComponentsInChildren<FrameTestWindow>(true);
            if (windows.Length != 1)
                throw new InvalidOperationException(
                    $"[OcclusionTest] 配置一级窗口按钮时预期 1 个 FrameTestWindow，实际 {windows.Length} 个");

            windows[0].ConfigureSecondaryAction(
                buttonLabel,
                () => OpenSecondaryWindow(
                        secondaryWindowId, secondaryLabel, expectedCulledFrames)
                    .Forget(Debug.LogException));
        }

        private async UniTask OpenSecondaryWindow(
            UIWindowId windowId, string label, int expectedCulledFrames)
        {
            if (_secondaryWindowOpening || _ui.OpenCount != 1)
                return;

            _secondaryWindowOpening = true;
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            try
            {
                SetResult($"遮挡测试 · 正在打开{label}");
                UniTask windowTask = _ui.OpenAsync<FrameTestWindow>(windowId);
                await WaitForConditionAsync(
                    () => _ui.OpenCount == 2 && AreFramesFullyOpen(2) &&
                          CountCulledFrames() == expectedCulledFrames &&
                          CountMasks(true) == 1 &&
                          CountVisibleMasks() == 1,
                    $"{label}覆盖一级窗口", token);

                bool passed = ValidateSingleVisibleMask(label);
                SetResult($"遮挡测试 · {label}：{(passed ? "通过" : "未通过")}；"
                          + "关闭二级窗口后可再次点击一级窗口按钮");
                if (!passed)
                    Debug.LogError($"[OcclusionTest] {label}覆盖展示未通过");

                await windowTask;
                SetResult($"遮挡测试 · {label}已关闭，可再次点击一级窗口按钮");
            }
            finally
            {
                _secondaryWindowOpening = false;
            }
        }

        private async UniTask OpenNoMaskFullScreenTest()
        {
            if (!TryBeginOcclusionTest()) return;
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            SetResult("Mask 测试 · 信息页按窗口配置不创建遮罩");
            try
            {
                await _ui.CloseAllAsync();
                _ui.OpenAsync<FrameTestWindow>(UIWindowId.FullScreenInfoTest)
                    .Forget(Debug.LogException);
                await WaitForConditionAsync(() => _ui.OpenCount == 1, "无 Mask 全屏打开", token);

                int maskCount = CountMasks(false);
                bool passed = maskCount == 0;
                SetResult($"Mask 测试 · 无遮罩窗口：{(passed ? "通过" : "未通过")}，实际 Mask {maskCount}/0");
                if (!passed)
                    Debug.LogError($"[OcclusionTest] 无遮罩窗口预期 0 个 Mask，实际 {maskCount} 个");
                await _ui.CloseAllAsync();
            }
            finally
            {
                _occlusionTestRunning = false;
            }
        }

        private bool TryBeginOcclusionTest()
        {
            if (_occlusionTestRunning)
            {
                SetResult("遮挡测试正在运行，请等待当前用例结束");
                return false;
            }
            _occlusionTestRunning = true;
            return true;
        }

        private async UniTask WaitForConditionAsync(
            Func<bool> condition, string stage, CancellationToken token, float timeoutSeconds = 3f)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            int stableFrames = 0;
            while (stableFrames < 2)
            {
                token.ThrowIfCancellationRequested();
                if (Time.realtimeSinceStartup >= deadline)
                    throw new TimeoutException($"[OcclusionTest] 等待“{stage}”超时");
                stableFrames = condition() ? stableFrames + 1 : 0;
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        private bool ValidateSingleVisibleMask(string label)
        {
            int visibleMasks = CountVisibleMasks();
            bool passed = visibleMasks == 1;
            if (!passed)
                Debug.LogError($"[OcclusionTest] {label} Mask 交接异常：可见数量 {visibleMasks}/1");
            return passed;
        }

        private int CountFrames()
        {
            if (_ui?.UICanvas == null) return 0;
            return _ui.UICanvas.GetComponentsInChildren<UIWindowFrame>(true).Length;
        }

        private bool AreFramesFullyOpen(int expectedCount)
        {
            if (_ui?.UICanvas == null) return false;
            var frames = _ui.UICanvas.GetComponentsInChildren<UIWindowFrame>(true);
            if (frames.Length != expectedCount) return false;

            for (int i = 0; i < frames.Length; i++)
            {
                if (!frames[i].TryGetComponent<CanvasGroup>(out var canvasGroup) ||
                    canvasGroup.alpha < 0.999f)
                    return false;
                if (frames[i].transform is RectTransform rect &&
                    (Mathf.Abs(rect.localScale.x - 1f) > 0.001f ||
                     Mathf.Abs(rect.localScale.y - 1f) > 0.001f))
                    return false;
            }
            return true;
        }

        private int CountCulledFrames()
        {
            if (_ui?.UICanvas == null) return 0;
            int count = 0;
            var frames = _ui.UICanvas.GetComponentsInChildren<UIWindowFrame>(true);
            for (int i = 0; i < frames.Length; i++)
            {
                var renderers = frames[i].GetComponentsInChildren<CanvasRenderer>(true);
                bool allCulled = renderers.Length > 0;
                for (int j = 0; j < renderers.Length; j++)
                    allCulled &= renderers[j] == null || renderers[j].cull;
                if (allCulled) count++;
            }
            return count;
        }

        private int CountVisibleMasks() => CountMasks(false) - CountMasks(true);

        private int CountMasks(bool culledOnly)
        {
            if (_ui?.UICanvas == null)
                return 0;

            int count = 0;
            var renderers = _ui.UICanvas.GetComponentsInChildren<CanvasRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].gameObject.name == "__Mask__" &&
                    (!culledOnly || renderers[i].cull))
                    count++;
            }
            return count;
        }

        private void SetResult(string value)
        {
            if (resultText != null)
                resultText.text = value;
        }
    }
}
