using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.UI;
using System.Threading;
using VContainer;
using VContainer.Unity;

namespace Game.UISystem
{
    public enum ToastDuration
    {
        /// <summary>显示 1 秒；开场和退场动画时间不计入该时长。</summary>
        Short = 0,
        /// <summary>显示 2 秒；开场和退场动画时间不计入该时长。</summary>
        Normal = 1,
        /// <summary>显示 3 秒；开场和退场动画时间不计入该时长。</summary>
        Long = 2
    }

    /// <summary>
    /// UI 系统对业务层暴露的入口。
    /// OpenAsync 返回时表示窗口已经完成退场动画并清理，而不只是收到关闭请求。
    /// </summary>
    public interface IUIManager
    {
        /// <summary>打开无参数、无返回值窗口，并异步等待窗口完全关闭。</summary>
        UniTask OpenAsync<TWindow>(
            UIWindowId windowId,
            UILayer? layerOverride = null)
            where TWindow : UIWindow<R3.Unit, R3.Unit>;

        /// <summary>打开带参数和返回值的窗口，并取得窗口关闭时提交的结果。</summary>
        UniTask<TResult> OpenAsync<TWindow, TParam, TResult>(
            UIWindowId windowId,
            TParam param,
            UILayer? layerOverride = null)
            where TWindow : UIWindow<TParam, TResult>;

        /// <summary>关闭主窗口栈顶部窗口；Toast 不属于主窗口栈，不受此方法影响。</summary>
        UniTask CloseTopAsync();

        /// <summary>关闭所有活动 UI，包括主窗口、Loading 和不入栈的 Toast。</summary>
        UniTask CloseAllAsync();

        /// <summary>不播放退场动画，立即清理所有活动 UI；用于活动场景切换。</summary>
        void CloseAllImmediately();

        /// <summary>显示独立计时的轻提示；该接口不等待 Toast 关闭。</summary>
        void ShowToast(
            string text,
            string icon = null,
            ToastDuration time = ToastDuration.Normal);
        Canvas UICanvas { get; }
        int OpenCount { get; }
    }

    /// <summary>
    /// 一个已创建 UI 实例的运行时上下文。
    /// 名称沿用 StackEntry，但 Toast 也会使用该结构，只是不会加入主窗口栈。
    /// Closed 在实例完成退场和资源清理后才完成，供 CloseTopAsync/CloseAllAsync 等待。
    /// </summary>
    internal sealed class StackEntry
    {
        private bool _isClosing;
        private bool _isCleaned;

        public readonly UIWindowBase Window;
        public readonly UIWindowFrame Frame;
        public readonly GameObject FrameGo;
        public readonly GameObject MaskGo;
        public readonly CanvasGroup CanvasGroup;
        public readonly UIWindowStyle Style;
        public readonly UniTaskCompletionSource<R3.Unit> Closed =
            new UniTaskCompletionSource<R3.Unit>();
        public readonly bool ParticipatesInWindowStack;
        public bool IsInStack { get; set; }
        public bool OpenAnimationComplete { get; set; }

        public StackEntry(
            UIWindowBase window,
            UIWindowFrame frame,
            GameObject frameGo,
            GameObject maskGo,
            CanvasGroup canvasGroup,
            UIWindowStyle style,
            bool participatesInWindowStack)
        {
            Window = window;
            Frame = frame;
            FrameGo = frameGo;
            MaskGo = maskGo;
            CanvasGroup = canvasGroup;
            Style = style;
            ParticipatesInWindowStack = participatesInWindowStack;
        }

        public bool TryBeginClosing()
        {
            // 多个关闭来源可能同时到达（按钮、ESC、切场景、自动计时），
            // 只有第一个调用者负责执行退场动画，其余调用者等待 Closed 即可。
            if (_isClosing) return false;
            _isClosing = true;
            return true;
        }

        public bool TryBeginCleanup()
        {
            // 正常关闭、异常捕获和对象销毁都可能进入清理，必须保证 Destroy/移栈只执行一次。
            if (_isCleaned) return false;
            _isCleaned = true;
            return true;
        }

        public void ApplyInteraction(bool isTop)
        {
            // 开场动画完成前禁止点击；非栈顶窗口和正在关闭的窗口同样禁止交互。
            // Toast 不入主栈，调用方会把它视为自身层级中的活动顶层。
            bool isActiveTop = isTop && !Window.IsCloseRequested && !Window.IsClosing;
            if (CanvasGroup != null)
            {
                CanvasGroup.interactable = isActiveTop && OpenAnimationComplete;
                CanvasGroup.blocksRaycasts = isActiveTop && OpenAnimationComplete;
            }

            if (MaskGo != null && MaskGo.TryGetComponent<Image>(out var image))
                image.raycastTarget = isActiveTop;
        }
    }

