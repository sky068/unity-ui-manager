using System;
using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using UnityEngine;
using VContainer;

namespace Game.UISystem.Example
{
    /// <summary>
    /// Demo 场景入口。按钮及其点击事件均直接配置在 UIWindowTestCases 场景中，
    /// 本类只保留可由 Button.onClick 调用的示例方法。
    /// </summary>
    public class WindowTestScenePresenter : MonoBehaviour
    {
        [SerializeField] private TMP_Text resultText;

        private IUIManager _ui;

        [Inject]
        private void Construct(IUIManager uiManager)
        {
            // 初始场景由 UISystemScope.Awake 扫描注入；后续场景由 sceneLoaded 注入。
            // 对后续加载场景，Construct 保证在 Start 前调用，但不会早于 Awake/OnEnable。
            _ui = uiManager;
        }

        // ── 场景 Button.onClick 入口 ──────────────────────────────────

        public void OnConfirmClicked() => Run(OpenConfirm());
        public void OnDialogCompactClicked() =>
            Run(OpenFrame(UIWindowId.DialogCompactTest, "Dialog · 紧凑内容"));
        public void OnDialogContentClicked() =>
            Run(OpenFrame(UIWindowId.DialogContentTest, "Dialog · 长内容自适应"));
        public void OnSettingsClicked() => Run(OpenSettings());
        public void OnFullScreenInfoClicked() =>
            Run(OpenFrame(UIWindowId.FullScreenInfoTest, "FullScreen · 信息页"));
        public void OnFullScreenListClicked() =>
            Run(OpenFrame(UIWindowId.FullScreenListTest, "FullScreen · 列表页"));
        public void OnTipsClicked() => Run(OpenTips());
        public void OnNoneToastClicked() =>
            Run(OpenFrame(UIWindowId.NoneToastTest, "None · 轻提示"));
        public void OnNoneLoadingClicked() =>
            Run(OpenFrame(UIWindowId.NoneLoadingTest, "None · 加载提示"));
        public void OnCommonToastClicked() => ShowCommonToast();

        public void OnOpenClicked() => OpenSynchronously();
        public void OnOpenWithParamClicked() => OpenWithParamSynchronously();
        public void OnOpenForResultClicked() => Run(OpenSameDialogForResultAsync());
        public void OnWaitOpenedClicked() => Run(WaitUntilOpened());
        public void OnWaitClosedClicked() => Run(WaitUntilClosed());
        public void OnHandleCloseClicked() => Run(CloseWithHandle());

        public void OnCloseByIdClicked() => Run(ShowCloseById());
        public void OnCloseByIdAsyncClicked() => Run(ShowCloseByIdAsync());
        public void OnCloseTopClicked() => Run(ShowCloseTop());
        public void OnCloseTopAsyncClicked() => Run(ShowCloseTopAsync());
        public void OnCloseAllClicked() => Run(ShowCloseAll());
        public void OnCloseAllAsyncClicked() => Run(ShowCloseAllAsync());
        public void OnCloseAllImmediatelyClicked() => Run(ShowCloseAllImmediately());

        public void OnLargeOverSmallClicked() => Run(OpenDialogStackExample(true));
        public void OnSmallOverLargeClicked() => Run(OpenDialogStackExample(false));
        public void OnFullScreenOverDialogClicked() => Run(OpenFullScreenStackExample());

        // ── 窗口外观示例 ──────────────────────────────────────────────

        private async UniTask OpenConfirm()
        {
            SetResult("Dialog · 删除确认：等待选择");
            bool confirmed = await _ui.OpenForResultAsync<ConfirmParam, bool>(
                UIWindowId.ConfirmWindow,
                new ConfirmParam
                {
                    Title = "删除确认",
                    Message = "确定要删除这条示例存档吗？\n这个弹窗演示如何接收 bool 返回值。",
                    Confirm = "删除",
                    Cancel = "取消"
                });
            SetResult(confirmed ? "Dialog · 删除确认：已确认" : "Dialog · 删除确认：已取消");
        }

        private async UniTask OpenSettings()
        {
            SetResult("FullScreen · 设置页：已打开");
            await _ui.Open(UIWindowId.SettingWindow).Closed;
            SetResult("FullScreen · 设置页：已关闭");
        }

