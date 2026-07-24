using Cysharp.Threading.Tasks;
using R3;
using UnityEngine.InputSystem;
using UnityEngine;
using System;

namespace Game.UISystem
{
    public abstract class UIWindowBase : MonoBehaviour
    {
        internal abstract bool IsCloseRequested { get; }
        internal abstract bool IsClosing { get; }
        internal abstract bool TryRequestClose();
        internal abstract void NotifyOpened();
        internal abstract void NotifyClosing();
        internal abstract void MarkClosing();
    }

    /// <summary>
    /// 无参无返回值的简化基类
    /// </summary>
    public abstract class UIWindow : UIWindow<R3.Unit, R3.Unit>
    {
        protected void Close() => Complete(R3.Unit.Default);
    }

    /// <summary>
    /// 所有窗口内容脚本的泛型基类，挂在“内容 Prefab”的根节点上。
    /// TParam 是打开窗口时传入的数据，TResult 是窗口关闭时返回给调用方的结果。
    ///
    /// 生命周期函数由 UIManager 驱动，调用顺序为：
    /// Setup → OnInit → 开场动画 → OnOpened → 等待关闭请求 → OnClosing → 退场动画 → Destroy。
    /// 子类不要自行实现 Start/Awake 来替代这些回调，否则可能早于参数绑定或容器注入。
    /// </summary>
    public abstract class UIWindow<TParam, TResult> : UIWindowBase
    {
        /// <summary>当前窗口的底框，可访问底框上的组件</summary>
        private UIWindowFrame Frame { get; set; }

        // 同一个完成源同时承担“向调用方返回结果”和“通知 UIManager 开始关闭”的职责。
        private UniTaskCompletionSource<TResult> _tcs;
        private bool _closeOnEsc;
        private Func<bool> _isTop;
        private Action _onCloseRequested;
        private bool _closeRequested;
        private bool _isClosing;

        // ── 由 UIManager 调用，子类勿手动调用 ────────────────────────

        internal void Setup(
            TParam param,
            UniTaskCompletionSource<TResult> tcs,
            UIWindowFrame frame,
            bool closeOnEsc,
            Func<bool> isTop,
            Action onCloseRequested)
        {
            // 必须先保存全部基础设施引用，再进入业务 OnInit；这样 OnInit 内立即调用
            // Complete 也能正确完成结果并取消仍未开始/完成的开场动画。
            Frame  = frame;
            _tcs   = tcs;
            _closeOnEsc = closeOnEsc;
            _isTop = isTop;
            _onCloseRequested = onCloseRequested;

            // 公共 Frame 的关闭按钮统一走 TryRequestClose，避免业务窗口重复绑定。
            frame.SetCloseAction(() => TryRequestClose());

            OnInit(param);
        }

        // ── 子类覆写 ──────────────────────────────────────────────────

        /// <summary>
        /// 初始化窗口内容，开场动画前调用。此时参数、Frame 和依赖注入均已准备完成，
        /// 适合设置文本、绑定按钮和根据参数刷新首屏。
        /// </summary>
        protected virtual void OnInit(TParam param) { }

        /// <summary>开场动画结束后调用，适合启动计时器、焦点或只应在完全显示后执行的逻辑。</summary>
        protected virtual void OnOpened() { }

        /// <summary>
        /// Single 窗口在实例未关闭时被再次打开后调用。
        /// 适合使用新参数刷新内容；不要在此重复绑定只需初始化一次的事件。
        /// </summary>
        protected virtual void OnReopen(TParam param) { }

        /// <summary>关闭动画开始前调用，适合解绑业务监听；不要在这里再次触发关闭。</summary>
        protected virtual void OnClosing() { }

        // ── 子类 API ──────────────────────────────────────────────────

        /// <summary>关闭窗口并返回结果</summary>
        protected void Complete(TResult result)
        {
            TryComplete(result);
        }

        /// <summary>设置底框标题文字</summary>
        protected void SetTitle(string title) => Frame?.SetTitle(title);

        // ── ESC / 返回键 ──────────────────────────────────────────────

        // Unity 通过反射调用 Update，必须是 private（或 protected private）
        // 子类如需 ESC 逻辑请覆写 OnUpdate() 而非 Update()
        private void Update()
        {
            if (!_closeRequested && !_isClosing &&
                _closeOnEsc &&
                (_isTop?.Invoke() ?? false) &&
                Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            {
                TryRequestClose();
            }

            if (!_closeRequested && !_isClosing)
                OnUpdate();
        }

        /// <summary>子类需要每帧逻辑时覆写此方法，不要覆写 Update()</summary>
        protected virtual void OnUpdate() { }

        // ── 内部回调，由 UIManager 触发 ───────────────────────────────

        private bool TryComplete(TResult result)
        {
            // 关闭请求可能来自多个入口。_closeRequested 和 _isClosing 共同保证结果只提交一次。
            if (_tcs == null || _closeRequested || _isClosing)
                return false;

            // 先置位再完成 TCS，防止等待方同步恢复后重入并再次提交结果。
            _closeRequested = true;
            if (!_tcs.TrySetResult(result))
            {
                _closeRequested = false;
                return false;
            }

            // 通知 UIManager 取消尚未完成的开场动画；真正的退场和 Destroy 仍由 UIManager 负责。
            _onCloseRequested?.Invoke();
            return true;
        }

        internal override bool TryRequestClose() => TryComplete(default);
        internal void NotifyReopened(TParam param) => OnReopen(param);
        internal override void NotifyOpened()  => OnOpened();
        internal override void NotifyClosing() => OnClosing();
        internal override void MarkClosing()   => _isClosing = true;
        internal override bool IsCloseRequested => _closeRequested;
        internal override bool IsClosing => _isClosing;
    }
}