    public class UIManager : IUIManager
    {
        private readonly IObjectResolver _container;
        private readonly UILayerConfig _layerConfig;
        private readonly UIWindowConfig _windowConfig;
        // 主窗口栈负责返回键、CloseTopAsync 和窗口之间的交互互斥。
        // 为保持结构简单，除 Toast Layer 外的窗口仍共用这一条栈。
        private readonly Stack<StackEntry> _stack = new Stack<StackEntry>();

        // 活动集合包含所有实例，包括不进入主栈的 Toast。
        // 切场景调用 CloseAllAsync 时必须以它为准，不能只遍历 _stack。
        private readonly HashSet<StackEntry> _activeEntries = new HashSet<StackEntry>();

        public int OpenCount => _stack.Count;
        public Canvas UICanvas => _layerConfig.UICanvas;

        private static IUIManager _ui;

        public static IUIManager Instance
        {
            get
            {
                // 静态入口只为非容器代码和历史代码保留。正常业务组件应注入 IUIManager，
                // 这样依赖关系明确，也便于测试时替换实现。
                if (_ui != null) return _ui;
                var scope = UISystemScope.Instance;
                if (scope == null || scope.Container == null)
                {
                    Debug.LogError("[UIManager] 场景中不存在可用的 UISystemScope");
                    return null;
                }

                _ui = scope.Container.Resolve<IUIManager>();
                return _ui;
            }
        }

        internal static void ResetInstance(IUIManager instance)
        {
            // 持久 Scope 销毁时必须清空缓存，避免下次进入 Play Mode 或重建 Scope 后
            // 返回已经随旧容器释放的 UIManager。
            if (ReferenceEquals(_ui, instance) || instance == null)
                _ui = null;
        }

        [Inject]
        public UIManager(
            IObjectResolver container,
            UILayerConfig layerConfig,
            UIWindowConfig windowConfig)
        {
            // UIManager 是纯 C# 容器单例，所有外部依赖都由 VContainer 构造注入。
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _layerConfig = layerConfig ?? throw new ArgumentNullException(nameof(layerConfig));
            _windowConfig = windowConfig ?? throw new ArgumentNullException(nameof(windowConfig));
        }

        public async UniTask OpenAsync<TWindow>(
            UIWindowId windowId,
            UILayer? layerOverride = null)
            where TWindow : UIWindow<R3.Unit, R3.Unit>
        {
            await OpenAsync<TWindow, R3.Unit, R3.Unit>(
                windowId, R3.Unit.Default, layerOverride);
        }