        private async UniTask OpenTips()
        {
            SetResult("None · 新手提示：等待关闭");
            await _ui.Open<TipsParam, Unit>(
                UIWindowId.TipsWindow,
                new TipsParam
                {
                    Title = "新手提示",
                    Message = "这个无底框窗口演示参数传入与完成回调。",
                    OkText = "明白了"
                }).Closed;
            SetResult("None · 新手提示：已关闭");
        }

        private async UniTask OpenFrame(UIWindowId windowId, string label)
        {
            SetResult(label + "：已打开");
            await _ui.Open<FrameWindowParam, Unit>(windowId, null).Closed;
            SetResult(label + "：已关闭");
        }

        private void ShowCommonToast()
        {
            _ui.ShowToast("这是一条不带图标的 Common Toast", null, ToastDuration.Normal);
            SetResult("Toast · CommonToast：已显示，将自动关闭");
        }

        // ── 打开与生命周期 API ────────────────────────────────────────

        private void OpenSynchronously()
        {
            _ui.Open(UIWindowId.SettingWindow);
            SetResult("Open()：FullScreen 已同步创建；Mask 在不透明全屏内容后方不可见");
        }

        private void OpenWithParamSynchronously()
        {
            _ui.Open<ConfirmParam, bool>(
                UIWindowId.ConfirmWindow,
                CreateOpenComparisonParam());
            SetResult("同步对照：ConfirmWindow 已立即创建并显示 Mask，调用方不等待结果");
        }

        private async UniTask OpenSameDialogForResultAsync()
        {
            SetResult("异步对照：同一个 ConfirmWindow 已创建，正在等待选择和完整清理");
            bool confirmed = await _ui.OpenForResultAsync<ConfirmParam, bool>(
                UIWindowId.ConfirmWindow,
                CreateOpenComparisonParam());
            SetResult(confirmed
                ? "异步对照：同一 ConfirmWindow 已确认并完成清理"
                : "异步对照：同一 ConfirmWindow 已取消并完成清理");
        }

        private static ConfirmParam CreateOpenComparisonParam() => new ConfirmParam
        {
            Title = "同步 / 异步打开对照",
            Message = "两个入口使用同一个 ConfirmWindow、相同参数和相同 Mask 配置。",
            Confirm = "确认",
            Cancel = "取消"
        };

        private async UniTask WaitUntilOpened()
        {
            var handle = _ui.Open<FrameWindowParam, Unit>(
                UIWindowId.DialogCompactTest, null);
            SetResult("handle.Opened：正在等待开场动画");
            await handle.Opened;
            SetResult("handle.Opened：开场动画和 OnOpened 已完成，请手动关闭窗口");
        }

        private async UniTask WaitUntilClosed()
        {
            var handle = _ui.Open(UIWindowId.SettingWindow);
            SetResult("handle.Closed：请手动关闭设置页");
            await handle.Closed;
            SetResult("handle.Closed：退场动画、OnClosing 和对象清理均已完成");
        }

        private async UniTask CloseWithHandle()
        {
            var handle = _ui.Open<FrameWindowParam, Unit>(
                UIWindowId.DialogCompactTest, null);
            SetResult("handle.Close()：窗口完全打开后将由句柄关闭");
            await handle.Opened;
            await UniTask.Delay(TimeSpan.FromMilliseconds(700));
            handle.Close();
            await handle.Closed;
            SetResult("handle.Close()：指定窗口已完成关闭和清理");
        }

        // ── 关闭 API ──────────────────────────────────────────────────

        private async UniTask ShowCloseById()
        {
            await OpenTwoWindowsForCloseExample();
            await UniTask.Delay(TimeSpan.FromMilliseconds(700));
            _ui.Close(UIWindowId.DialogCompactTest);
            SetResult("Close(windowId)：已同步关闭指定的底层 Dialog，栈顶窗口保留");
        }

        private async UniTask ShowCloseByIdAsync()
        {
            await OpenTwoWindowsForCloseExample();
            await UniTask.Delay(TimeSpan.FromMilliseconds(700));
            SetResult("CloseAsync(windowId)：正在等待指定的底层 Dialog 完成清理");
            await _ui.CloseAsync(UIWindowId.DialogCompactTest);
            SetResult("CloseAsync(windowId)：指定 ID 窗口已清理，栈顶窗口保留");
        }

        private async UniTask ShowCloseTop()
        {
            await OpenTwoWindowsForCloseExample();
            await UniTask.Delay(TimeSpan.FromMilliseconds(700));
            _ui.CloseTop();
            SetResult("CloseTop()：已同步发起栈顶窗口关闭，底层窗口保留");
        }

