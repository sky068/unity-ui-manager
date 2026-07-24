using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
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
    /// Open/Close 方法同步发起操作；带 Async 后缀的方法只用于等待动画、清理或窗口结果。
    /// </summary>
    public interface IUIManager
    {
        /// <summary>同步创建无参数、无返回值窗口，并立即返回它的生命周期句柄。</summary>
        UIWindowHandle<UIUnit> Open(
            UIWindowId windowId,
            UILayer? layerOverride = null);

        /// <summary>同步创建带参数和返回值的窗口，并立即返回它的生命周期句柄。</summary>
        UIWindowHandle<TResult> Open<TParam, TResult>(
            UIWindowId windowId,
            TParam param,
            UILayer? layerOverride = null);

        /// <summary>打开窗口，并异步等待窗口完成退场动画和清理后返回结果。</summary>
        UniTask<TResult> OpenForResultAsync<TParam, TResult>(
            UIWindowId windowId,
            TParam param,
            UILayer? layerOverride = null);

        /// <summary>
        /// 同步关闭指定 ID 窗口。默认只关闭最后打开的一个实例；
        /// closeAll 为 true 时关闭调用时已存在的全部同 ID 实例。
        /// </summary>
        void Close(UIWindowId windowId, bool closeAll = false);

        /// <summary>
        /// 关闭指定 ID 窗口并等待清理。默认只关闭最后打开的一个实例；
        /// closeAll 为 true 时关闭并等待调用时已存在的全部同 ID 实例。
        /// </summary>
        UniTask CloseAsync(UIWindowId windowId, bool closeAll = false);

        /// <summary>同步请求关闭主窗口栈顶部窗口；Toast 不属于主窗口栈。</summary>
        void CloseTop();

        /// <summary>关闭主窗口栈顶部窗口，并等待其完成退场动画和清理。</summary>
        UniTask CloseTopAsync();

        /// <summary>
        /// 同步请求关闭调用时已有的全部活动 UI，包括主窗口、Loading 和 Toast。
        /// 调用后新开的窗口不属于本次关闭批次。
        /// </summary>
        void CloseAll();

        /// <summary>关闭调用时已有的全部活动 UI，并等待该批窗口完成清理。</summary>
        UniTask CloseAllAsync();

        /// <summary>不播放退场动画，立即清理所有活动 UI；用于活动场景切换。</summary>
        void CloseAllImmediately();

        /// <summary>显示独立计时的轻提示；该接口不等待 Toast 关闭。</summary>
        void ShowToast(
            string text,
            string icon = null,
            ToastDuration time = ToastDuration.Normal);

        /// <summary>
        /// 获取当前仍处于显示生命周期内的全屏窗口数量；包含开场和退场动画中的实例，
        /// 直到实例完成清理后才从计数中移除。
        /// </summary>
        int GetOpenFullScreenCount();

        /// <summary>获取指定 UI Layer 的根节点，供外部按需挂载自定义 UI。</summary>
        RectTransform GetLayerRoot(UILayer layer);

        Canvas UICanvas { get; }
        int OpenCount { get; }
    }

    /// <summary>已创建窗口的生命周期句柄。</summary>
    public sealed class UIWindowHandle<TResult>
    {
        private readonly Action _close;
        private readonly UniTaskCompletionSource<UIUnit> _openedSource;
        private readonly UniTaskCompletionSource<TResult> _closedSource;

        internal UIWindowHandle(
            Action close,
            UniTaskCompletionSource<UIUnit> openedSource,
            UniTaskCompletionSource<TResult> closedSource)
        {
            _close = close;
            _openedSource = openedSource;
            _closedSource = closedSource;
        }

        /// <summary>开场动画和 OnOpened 完成后结束；开场期间关闭则取消。</summary>
        public UniTask Opened => _openedSource.Task;

        /// <summary>窗口提交结果、完成退场动画并清理后结束。</summary>
        public UniTask<TResult> Closed => _closedSource.Task;

        /// <summary>同步请求关闭该窗口；重复调用不会重复执行关闭流程。</summary>
        public void Close() => _close?.Invoke();

        internal void TrySetOpened() => _openedSource.TrySetResult(UIUnit.Default);
        internal void TrySetOpenedException(Exception exception) =>
            _openedSource.TrySetException(exception);
        internal void TrySetOpenedCanceled(CancellationToken token) =>
            _openedSource.TrySetCanceled(token);
        internal void TrySetClosed(TResult result) => _closedSource.TrySetResult(result);
        internal void TrySetClosedException(Exception exception) =>
            _closedSource.TrySetException(exception);
        internal void TrySetClosedCanceled(CancellationToken token) =>
            _closedSource.TrySetCanceled(token);
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
        public readonly UIWindowId WindowId;
        public readonly long OpenSequence;
        public readonly UIWindowFrame Frame;
        public readonly GameObject FrameGo;
        public readonly GameObject MaskGo;
        public readonly GameObject InputBlockerGo;
        public readonly CanvasGroup CanvasGroup;
        public readonly UIWindowStyle Style;
        public readonly UILayer Layer;
        public readonly UIOcclusionMode OcclusionMode;
        public object Handle { get; set; }
        public readonly UniTaskCompletionSource<UIUnit> Closed =
            new UniTaskCompletionSource<UIUnit>();
        public readonly bool ParticipatesInWindowStack;
        public bool IsInStack { get; set; }
        public bool OpenAnimationComplete { get; set; }
        public bool IsFrameCulled { get; private set; }
        public bool IsMaskCulled { get; private set; }
        private readonly List<CanvasRenderer> _frameRenderers = new List<CanvasRenderer>();
        private readonly List<CanvasRenderer> _maskRenderers = new List<CanvasRenderer>();

        public StackEntry(
            UIWindowBase window,
            UIWindowId windowId,
            long openSequence,
            UIWindowFrame frame,
            GameObject frameGo,
            GameObject maskGo,
            GameObject inputBlockerGo,
            CanvasGroup canvasGroup,
            UIWindowStyle style,
            UILayer layer,
            UIOcclusionMode occlusionMode,
            bool participatesInWindowStack)
        {
            Window = window;
            WindowId = windowId;
            OpenSequence = openSequence;
            Frame = frame;
            FrameGo = frameGo;
            MaskGo = maskGo;
            InputBlockerGo = inputBlockerGo;
            CanvasGroup = canvasGroup;
            Style = style;
            Layer = layer;
            OcclusionMode = occlusionMode;
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

            if (InputBlockerGo != null &&
                InputBlockerGo.TryGetComponent<Image>(out var image))
                image.raycastTarget = isActiveTop;
            if (InputBlockerGo != null &&
                InputBlockerGo.TryGetComponent<Button>(out var button))
                button.interactable = isActiveTop && OpenAnimationComplete;
        }

        public void ApplyInputTransitionBlocker()
        {
            // 退场交接期间继续吞掉点击，但不能把这次点击解释为关闭窗口。
            if (InputBlockerGo != null &&
                InputBlockerGo.TryGetComponent<Image>(out var image))
                image.raycastTarget = true;
            if (InputBlockerGo != null &&
                InputBlockerGo.TryGetComponent<Button>(out var button))
                button.interactable = false;
        }

        public void ApplyRenderCulling(bool cullFrame, bool cullMask)
        {
            // 已显示且状态不变时无需扫描；被裁剪节点仍会刷新一次，以覆盖 TMP 在文字
            // 变化后动态创建的子 Renderer。栈变化频率低，这里优先保证恢复显示正确。
            if (cullFrame || IsFrameCulled != cullFrame)
                SetCulled(FrameGo, cullFrame, _frameRenderers);
            if (cullMask || IsMaskCulled != cullMask)
                SetCulled(MaskGo, cullMask, _maskRenderers);
            IsFrameCulled = cullFrame;
            IsMaskCulled = cullMask;
        }

        private static void SetCulled(
            GameObject root, bool culled, List<CanvasRenderer> renderers)
        {
            if (root == null) return;
            renderers.Clear();
            root.GetComponentsInChildren(true, renderers);
            for (int i = 0; i < renderers.Count; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer.cull == culled)
                    continue;

                renderer.cull = culled;
                // Graphic 会在裁剪期间继续变脏；通知它裁剪状态变化，恢复时才能重建
                // TMP 字形、材质和顶点，而不是继续显示隐藏前的旧内容。
                if (renderer.TryGetComponent<Graphic>(out var graphic))
                    graphic.OnCullingChanged();
            }
        }
    }

    public class UIManager : IUIManager
    {
        private readonly IObjectResolver _container;
        private readonly UILayerConfig _layerConfig;
        private readonly UIWindowConfig _windowConfig;
        // 主窗口栈负责返回键、CloseTop/CloseTopAsync 和窗口之间的交互互斥。
        // 为保持结构简单，除 Toast Layer 外的窗口仍共用这一条栈。
        private readonly Stack<StackEntry> _stack = new Stack<StackEntry>();

        // 活动集合包含所有实例，包括不进入主栈的 Toast。
        // CloseAll/CloseAllAsync 必须以它为准，不能只遍历 _stack。
        private readonly HashSet<StackEntry> _activeEntries = new HashSet<StackEntry>();
        private StackEntry _maskOwner;
        private long _nextOpenSequence;
        private bool _isClosingAll;
        private int _closeAllOperationCount;
        private readonly List<StackEntry> _occlusionBuffer = new List<StackEntry>();
        private readonly List<StackEntry> _maskSearchBuffer = new List<StackEntry>();
        private readonly Vector3[] _worldCorners = new Vector3[4];

        public int OpenCount => _stack.Count;
        public Canvas UICanvas => _layerConfig.UICanvas;

        public int GetOpenFullScreenCount()
        {
            int count = 0;
            foreach (var entry in _activeEntries)
            {
                // 计数覆盖完整视觉生命周期：打开登记后立即计入，退场动画和 Destroy
                // 完成前仍计入，避免外部 UI 在全屏窗口尚未消失时提前恢复。
                if (entry?.Style != null &&
                    entry.Style.frameType == UIFrameType.FullScreen &&
                    entry.FrameGo != null)
                    count++;
            }
            return count;
        }

        public RectTransform GetLayerRoot(UILayer layer)
        {
            if (!Enum.IsDefined(typeof(UILayer), layer))
                throw new ArgumentOutOfRangeException(nameof(layer), layer, "未知的 UI Layer");

            var root = _layerConfig.GetLayerRoot(layer);
            if (root == null)
                throw new InvalidOperationException(
                    $"[UIManager] Layer '{layer}' 的根节点未配置或不属于主 Canvas");
            return root;
        }

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

        public UIWindowHandle<UIUnit> Open(
            UIWindowId windowId,
            UILayer? layerOverride = null) =>
            Open<UIUnit, UIUnit>(windowId, UIUnit.Default, layerOverride);

        public UIWindowHandle<TResult> Open<TParam, TResult>(
            UIWindowId windowId,
            TParam param,
            UILayer? layerOverride = null)
        {
            // 先完成全部配置校验，再创建 GameObject，避免配置错误留下半初始化节点。
            var config = GetValidatedConfig(windowId, layerOverride);
            var style = config.style;
            var layer = layerOverride ?? config.defaultLayer;
            ValidateLayerOpenOrder(windowId, layer);
            var layerRoot = _layerConfig.GetLayerRoot(layer);
            if (layerRoot == null)
                throw new InvalidOperationException(
                    $"[UIManager] Window '{windowId}' 对应的 Layer 根节点未配置");

            if (config.openMode == UIWindowOpenMode.Single)
            {
                var existing = FindLatestReusableEntry(windowId);
                if (existing != null)
                {
                    if (existing.Layer != layer)
                        throw new InvalidOperationException(
                            $"[UIManager] Single Window '{windowId}' 已在 {existing.Layer} Layer 中打开，不能以 {layer} Layer 重新打开");
                    if (!(existing.Window is UIWindow<TParam, TResult> existingWindow) ||
                        !(existing.Handle is UIWindowHandle<TResult> existingHandle))
                        throw new InvalidOperationException(
                            $"[UIManager] Single Window '{windowId}' 的参数或返回值类型与已有实例不一致");

                    try
                    {
                        existingWindow.NotifyReopened(param);
                    }
                    catch
                    {
                        // OnReopen 可能已经部分修改 UI。异常时关闭旧实例，避免它以半刷新
                        // 状态继续留在活动栈中；原异常仍交给调用方处理。
                        existing.Window.TryRequestClose();
                        throw;
                    }
                    if (!existing.Window.IsCloseRequested && !existing.Window.IsClosing)
                        BringToTop(existing);
                    return existingHandle;
                }
            }

            // Toast 只复用加载、动画和清理流程，不加入主窗口栈，因此不会禁用当前弹窗，
            // 也不会被 CloseTop/CloseTopAsync 当作业务窗口关闭。
            bool participatesInWindowStack = layer != UILayer.Toast;

            GameObject frameGo = null;
            GameObject maskGo = null;
            GameObject inputBlockerGo = null;
            StackEntry stackEntry = null;
            CancellationTokenSource openAnimationCancellation = null;
            UIWindowHandle<TResult> handle = null;

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

                var window = contentGo.GetComponent<UIWindow<TParam, TResult>>();
                if (window == null)
                    throw new InvalidOperationException(
                        $"[UIManager] Content '{config.contentPrefabAddress}' 缺少 " +
                        $"UIWindow<{typeof(TParam).Name}, {typeof(TResult).Name}>");

                var resultSource = new UniTaskCompletionSource<TResult>();
                var openedSource = new UniTaskCompletionSource<UIUnit>();
                var closedSource = new UniTaskCompletionSource<TResult>();
                // 遮罩只负责视觉表现，不参与射线；是否显示由单窗口条目决定。
                if (config.showMask)
                {
                    // 继承当前唯一可见 Mask 的完整 RGBA；新窗口接管后再插值到自身目标色，
                    // 避免不同 Style 之间切换时发生一帧跳色或透明度突变。
                    Color initialMaskColor = GetVisibleMaskColor(style.maskColor);
                    maskGo = UIAnimator.CreateMask(
                        layerRoot,
                        initialMaskColor);
                    maskGo.transform.SetSiblingIndex(frameGo.transform.GetSiblingIndex());
                }

                // 输入屏蔽层与遮罩独立创建，因此无可见遮罩的窗口也能阻止屏幕点击穿透。
                if (config.blockInput)
                {
                    inputBlockerGo = UIAnimator.CreateInputBlocker(
                        layerRoot,
                        config.closeOnOutsideClick
                            ? () => window.TryRequestClose()
                            : null);
                    inputBlockerGo.transform.SetSiblingIndex(frameGo.transform.GetSiblingIndex());
                }

                var canvasGroup = frameGo.GetOrAddComponent<CanvasGroup>();
                var frameRect = frameGo.GetComponent<RectTransform>();
                stackEntry = new StackEntry(
                    window,
                    windowId,
                    ++_nextOpenSequence,
                    frame,
                    frameGo,
                    maskGo,
                    inputBlockerGo,
                    canvasGroup,
                    style,
                    layer,
                    config.occlusionMode,
                    participatesInWindowStack);
                openAnimationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    window.GetCancellationTokenOnDestroy());
                handle = new UIWindowHandle<TResult>(
                    () => window.TryRequestClose(), openedSource, closedSource);
                stackEntry.Handle = handle;

                // Setup 会先保存关闭结果源和生命周期回调，再调用业务窗口 OnInit。
                // 关闭请求会取消仍在播放的开场动画，使流程尽快进入统一退场阶段。
                window.Setup(
                    param,
                    resultSource,
                    frame,
                    config.closeOnEsc,
                    () => !stackEntry.ParticipatesInWindowStack || IsTop(stackEntry),
                    () => openAnimationCancellation.Cancel());

                frame.PrepareContent(contentRect);

                // 同步返回前登记活动实例，保证紧接着调用 CloseAll 时能够捕获它。
                _activeEntries.Add(stackEntry);

                if (stackEntry.ParticipatesInWindowStack)
                    Push(stackEntry);

                // 创建、Setup 和入栈均在本方法返回前完成；动画和等待结果在后台生命周期中执行。
                RunEntryLifecycleAsync(
                        stackEntry,
                        frameRect,
                        resultSource,
                        handle,
                        openAnimationCancellation)
                    .Forget();
                return handle;
            }
            catch
            {
                openAnimationCancellation?.Dispose();
                if (stackEntry != null)
                    CleanupEntry(stackEntry);
                else
                {
                    if (inputBlockerGo != null) UnityEngine.Object.Destroy(inputBlockerGo);
                    if (maskGo != null) UnityEngine.Object.Destroy(maskGo);
                    if (frameGo != null) UnityEngine.Object.Destroy(frameGo);
                }

                throw;
            }
        }

        public UniTask<TResult> OpenForResultAsync<TParam, TResult>(
            UIWindowId windowId,
            TParam param,
            UILayer? layerOverride = null) =>
            Open<TParam, TResult>(windowId, param, layerOverride).Closed;

        public void Close(UIWindowId windowId, bool closeAll = false)
        {
            ValidateWindowId(windowId);
            if (!closeAll)
            {
                FindLatestActiveEntry(windowId)?.Window.TryRequestClose();
                return;
            }

            var entries = FindActiveEntries(windowId);
            for (int i = 0; i < entries.Count; i++)
                entries[i].Window.TryRequestClose();
        }

        public async UniTask CloseAsync(UIWindowId windowId, bool closeAll = false)
        {
            ValidateWindowId(windowId);
            if (!closeAll)
            {
                var entry = FindLatestActiveEntry(windowId);
                if (entry == null) return;

                entry.Window.TryRequestClose();
                await entry.Closed.Task;
                return;
            }

            var entries = FindActiveEntries(windowId);
            if (entries.Count == 0) return;

            var closeTasks = new UniTask[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                entries[i].Window.TryRequestClose();
                closeTasks[i] = entries[i].Closed.Task;
            }
            await UniTask.WhenAll(closeTasks);
        }

        private async UniTaskVoid RunEntryLifecycleAsync<TResult>(
            StackEntry entry,
            RectTransform frameRect,
            UniTaskCompletionSource<TResult> resultSource,
            UIWindowHandle<TResult> handle,
            CancellationTokenSource openAnimationCancellation)
        {
            try
            {
                try
                {
                    await UniTask.WhenAll(
                        UIAnimator.PlayOpenAsync(
                            frameRect,
                            entry.CanvasGroup,
                            entry.Style,
                            openAnimationCancellation.Token),
                        entry.MaskGo != null
                            ? UIAnimator.AnimateMaskColorAsync(
                                entry.MaskGo,
                                entry.Style.maskColor,
                                entry.Style.openDuration,
                                openAnimationCancellation.Token)
                            : UniTask.CompletedTask);
                }
                catch (OperationCanceledException) when (entry.Window.IsCloseRequested)
                {
                    // 开场期间关闭属于正常流程，随后直接进入统一退场和清理。
                }

                entry.OpenAnimationComplete = true;
                RecalculateOcclusion();
                entry.ApplyInteraction(
                    !entry.ParticipatesInWindowStack || IsTop(entry));

                if (!entry.Window.IsCloseRequested && !entry.Window.IsClosing)
                {
                    entry.Window.NotifyOpened();
                    handle.TrySetOpened();
                }
                else
                {
                    handle.TrySetOpenedCanceled(openAnimationCancellation.Token);
                }

                TResult result = await resultSource.Task.AttachExternalCancellation(
                    entry.Window.GetCancellationTokenOnDestroy());
                await CloseEntryAsync(entry);
                handle.TrySetClosed(result);
            }
            catch (OperationCanceledException exception)
            {
                CleanupEntry(entry);
                handle.TrySetOpenedCanceled(exception.CancellationToken);
                handle.TrySetClosedCanceled(exception.CancellationToken);
            }
            catch (Exception exception)
            {
                CleanupEntry(entry);
                handle.TrySetOpenedException(exception);
                handle.TrySetClosedException(exception);
                Debug.LogException(exception);
            }
            finally
            {
                openAnimationCancellation.Dispose();
            }
        }

        public void CloseTop()
        {
            if (_stack.Count > 0)
                _stack.Peek().Window.TryRequestClose();
        }

        public async UniTask CloseTopAsync()
        {
            if (_stack.Count == 0) return;

            var top = _stack.Peek();
            top.Window.TryRequestClose();
            await top.Closed.Task;
        }

        public void CloseAll()
        {
            BeginCloseAllBatch().Forget(Debug.LogException);
        }

        public UniTask CloseAllAsync() => BeginCloseAllBatch();

        private UniTask BeginCloseAllBatch()
        {
            if (_activeEntries.Count == 0)
                return UniTask.CompletedTask;

            var entries = new List<StackEntry>(_activeEntries);
            var closeTasks = new UniTask[entries.Count];

            if (_closeAllOperationCount++ == 0)
            {
                _isClosingAll = true;
                // 多个窗口并发退场时冻结当前 Mask owner，避免 HashSet 顺序造成闪烁。
                _maskOwner = ResolveMaskOwner();
            }

            for (int i = 0; i < entries.Count; i++)
            {
                entries[i].Window.TryRequestClose();
                closeTasks[i] = entries[i].Closed.Task;
            }

            return WaitForCloseAllBatchAsync(closeTasks);
        }

        private async UniTask WaitForCloseAllBatchAsync(UniTask[] closeTasks)
        {
            try
            {
                await UniTask.WhenAll(closeTasks);
            }
            finally
            {
                _closeAllOperationCount--;
                if (_closeAllOperationCount == 0)
                {
                    _isClosingAll = false;
                    _maskOwner = ResolveMaskOwner();
                    RecalculateOcclusion();
                }
            }
        }

        public void CloseAllImmediately()
        {
            if (_activeEntries.Count == 0) return;

            _maskOwner = null;

            // 仍使用快照，CleanupEntry 会同步修改 _activeEntries。
            // 立即清理用于场景边界，不播放动画，避免旧场景 UI 短暂覆盖新场景。
            var entries = new List<StackEntry>(_activeEntries);
            foreach (var entry in entries)
            {
                // 先完成窗口结果，使正在等待 handle.Closed 的调用方能够正常结束。
                entry.Window.TryRequestClose();

                // TryRequestClose 可能唤醒后台生命周期，并由 CloseEntryAsync 抢先取得关闭权。
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
            // 因而切场景时 CloseAll/CloseAllAsync 可以关闭尚未到期的 Toast。
            Open<CommonToastParam, UIUnit>(
                UIWindowId.CommonToast,
                new CommonToastParam
                {
                    Text = text ?? string.Empty,
                    IconPath = icon,
                    Duration = time
                },
                UILayer.Toast);
        }

        private UIWindowEntry GetValidatedConfig(UIWindowId windowId, UILayer? layerOverride)
        {
            ValidateWindowId(windowId);

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
            if (!Enum.IsDefined(typeof(UIOcclusionMode), config.occlusionMode))
                throw new InvalidOperationException($"[UIManager] Window '{windowId}' 的遮挡策略非法");
            if (!Enum.IsDefined(typeof(UIWindowOpenMode), config.openMode))
                throw new InvalidOperationException($"[UIManager] Window '{windowId}' 的实例策略非法");
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
            if (config.style.maskColor.r < 0f || config.style.maskColor.r > 1f ||
                config.style.maskColor.g < 0f || config.style.maskColor.g > 1f ||
                config.style.maskColor.b < 0f || config.style.maskColor.b > 1f ||
                config.style.maskColor.a < 0f || config.style.maskColor.a > 1f)
                throw new InvalidOperationException($"[UIManager] Window '{windowId}' 的遮罩颜色必须在 0 到 1 之间");
            if (config.occlusionMode == UIOcclusionMode.HideAllBelow &&
                (config.style.frameType != UIFrameType.FullScreen || !config.allowFullOcclusion))
                throw new InvalidOperationException(
                    $"[UIManager] Window '{windowId}' 只有在 FullScreen 且明确声明不透明时才能使用 HideAllBelow");
            return config;
        }

        private StackEntry FindLatestActiveEntry(UIWindowId windowId)
        {
            StackEntry latest = null;
            foreach (var entry in _activeEntries)
            {
                if (entry.WindowId != windowId)
                    continue;
                if (latest == null || entry.OpenSequence > latest.OpenSequence)
                    latest = entry;
            }
            return latest;
        }

        private StackEntry FindLatestReusableEntry(UIWindowId windowId)
        {
            StackEntry latest = null;
            foreach (var entry in _activeEntries)
            {
                if (entry.WindowId != windowId ||
                    entry.Window.IsCloseRequested || entry.Window.IsClosing)
                    continue;
                if (latest == null || entry.OpenSequence > latest.OpenSequence)
                    latest = entry;
            }
            return latest;
        }

        private List<StackEntry> FindActiveEntries(UIWindowId windowId)
        {
            var entries = new List<StackEntry>();
            foreach (var entry in _activeEntries)
            {
                if (entry.WindowId == windowId)
                    entries.Add(entry);
            }
            return entries;
        }

        private static void ValidateWindowId(UIWindowId windowId)
        {
            if (!windowId.IsValid)
                throw new ArgumentOutOfRangeException(nameof(windowId), windowId, "未知的窗口 ID");
        }

        private void ValidateLayerOpenOrder(UIWindowId windowId, UILayer layer)
        {
            // Toast 独立于主窗口栈。主栈只允许同层或向更高层打开，防止视觉上位于
            // 后方的低层窗口成为逻辑栈顶并错误接管输入、ESC 和 Mask。
            if (layer == UILayer.Toast || _stack.Count == 0)
                return;

            StackEntry currentTop = null;
            foreach (var entry in _stack)
            {
                if (!entry.Window.IsCloseRequested && !entry.Window.IsClosing)
                {
                    currentTop = entry;
                    break;
                }
            }
            if (currentTop == null)
                return;

            UILayer currentLayer = currentTop.Layer;
            if ((int)layer < (int)currentLayer)
                throw new InvalidOperationException(
                    $"[UIManager] Window '{windowId}' 不能在活动的 {currentLayer} 窗口上方打开更低的 {layer} Layer");
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
            // CloseAll 批次期间冻结旧 Mask owner；批次之后再从幸存窗口统一推导。
            if (!_isClosingAll && entry.MaskGo != null)
                _maskOwner = entry;
            entry.ApplyInteraction(true);
            RecalculateOcclusion();
        }

        private void BringToTop(StackEntry entry)
        {
            if (entry == null) return;

            if (entry.ParticipatesInWindowStack && entry.IsInStack && !IsTop(entry))
            {
                RemoveEntry(entry);
                Push(entry);
            }

            // Transform 层级与逻辑栈同步：Mask 在最下，输入屏蔽层居中，Frame 在最上。
            entry.MaskGo?.transform.SetAsLastSibling();
            entry.InputBlockerGo?.transform.SetAsLastSibling();
            entry.FrameGo?.transform.SetAsLastSibling();

            if (!entry.ParticipatesInWindowStack)
                entry.ApplyInteraction(true);
            if (!_isClosingAll && entry.MaskGo != null)
                _maskOwner = entry;
            RecalculateOcclusion();
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
                bool wasTop = IsTop(entry);
                entry.ApplyInteraction(false);
                StackEntry transitionBlocker = null;
                if (wasTop)
                {
                    transitionBlocker = entry.InputBlockerGo != null
                        ? entry
                        : FindNextInputBlocker(entry);
                }
                transitionBlocker?.ApplyInputTransitionBlocker();
                // 先完成 Mask 所有权交接，再恢复下层渲染。交接会复制完整 RGBA，
                // 因此同一帧始终只有一个连续颜色的可见遮罩。
                UniTask maskTransition = PrepareMaskForClosing(entry);
                RecalculateOcclusion();

                var frameRect = entry.FrameGo != null
                    ? entry.FrameGo.GetComponent<RectTransform>()
                    : null;
                await UniTask.WhenAll(
                    UIAnimator.PlayCloseAsync(
                        frameRect,
                        entry.CanvasGroup,
                        entry.Style,
                        entry.Window.GetCancellationTokenOnDestroy()),
                    maskTransition);
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

            if (ReferenceEquals(_maskOwner, entry))
                // CloseAll 期间新开的窗口不属于旧批次；旧 owner 清理后允许幸存窗口
                // 接管 Mask，避免其在旧批次完全结束前短暂失去应有的视觉遮罩。
                _maskOwner = FindNextMaskOwner(entry);

            bool wasTop = entry.IsInStack && IsTop(entry);
            if (entry.IsInStack)
            {
                RemoveEntry(entry);
                entry.IsInStack = false;
            }

            // 不依赖“只恢复前一个窗口”的增量状态；任意关闭顺序都从完整栈重新推导。
            RecalculateOcclusion();

            if (entry.MaskGo != null) UnityEngine.Object.Destroy(entry.MaskGo);
            if (entry.InputBlockerGo != null)
                UnityEngine.Object.Destroy(entry.InputBlockerGo);
            if (entry.FrameGo != null) UnityEngine.Object.Destroy(entry.FrameGo);

            if (wasTop && _stack.Count > 0)
                _stack.Peek().ApplyInteraction(true);

            // Closed 必须最后完成，确保所有等待方恢复时节点已销毁、栈状态已恢复。
            entry.Closed.TrySetResult(UIUnit.Default);
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

        private void RecalculateOcclusion()
        {
            if (_stack.Count == 0)
            {
                _maskOwner = null;
                return;
            }

            // Stack 枚举顺序为栈顶到栈底。只有动画完成、未开始关闭且自身可见的窗口
            // 才能成为遮挡源；这样开场/退场动画不会露出透明空洞。
            _occlusionBuffer.Clear();
            var occluders = _occlusionBuffer;
            if (!_isClosingAll && (_maskOwner == null || !_maskOwner.IsInStack ||
                                   _maskOwner.MaskGo == null))
                _maskOwner = ResolveMaskOwner();
            foreach (var entry in _stack)
            {
                bool cullFrame = false;
                // Mask 不参与叠加：最上方有效窗口独占遮罩。开场时新窗口立即接管；
                // 退场开始后则交还给下方窗口，没有下方 Mask 时保留自身完成淡出。
                bool cullMask = entry.MaskGo != null &&
                                !ReferenceEquals(entry, _maskOwner);

                for (int i = 0; i < occluders.Count; i++)
                {
                    var occluder = occluders[i];
                    if (!IsVisuallyAbove(occluder, entry))
                        continue;

                    if (occluder.OcclusionMode == UIOcclusionMode.HideAllBelow)
                    {
                        cullFrame = true;
                        cullMask = true;
                        break;
                    }

                    if (occluder.OcclusionMode == UIOcclusionMode.HideFullyCovered &&
                        FullyCovers(occluder.Frame.OcclusionRect,
                            entry.FrameGo != null
                                ? entry.FrameGo.GetComponent<RectTransform>()
                                : null))
                    {
                        // Frame 被完全覆盖时，它自己的 Mask 也必须一并裁剪；否则多级
                        // Dialog 会叠加暗度，既改变视觉结果，也保留了不必要的 Draw Call。
                        cullFrame = true;
                        cullMask = true;
                    }
                }

                entry.ApplyRenderCulling(cullFrame, cullMask);

                bool canOcclude = !cullFrame && entry.OpenAnimationComplete &&
                                  !entry.Window.IsCloseRequested && !entry.Window.IsClosing &&
                                  entry.OcclusionMode != UIOcclusionMode.KeepVisible;
                if (canOcclude)
                    occluders.Add(entry);
            }
        }

        private StackEntry ResolveMaskOwner()
        {
            foreach (var entry in _stack)
            {
                if (entry.MaskGo == null || entry.Window.IsClosing)
                    continue;
                return entry;
            }
            return null;
        }

        private StackEntry FindNextMaskOwner(StackEntry excluding)
        {
            _maskSearchBuffer.Clear();
            var occluders = _maskSearchBuffer;
            foreach (var entry in _stack)
            {
                if (ReferenceEquals(entry, excluding) || entry.Window.IsClosing)
                    continue;

                if (entry.MaskGo != null)
                {
                    bool hidden = false;
                    for (int i = 0; i < occluders.Count; i++)
                    {
                        var occluder = occluders[i];
                        if (occluder.OcclusionMode == UIOcclusionMode.HideAllBelow ||
                            (occluder.OcclusionMode == UIOcclusionMode.HideFullyCovered &&
                             FullyCovers(occluder.Frame.OcclusionRect,
                                 entry.FrameGo != null
                                     ? entry.FrameGo.GetComponent<RectTransform>()
                                     : null)))
                        {
                            hidden = true;
                            break;
                        }
                    }
                    if (!hidden)
                        return entry;
                }

                if (entry.OpenAnimationComplete &&
                    entry.OcclusionMode != UIOcclusionMode.KeepVisible)
                    occluders.Add(entry);
            }
            return null;
        }

        private StackEntry FindNextInputBlocker(StackEntry excluding)
        {
            foreach (var entry in _stack)
            {
                if (ReferenceEquals(entry, excluding) || entry.Window.IsClosing ||
                    entry.InputBlockerGo == null)
                    continue;
                return entry;
            }
            return null;
        }

        private UniTask PrepareMaskForClosing(StackEntry entry)
        {
            if (entry.MaskGo == null || !ReferenceEquals(_maskOwner, entry))
                return UniTask.CompletedTask;

            CancellationToken token = entry.Window.GetCancellationTokenOnDestroy();
            if (_isClosingAll)
            {
                Color transparent = GetMaskColor(entry.MaskGo, entry.Style.maskColor);
                transparent.a = 0f;
                return UIAnimator.AnimateMaskColorAsync(
                    entry.MaskGo, transparent, entry.Style.closeDuration, token);
            }

            StackEntry nextOwner = FindNextMaskOwner(entry);
            if (nextOwner == null)
            {
                Color transparent = GetMaskColor(entry.MaskGo, entry.Style.maskColor);
                transparent.a = 0f;
                return UIAnimator.AnimateMaskColorAsync(
                    entry.MaskGo, transparent, entry.Style.closeDuration, token);
            }

            Color handoffColor = GetMaskColor(entry.MaskGo, nextOwner.Style.maskColor);
            if (nextOwner.MaskGo.TryGetComponent<Image>(out var nextImage))
                nextImage.color = handoffColor;
            _maskOwner = nextOwner;
            return UIAnimator.AnimateMaskColorAsync(
                nextOwner.MaskGo,
                nextOwner.Style.maskColor,
                entry.Style.closeDuration,
                nextOwner.Window.GetCancellationTokenOnDestroy());
        }

        private Color GetVisibleMaskColor(Color fallbackTarget)
        {
            if (_maskOwner?.MaskGo != null && !_maskOwner.IsMaskCulled)
                return GetMaskColor(_maskOwner.MaskGo, fallbackTarget);

            Color transparent = fallbackTarget;
            transparent.a = 0f;
            return transparent;
        }

        private static Color GetMaskColor(GameObject maskGo, Color fallback)
        {
            return maskGo != null && maskGo.TryGetComponent<Image>(out var image)
                ? image.color
                : fallback;
        }

        private static bool IsVisuallyAbove(StackEntry upper, StackEntry lower)
        {
            if (upper?.FrameGo == null || lower?.FrameGo == null)
                return false;

            Transform upperLayer = upper.FrameGo.transform.parent;
            Transform lowerLayer = lower.FrameGo.transform.parent;
            if (upperLayer == null || lowerLayer == null)
                return false;

            if (ReferenceEquals(upperLayer, lowerLayer))
                return upper.FrameGo.transform.GetSiblingIndex() >
                       lower.FrameGo.transform.GetSiblingIndex();

            // Layer 根节点必须处于同一父节点下才可安全比较；结构异常时保守地不裁剪。
            if (!ReferenceEquals(upperLayer.parent, lowerLayer.parent))
                return false;
            return upperLayer.GetSiblingIndex() > lowerLayer.GetSiblingIndex();
        }

        private bool FullyCovers(RectTransform opaqueRect, RectTransform targetRect)
        {
            if (opaqueRect == null || targetRect == null ||
                !opaqueRect.gameObject.activeInHierarchy || !targetRect.gameObject.activeInHierarchy)
                return false;

            targetRect.GetWorldCorners(_worldCorners);
            Rect localOpaqueRect = opaqueRect.rect;
            const float epsilon = 0.5f;
            localOpaqueRect.xMin += epsilon;
            localOpaqueRect.xMax -= epsilon;
            localOpaqueRect.yMin += epsilon;
            localOpaqueRect.yMax -= epsilon;

            for (int i = 0; i < _worldCorners.Length; i++)
            {
                Vector3 localPoint = opaqueRect.InverseTransformPoint(_worldCorners[i]);
                if (!localOpaqueRect.Contains(new Vector2(localPoint.x, localPoint.y)))
                    return false;
            }

            return true;
        }
    }
}