        public async UniTask<TResult> OpenAsync<TWindow, TParam, TResult>(
            UIWindowId windowId,
            TParam param,
            UILayer? layerOverride = null)
            where TWindow : UIWindow<TParam, TResult>
        {
            // 先完成全部配置校验，再创建 GameObject，避免配置错误留下半初始化节点。
            var config = GetValidatedConfig(windowId, layerOverride);
            var style = config.style;
            var layer = layerOverride ?? config.defaultLayer;
            var layerRoot = _layerConfig.GetLayerRoot(layer);
            if (layerRoot == null)
                throw new InvalidOperationException(
                    $"[UIManager] Window '{windowId}' 对应的 Layer 根节点未配置");

            // Toast 只复用加载、动画和清理流程，不加入主窗口栈，因此不会禁用当前弹窗，
            // 也不会被 CloseTopAsync 当作业务窗口关闭。
            bool participatesInWindowStack = layer != UILayer.Toast;

            GameObject frameGo = null;
            GameObject maskGo = null;
            StackEntry stackEntry = null;
            CancellationTokenSource openAnimationCancellation = null;

            try
            {
                var framePrefab = LoadPrefab("Frames", style.framePrefabAddress, windowId);
                var contentPrefab = LoadPrefab("Windows", config.contentPrefabAddress, windowId);

                // Frame 和内容都通过容器实例化，确保其中的 [Inject] 在对象激活前完成。
                frameGo = _container.Instantiate(framePrefab, layerRoot);
                var frame = frameGo.GetComponent<UIWindowFrame>();
                if (frame == null || frame.ContentRoot == null)
                    throw new InvalidOperationException(
                        $"[UIManager] Frame '{style.framePrefabAddress}' 缺少有效的 UIWindowFrame/ContentRoot");

                if (frame.FrameType != style.frameType)
                    throw new InvalidOperationException(
                        $"[UIManager] Style '{style.name}' 与 Frame '{style.framePrefabAddress}' 类型不一致");

                var contentGo = _container.Instantiate(contentPrefab, frame.ContentRoot);
                var contentRect = contentGo.GetComponent<RectTransform>();
                if (contentRect == null)
                    throw new InvalidOperationException(
                        $"[UIManager] Content '{config.contentPrefabAddress}' 根节点缺少 RectTransform");

                var window = contentGo.GetComponent<TWindow>();
                if (window == null)
                    throw new InvalidOperationException(
                        $"[UIManager] Content '{config.contentPrefabAddress}' 缺少 {typeof(TWindow).Name}");

                var resultSource = new UniTaskCompletionSource<TResult>();
                if (style.showMask)
                {
                    maskGo = UIAnimator.CreateMask(
                        layerRoot,
                        style,
                        () => window.TryRequestClose());
                    maskGo.transform.SetSiblingIndex(frameGo.transform.GetSiblingIndex());
                }

                var canvasGroup = frameGo.GetOrAddComponent<CanvasGroup>();
                var frameRect = frameGo.GetComponent<RectTransform>();
                stackEntry = new StackEntry(
                    window,
                    frame,
                    frameGo,
                    maskGo,
                    canvasGroup,
                    style,
                    participatesInWindowStack);
                openAnimationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    window.GetCancellationTokenOnDestroy());

                // Setup 会先保存关闭结果源和生命周期回调，再调用业务窗口 OnInit。
                // 关闭请求会取消仍在播放的开场动画，使流程尽快进入统一退场阶段。
                window.Setup(
                    param,
                    resultSource,
                    frame,
                    style,
                    () => !stackEntry.ParticipatesInWindowStack || IsTop(stackEntry),
                    () => openAnimationCancellation.Cancel());

                frame.PrepareContent(contentRect);

                // 必须在第一次 await 前登记活动实例，保证同帧发生场景切换时 CloseAllAsync 能看到它。
                _activeEntries.Add(stackEntry);

                if (stackEntry.ParticipatesInWindowStack)
                    Push(stackEntry);

                try
                {
                    await UniTask.WhenAll(
                        UIAnimator.PlayOpenAsync(
                            frameRect, canvasGroup, style, openAnimationCancellation.Token),
                        maskGo != null
                            ? UIAnimator.FadeMaskAsync(
                                maskGo, style.maskColor.a, style.openDuration,
                                openAnimationCancellation.Token)
                            : UniTask.CompletedTask);
                }
                catch (OperationCanceledException) when (window.IsCloseRequested)
                {
                    // 用户可能在开场动画未结束时关闭窗口。这属于正常控制流：
                    // 忽略本次取消异常，随后等待 resultSource 并进入统一退场和清理流程。
                }
                stackEntry.OpenAnimationComplete = true;
                stackEntry.ApplyInteraction(
                    !stackEntry.ParticipatesInWindowStack || IsTop(stackEntry));

                if (!window.IsCloseRequested && !window.IsClosing)
                    window.NotifyOpened();

                // resultSource 由 Close/Complete/ESC/遮罩等关闭入口完成；
                // 外部销毁窗口时使用 Destroy token 终止等待，外层 catch 会负责兜底清理。
                TResult result = await resultSource.Task.AttachExternalCancellation(
                    window.GetCancellationTokenOnDestroy());
                await CloseEntryAsync(stackEntry);
                openAnimationCancellation.Dispose();
                return result;
            }
            catch
            {
                openAnimationCancellation?.Dispose();
                if (stackEntry != null)
                    CleanupEntry(stackEntry);
                else
                {
                    if (maskGo != null) UnityEngine.Object.Destroy(maskGo);
                    if (frameGo != null) UnityEngine.Object.Destroy(frameGo);
                }

                throw;
            }
        }

        public async UniTask CloseTopAsync()
        {
            if (_stack.Count == 0) return;

            var top = _stack.Peek();
            top.Window.TryRequestClose();
            await top.Closed.Task;
        }

        public async UniTask CloseAllAsync()
        {
            if (_activeEntries.Count == 0) return;

            // 使用快照是必要的：每个窗口退场完成后都会从 _activeEntries 删除自身，
            // 直接遍历原集合会在 await 期间产生“集合已修改”异常。
            var entries = new List<StackEntry>(_activeEntries);
            var closeTasks = new UniTask[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                entries[i].Window.TryRequestClose();
                closeTasks[i] = entries[i].Closed.Task;
            }

            // 并发退场，避免窗口较多时逐个等待导致切场景耗时累加。
            // 返回时快照中的实例均已完成 CleanupEntry。
            await UniTask.WhenAll(closeTasks);
        }