        private async UniTask ShowCloseTopAsync()
        {
            await OpenTwoWindowsForCloseExample();
            await UniTask.Delay(TimeSpan.FromMilliseconds(700));
            SetResult("CloseTopAsync()：正在等待栈顶窗口退场和清理");
            await _ui.CloseTopAsync();
            SetResult("CloseTopAsync()：栈顶窗口已清理，底层窗口保留");
        }

        private async UniTask ShowCloseAll()
        {
            await OpenTwoWindowsForCloseExample();
            await UniTask.Delay(TimeSpan.FromMilliseconds(700));
            _ui.CloseAll();
            _ui.Open(UIWindowId.SettingWindow);
            SetResult("CloseAll()：只关闭调用时的窗口；随后同步打开的设置页会保留");
        }

        private async UniTask ShowCloseAllAsync()
        {
            await OpenTwoWindowsForCloseExample();
            await UniTask.Delay(TimeSpan.FromMilliseconds(700));
            SetResult("CloseAllAsync()：正在等待调用时捕获的全部窗口完成清理");
            await _ui.CloseAllAsync();
            SetResult("CloseAllAsync()：本次关闭批次已全部清理完成");
        }

        private async UniTask ShowCloseAllImmediately()
        {
            await OpenTwoWindowsForCloseExample();
            await UniTask.Delay(TimeSpan.FromMilliseconds(700));
            _ui.CloseAllImmediately();
            SetResult("CloseAllImmediately()：全部活动窗口已无动画立即清理");
        }

        private async UniTask OpenTwoWindowsForCloseExample()
        {
            await _ui.CloseAllAsync();
            var first = _ui.Open<FrameWindowParam, Unit>(
                UIWindowId.DialogCompactTest, null);
            var second = _ui.Open<FrameWindowParam, Unit>(
                UIWindowId.DialogContentTest, null);
            await first.Opened;
            await second.Opened;
            SetResult("关闭示例：已打开两个窗口，稍后执行对应关闭方法");
        }

        // ── 窗口覆盖示例 ──────────────────────────────────────────────

        private UniTask OpenDialogStackExample(bool largeOverSmall)
        {
            UIWindowId primaryId = largeOverSmall
                ? UIWindowId.DialogCompactTest
                : UIWindowId.DialogContentTest;
            UIWindowId secondaryId = largeOverSmall
                ? UIWindowId.DialogContentTest
                : UIWindowId.DialogCompactTest;
            string label = largeOverSmall ? "大 Dialog 覆盖小 Dialog" : "小 Dialog 覆盖大 Dialog";
            string buttonLabel = largeOverSmall ? "打开大 Dialog" : "打开小 Dialog";
            return OpenStackExample(primaryId, secondaryId, label, buttonLabel);
        }

        private UniTask OpenFullScreenStackExample() => OpenStackExample(
            UIWindowId.DialogCompactTest,
            UIWindowId.FullScreenListTest,
            "全屏窗口覆盖 Dialog",
            "打开全屏窗口");

        private async UniTask OpenStackExample(
            UIWindowId primaryId,
            UIWindowId secondaryId,
            string label,
            string buttonLabel)
        {
            var token = this.GetCancellationTokenOnDestroy();
            try
            {
                await _ui.CloseAllAsync().AttachExternalCancellation(token);
                await UniTask.NextFrame(token);

                SetResult($"窗口叠加示例 · {label}：请点击一级窗口内的按钮");
                var primary = _ui.Open<FrameWindowParam, Unit>(
                    primaryId,
                    new FrameWindowParam
                    {
                        SecondaryButtonLabel = buttonLabel,
                        SecondaryAction = () =>
                        {
                            if (!token.IsCancellationRequested)
                                Run(OpenFrame(secondaryId, label));
                        }
                    });
                await primary.Closed.AttachExternalCancellation(token);
                SetResult($"窗口叠加示例 · {label}：一级窗口已关闭");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // Demo 场景销毁后停止后续示例步骤，不在新场景继续打开窗口。
            }
        }

        private static void Run(UniTask task)
        {
            task.Forget(exception =>
            {
                if (!(exception is OperationCanceledException))
                    Debug.LogException(exception);
            });
        }

        private void SetResult(string value)
        {
            if (resultText != null)
                resultText.text = value;
        }
    }
}
