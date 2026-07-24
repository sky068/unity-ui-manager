using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

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
        private CancellationTokenSource _operationCancellation;

        private void Start()
        {
            _ui = UIManager.Instance ??
                  throw new InvalidOperationException("场景中不存在可用的 UISystemScope");
        }

        // ── 场景 Button.onClick 入口 ──────────────────────────────────

        public void OnConfirmClicked() => Run(OpenConfirm);
        public void OnDialogCompactClicked() =>
            Run(token => OpenFrame(DemoWindowIds.DialogCompactTest, "Dialog · 紧凑内容", token));
        public void OnDialogContentClicked() =>
            Run(token => OpenFrame(DemoWindowIds.DialogContentTest, "Dialog · 长内容自适应", token));
        public void OnSettingsClicked() => Run(OpenSettings);
        public void OnFullScreenInfoClicked() =>
            Run(token => OpenFrame(DemoWindowIds.FullScreenInfoTest, "FullScreen · 信息页", token));
        public void OnFullScreenListClicked() =>
            Run(token => OpenFrame(DemoWindowIds.FullScreenListTest, "FullScreen · 列表页", token));
        public void OnTipsClicked() => Run(OpenTips);
        public void OnNoneToastClicked() =>
            Run(token => OpenFrame(DemoWindowIds.NoneToastTest, "None · 轻提示", token));
        public void OnNoneLoadingClicked() =>
            Run(token => OpenFrame(DemoWindowIds.NoneLoadingTest, "None · 加载提示", token));
        public void OnCommonToastClicked()
        {
            CancelActiveOperation();
            ShowCommonToast();
        }

        public void OnOpenClicked()
        {
            CancelActiveOperation();
            OpenSynchronously();
        }
        public void OnOpenWithParamClicked()
        {
            CancelActiveOperation();
            OpenWithParamSynchronously();
        }
        public void OnOpenForResultClicked() => Run(OpenSameDialogForResultAsync);
        public void OnWaitOpenedClicked() => Run(WaitUntilOpened);
        public void OnWaitClosedClicked() => Run(WaitUntilClosed);
        public void OnHandleCloseClicked() => Run(CloseWithHandle);

        public void OnCloseByIdClicked() => Run(ShowCloseById);
        public void OnCloseByIdAsyncClicked() => Run(ShowCloseByIdAsync);
        public void OnCloseTopClicked() => Run(ShowCloseTop);
        public void OnCloseTopAsyncClicked() => Run(ShowCloseTopAsync);
        public void OnCloseAllClicked() => Run(ShowCloseAll);
        public void OnCloseAllAsyncClicked() => Run(ShowCloseAllAsync);
        public void OnCloseAllImmediatelyClicked() => Run(ShowCloseAllImmediately);

        public void OnLargeOverSmallClicked() => Run(token => OpenDialogStackExample(true, token));
        public void OnSmallOverLargeClicked() => Run(token => OpenDialogStackExample(false, token));
        public void OnFullScreenOverDialogClicked() => Run(OpenFullScreenStackExample);

        // ── 窗口外观示例 ──────────────────────────────────────────────

        private async UniTask OpenConfirm(CancellationToken token)
        {
            SetResult("Dialog · 删除确认：等待选择");
            bool confirmed = await _ui.OpenForResultAsync<ConfirmParam, bool>(
                DemoWindowIds.ConfirmWindow,
                new ConfirmParam
                {
                    Title = "删除确认",
                    Message = "确定要删除这条示例存档吗？\n这个弹窗演示如何接收 bool 返回值。",
                    Confirm = "删除",
                    Cancel = "取消"
                }).AttachExternalCancellation(token);
            SetResult(confirmed ? "Dialog · 删除确认：已确认" : "Dialog · 删除确认：已取消");
        }

        private async UniTask OpenSettings(CancellationToken token)
        {
            SetResult("FullScreen · 设置页：已打开");
            await _ui.Open(DemoWindowIds.SettingWindow).Closed.AttachExternalCancellation(token);
            SetResult("FullScreen · 设置页：已关闭");
        }

        private async UniTask OpenTips(CancellationToken token)
        {
            SetResult("None · 新手提示：等待关闭");
            await _ui.Open<TipsParam, UIUnit>(
                DemoWindowIds.TipsWindow,
                new TipsParam
                {
                    Title = "新手提示",
                    Message = "这个无底框窗口演示参数传入与完成回调。",
                    OkText = "明白了"
                }).Closed.AttachExternalCancellation(token);
            SetResult("None · 新手提示：已关闭");
        }

        private async UniTask OpenFrame(
            UIWindowId windowId, string label, CancellationToken token)
        {
            SetResult(label + "：已打开");
            await _ui.Open<FrameWindowParam, UIUnit>(windowId, null).Closed
                .AttachExternalCancellation(token);
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
            _ui.Open(DemoWindowIds.SettingWindow);
            SetResult("Open()：FullScreen 已同步创建；Mask 在不透明全屏内容后方不可见");
        }

        private void OpenWithParamSynchronously()
        {
            _ui.Open<ConfirmParam, bool>(
                DemoWindowIds.ConfirmWindow,
                CreateOpenComparisonParam());
            SetResult("同步对照：ConfirmWindow 已立即创建并显示 Mask，调用方不等待结果");
        }

        private async UniTask OpenSameDialogForResultAsync(CancellationToken token)
        {
            SetResult("异步对照：同一个 ConfirmWindow 已创建，正在等待选择和完整清理");
            bool confirmed = await _ui.OpenForResultAsync<ConfirmParam, bool>(
                DemoWindowIds.ConfirmWindow,
                CreateOpenComparisonParam()).AttachExternalCancellation(token);
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

        private async UniTask WaitUntilOpened(CancellationToken token)
        {
            var handle = _ui.Open<FrameWindowParam, UIUnit>(
                DemoWindowIds.DialogCompactTest, null);
            SetResult("handle.Opened：正在等待开场动画");
            await handle.Opened.AttachExternalCancellation(token);
            SetResult("handle.Opened：开场动画和 OnOpened 已完成，请手动关闭窗口");
        }

        private async UniTask WaitUntilClosed(CancellationToken token)
        {
            var handle = _ui.Open(DemoWindowIds.SettingWindow);
            SetResult("handle.Closed：请手动关闭设置页");
            await handle.Closed.AttachExternalCancellation(token);
            SetResult("handle.Closed：退场动画、OnClosing 和对象清理均已完成");
        }

        private async UniTask CloseWithHandle(CancellationToken token)
        {
            var handle = _ui.Open<FrameWindowParam, UIUnit>(
                DemoWindowIds.DialogCompactTest, null);
            SetResult("handle.Close()：窗口完全打开后将由句柄关闭");
            await handle.Opened.AttachExternalCancellation(token);
            await UniTask.Delay(TimeSpan.FromMilliseconds(700), cancellationToken: token);
            handle.Close();
            await handle.Closed.AttachExternalCancellation(token);
            SetResult("handle.Close()：指定窗口已完成关闭和清理");
        }

        // ── 关闭 API ──────────────────────────────────────────────────

        private async UniTask ShowCloseById(CancellationToken token)
        {
            await OpenTwoWindowsForCloseExample(token);
            await UniTask.Delay(TimeSpan.FromMilliseconds(700), cancellationToken: token);
            _ui.Close(DemoWindowIds.DialogCompactTest);
            SetResult("Close(windowId)：已同步关闭指定的底层 Dialog，栈顶窗口保留");
        }

        private async UniTask ShowCloseByIdAsync(CancellationToken token)
        {
            await OpenTwoWindowsForCloseExample(token);
            await UniTask.Delay(TimeSpan.FromMilliseconds(700), cancellationToken: token);
            SetResult("CloseAsync(windowId)：正在等待指定的底层 Dialog 完成清理");
            await _ui.CloseAsync(DemoWindowIds.DialogCompactTest)
                .AttachExternalCancellation(token);
            SetResult("CloseAsync(windowId)：指定 ID 窗口已清理，栈顶窗口保留");
        }

        private async UniTask ShowCloseTop(CancellationToken token)
        {
            await OpenTwoWindowsForCloseExample(token);
            await UniTask.Delay(TimeSpan.FromMilliseconds(700), cancellationToken: token);
            _ui.CloseTop();
            SetResult("CloseTop()：已同步发起栈顶窗口关闭，底层窗口保留");
        }

        private async UniTask ShowCloseTopAsync(CancellationToken token)
        {
            await OpenTwoWindowsForCloseExample(token);
            await UniTask.Delay(TimeSpan.FromMilliseconds(700), cancellationToken: token);
            SetResult("CloseTopAsync()：正在等待栈顶窗口退场和清理");
            await _ui.CloseTopAsync().AttachExternalCancellation(token);
            SetResult("CloseTopAsync()：栈顶窗口已清理，底层窗口保留");
        }

        private async UniTask ShowCloseAll(CancellationToken token)
        {
            await OpenTwoWindowsForCloseExample(token);
            await UniTask.Delay(TimeSpan.FromMilliseconds(700), cancellationToken: token);
            _ui.CloseAll();
            _ui.Open(DemoWindowIds.SettingWindow);
            SetResult("CloseAll()：只关闭调用时的窗口；随后同步打开的设置页会保留");
        }

        private async UniTask ShowCloseAllAsync(CancellationToken token)
        {
            await OpenTwoWindowsForCloseExample(token);
            await UniTask.Delay(TimeSpan.FromMilliseconds(700), cancellationToken: token);
            SetResult("CloseAllAsync()：正在等待调用时捕获的全部窗口完成清理");
            await _ui.CloseAllAsync().AttachExternalCancellation(token);
            SetResult("CloseAllAsync()：本次关闭批次已全部清理完成");
        }

        private async UniTask ShowCloseAllImmediately(CancellationToken token)
        {
            await OpenTwoWindowsForCloseExample(token);
            await UniTask.Delay(TimeSpan.FromMilliseconds(700), cancellationToken: token);
            _ui.CloseAllImmediately();
            SetResult("CloseAllImmediately()：全部活动窗口已无动画立即清理");
        }

        private async UniTask OpenTwoWindowsForCloseExample(CancellationToken token)
        {
            await _ui.CloseAllAsync().AttachExternalCancellation(token);
            var first = _ui.Open<FrameWindowParam, UIUnit>(
                DemoWindowIds.DialogCompactTest, null);
            var second = _ui.Open<FrameWindowParam, UIUnit>(
                DemoWindowIds.DialogContentTest, null);
            await first.Opened.AttachExternalCancellation(token);
            await second.Opened.AttachExternalCancellation(token);
            SetResult("关闭示例：已打开两个窗口，稍后执行对应关闭方法");
        }

        // ── 窗口覆盖示例 ──────────────────────────────────────────────

        private UniTask OpenDialogStackExample(
            bool largeOverSmall, CancellationToken token)
        {
            UIWindowId primaryId = largeOverSmall
                ? DemoWindowIds.DialogCompactTest
                : DemoWindowIds.DialogContentTest;
            UIWindowId secondaryId = largeOverSmall
                ? DemoWindowIds.DialogContentTest
                : DemoWindowIds.DialogCompactTest;
            string label = largeOverSmall ? "大 Dialog 覆盖小 Dialog" : "小 Dialog 覆盖大 Dialog";
            string buttonLabel = largeOverSmall ? "打开大 Dialog" : "打开小 Dialog";
            return OpenStackExample(primaryId, secondaryId, label, buttonLabel, token);
        }

        private UniTask OpenFullScreenStackExample(CancellationToken token) => OpenStackExample(
            DemoWindowIds.DialogCompactTest,
            DemoWindowIds.FullScreenListTest,
            "全屏窗口覆盖 Dialog",
            "打开全屏窗口",
            token);

        private async UniTask OpenStackExample(
            UIWindowId primaryId,
            UIWindowId secondaryId,
            string label,
            string buttonLabel,
            CancellationToken token)
        {
            try
            {
                await _ui.CloseAllAsync().AttachExternalCancellation(token);
                await UniTask.NextFrame(token);

                SetResult($"窗口叠加示例 · {label}：请点击一级窗口内的按钮");
                var primary = _ui.Open<FrameWindowParam, UIUnit>(
                    primaryId,
                    new FrameWindowParam
                    {
                        SecondaryButtonLabel = buttonLabel,
                        SecondaryAction = () =>
                        {
                            if (!token.IsCancellationRequested)
                                Run(nextToken => OpenFrame(secondaryId, label, nextToken));
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

        private void Run(Func<CancellationToken, UniTask> operation)
        {
            CancelActiveOperation();
            _operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy());
            RunOperation(operation, _operationCancellation).Forget();
        }

        private void CancelActiveOperation() => _operationCancellation?.Cancel();

        private async UniTaskVoid RunOperation(
            Func<CancellationToken, UniTask> operation,
            CancellationTokenSource operationCancellation)
        {
            try
            {
                await operation(operationCancellation.Token);
            }
            catch (OperationCanceledException) when (operationCancellation.IsCancellationRequested)
            {
                // 新操作开始或场景销毁后，旧操作不再继续修改全局 UI。
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                if (ReferenceEquals(_operationCancellation, operationCancellation))
                    _operationCancellation = null;
                operationCancellation.Dispose();
            }
        }

        private void OnDestroy()
        {
            _operationCancellation?.Cancel();
            _operationCancellation = null;
        }

        private void SetResult(string value)
        {
            if (resultText != null)
                resultText.text = value;
        }
    }
}