        public void CloseAllImmediately()
        {
            if (_activeEntries.Count == 0) return;

            // 仍使用快照，CleanupEntry 会同步修改 _activeEntries。
            // 立即清理用于场景边界，不播放动画，避免旧场景 UI 短暂覆盖新场景。
            var entries = new List<StackEntry>(_activeEntries);
            foreach (var entry in entries)
            {
                // 先完成窗口结果，使正在 await OpenAsync 的调用方能够正常结束。
                entry.Window.TryRequestClose();

                // TryRequestClose 可能同步唤醒 OpenAsync，并由 CloseEntryAsync 抢先取得关闭权。
                // 只有本方法取得关闭权时才主动派发 OnClosing，保证生命周期回调至多执行一次。
                if (entry.TryBeginClosing())
                {
                    entry.Window.MarkClosing();
                    try
                    {
                        entry.Window.NotifyClosing();
                    }
                    catch (Exception exception)
                    {
                        // 场景切换不能因单个窗口的业务清理异常而中断全局 UI 清理。
                        Debug.LogException(exception);
                    }
                    entry.ApplyInteraction(false);
                }

                CleanupEntry(entry);
            }
        }

        public void ShowToast(
            string text,
            string icon = null,
            ToastDuration time = ToastDuration.Normal)
        {
            if (!Enum.IsDefined(typeof(ToastDuration), time))
                throw new ArgumentOutOfRangeException(nameof(time), time, "未知的 Toast 时长枚举");

            // ShowToast 是不等待返回值的便捷接口。具体实例仍进入 _activeEntries，
            // 因而切场景时 CloseAllAsync 可以关闭尚未到期的 Toast。
            OpenAsync<CommonToast, CommonToastParam, R3.Unit>(
                UIWindowId.CommonToast,
                new CommonToastParam
                {
                    Text = text ?? string.Empty,
                    IconPath = icon,
                    Duration = time
                },
                UILayer.Toast).Forget(Debug.LogException);
        }

        private UIWindowEntry GetValidatedConfig(UIWindowId windowId, UILayer? layerOverride)
        {
            if (!Enum.IsDefined(typeof(UIWindowId), windowId))
                throw new ArgumentOutOfRangeException(nameof(windowId), windowId, "未知的窗口 ID");

            var config = _windowConfig.Get(windowId);
            if (config == null)
                throw new InvalidOperationException($"[UIManager] Window '{windowId}' 未注册");
            if (config.style == null)
                throw new InvalidOperationException($"[UIManager] Window '{windowId}' 未配置 Style");
            if (string.IsNullOrWhiteSpace(config.style.framePrefabAddress))
                throw new InvalidOperationException($"[UIManager] Window '{windowId}' 的 Frame 地址为空");
            if (string.IsNullOrWhiteSpace(config.contentPrefabAddress))
                throw new InvalidOperationException($"[UIManager] Window '{windowId}' 的 Content 地址为空");
            if (!Enum.IsDefined(typeof(UILayer), layerOverride ?? config.defaultLayer))
                throw new InvalidOperationException($"[UIManager] Window '{windowId}' 的 Layer 非法");
            if (!Enum.IsDefined(typeof(UIFrameType), config.style.frameType))
                throw new InvalidOperationException($"[UIManager] Window '{windowId}' 的 FrameType 非法");
            if (!Enum.IsDefined(typeof(WindowAnimationType), config.style.animationType))
                throw new InvalidOperationException($"[UIManager] Window '{windowId}' 的动画类型非法");
            if (config.style.openDuration < 0f || config.style.closeDuration < 0f ||
                float.IsNaN(config.style.openDuration) || float.IsNaN(config.style.closeDuration) ||
                float.IsInfinity(config.style.openDuration) || float.IsInfinity(config.style.closeDuration))
                throw new InvalidOperationException($"[UIManager] Window '{windowId}' 的动画时长非法");
            if (config.style.scaleFrom <= 0f || float.IsNaN(config.style.scaleFrom) ||
                float.IsInfinity(config.style.scaleFrom))
                throw new InvalidOperationException($"[UIManager] Window '{windowId}' 的缩放参数非法");
            if (config.style.animationType == WindowAnimationType.ToastSlide &&
                (config.style.slideDistance < 0f || float.IsNaN(config.style.slideDistance) ||
                 float.IsInfinity(config.style.slideDistance)))
                throw new InvalidOperationException($"[UIManager] Window '{windowId}' 的滑动距离非法");
            if (!IsFinite(config.style.maskColor.r) || !IsFinite(config.style.maskColor.g) ||
                !IsFinite(config.style.maskColor.b) || !IsFinite(config.style.maskColor.a))
                throw new InvalidOperationException($"[UIManager] Window '{windowId}' 的遮罩颜色非法");
            return config;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static GameObject LoadPrefab(string folder, string address, UIWindowId windowId)
        {
            // Resources.Load 返回的资源由 Unity 管理并缓存；这里只负责实例生命周期，
            // 不主动调用 UnloadAsset。路径约定为 Resources/UISystem/{folder}/{address}。
            var prefab = Resources.Load<GameObject>($"UISystem/{folder}/{address}");
            if (prefab == null)
                throw new InvalidOperationException(
                    $"[UIManager] Window '{windowId}' 找不到 UISystem/{folder}/{address}");
            return prefab;
        }

        private void Push(StackEntry entry)
        {
            // 新窗口入栈后暂时禁用原栈顶，防止两个窗口同时接收点击和 ESC。
            if (_stack.Count > 0)
                _stack.Peek().ApplyInteraction(false);
            _stack.Push(entry);
            entry.IsInStack = true;
            entry.ApplyInteraction(true);
        }

        private bool IsTop(StackEntry entry) =>
            entry != null && _stack.Count > 0 && ReferenceEquals(_stack.Peek(), entry);

        private async UniTask CloseEntryAsync(StackEntry entry)
        {
            if (!entry.TryBeginClosing())
            {
                // 已有其他关闭来源正在执行退场；本调用只需等待同一个 Closed 信号。
                await entry.Closed.Task;
                return;
            }

            try
            {
                // 业务 OnClosing 也可能抛异常，因此状态切换、回调和动画必须全部位于
                // try/finally 内；无论哪一步失败，finally 都会完成移栈、销毁和 Closed 信号。
                entry.Window.MarkClosing();
                entry.Window.NotifyClosing();
                entry.ApplyInteraction(false);

                var frameRect = entry.FrameGo != null
                    ? entry.FrameGo.GetComponent<RectTransform>()
                    : null;
                await UniTask.WhenAll(
                    UIAnimator.PlayCloseAsync(
                        frameRect,
                        entry.CanvasGroup,
                        entry.Style,
                        entry.Window.GetCancellationTokenOnDestroy()),
                    entry.MaskGo != null
                        ? UIAnimator.FadeMaskAsync(
                            entry.MaskGo,
                            0f,
                            entry.Style.closeDuration,
                            entry.Window.GetCancellationTokenOnDestroy())
                        : UniTask.CompletedTask);
            }
            catch (OperationCanceledException) when (
                entry.Window == null || entry.Window.IsCloseRequested)
            {
                // 场景切换的立即清理会销毁正在退场的节点，Destroy token 因此取消动画。
                // 此时关闭请求已经成立且 CleanupEntry 会在 finally 执行，属于正常控制流。
            }
            finally
            {
                CleanupEntry(entry);
            }
        }

        private void CleanupEntry(StackEntry entry)
        {
            // 清理方法必须幂等：正常退场、打开异常和 Destroy cancellation 都可能到达这里。
            if (entry == null || !entry.TryBeginCleanup()) return;

            _activeEntries.Remove(entry);

            bool wasTop = entry.IsInStack && IsTop(entry);
            if (entry.IsInStack)
            {
                RemoveEntry(entry);
                entry.IsInStack = false;
            }

            if (entry.MaskGo != null) UnityEngine.Object.Destroy(entry.MaskGo);
            if (entry.FrameGo != null) UnityEngine.Object.Destroy(entry.FrameGo);

            if (wasTop && _stack.Count > 0)
                _stack.Peek().ApplyInteraction(true);

            // Closed 必须最后完成，确保所有等待方恢复时节点已销毁、栈状态已恢复。
            entry.Closed.TrySetResult(R3.Unit.Default);
        }

        private void RemoveEntry(StackEntry target)
        {
            if (target == null || _stack.Count == 0) return;

            // 独立计时或异常可能让非栈顶窗口先关闭。临时弹出它上方的条目，
            // 找到目标后再按原顺序压回，不能简单调用 _stack.Pop()。
            var aboveTarget = new Stack<StackEntry>();
            bool found = false;
            while (_stack.Count > 0)
            {
                var current = _stack.Pop();
                if (ReferenceEquals(current, target))
                {
                    found = true;
                    break;
                }
                aboveTarget.Push(current);
            }

            while (aboveTarget.Count > 0)
                _stack.Push(aboveTarget.Pop());

            if (!found)
                Debug.LogWarning("[UIManager] 尝试移除一个不在栈中的窗口");
        }
    }
}
