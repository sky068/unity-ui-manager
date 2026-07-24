using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using System.Threading;

namespace Game.UISystem
{
    internal sealed class UIMaskTransitionState : MonoBehaviour
    {
        public int Revision { get; private set; }

        public int BeginTransition() => ++Revision;
    }

    /// <summary>
    /// 负责窗口开关动画和遮罩，与业务逻辑解耦
    /// </summary>
    internal static class UIAnimator
    {
        // ── 遮罩 ─────────────────────────────────────────────────────

        public static GameObject CreateMask(
            Transform parent,
            Color initialColor)
        {
            return CreateMaskWithColor(parent, initialColor);
        }

        /// <summary>用指定初始颜色创建遮罩；后续动画从该颜色的 alpha 开始。</summary>
        public static GameObject CreateMaskWithColor(
            Transform parent,
            Color targetColor)
        {
            var go = new GameObject("__Mask__");
            go.transform.SetParent(parent, false);

            var rt       = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img   = go.AddComponent<Image>();
            img.color = targetColor;
            img.raycastTarget = false;
            go.AddComponent<UIMaskTransitionState>();

            return go;
        }

        /// <summary>创建不参与渲染表现的全屏输入屏蔽层。</summary>
        public static GameObject CreateInputBlocker(
            Transform parent,
            System.Action onOutsideClick)
        {
            var go = new GameObject("__InputBlocker__");
            go.transform.SetParent(parent, false);

            var rt       = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = Color.clear;
            img.raycastTarget = true;

            if (onOutsideClick != null)
            {
                var btn        = go.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => onOutsideClick());
            }

            return go;
        }

        public static async UniTask AnimateMaskColorAsync(
            GameObject maskGo, Color to, float duration, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (maskGo == null) return;
            var img     = maskGo.GetComponent<Image>();
            if (img == null) return;
            var state   = maskGo.GetOrAddComponent<UIMaskTransitionState>();
            int revision = state.BeginTransition();
            Color from  = img.color;
            float elapsed = 0f;

            if (duration <= 0f)
            {
                img.color = to;
                return;
            }

            while (elapsed < duration && maskGo != null && img != null &&
                   state != null && state.Revision == revision)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                img.color = Color.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            if (img != null && state != null && state.Revision == revision)
                img.color = to;
        }

        // ── 窗口动画 ─────────────────────────────────────────────────

        public static async UniTask PlayOpenAsync(
            RectTransform target, CanvasGroup cg, UIWindowStyle style,
            CancellationToken cancellationToken)
        {
            if (target == null || cg == null || style == null) return;

            // 动画开始前统一校正 Frame 锚点：全屏窗口铺满 Canvas，其他窗口以屏幕中心为基准。
            ApplySize(target, style);

            // 动画期间关闭交互，避免用户在半透明或缩放尚未完成时重复点击。
            cg.alpha          = style.animationType == WindowAnimationType.Scale ? 1f : 0f;
            cg.interactable   = false;
            cg.blocksRaycasts = false;

            await PlayAsync(target, cg, style, isOpen: true, cancellationToken);

        }

        public static async UniTask PlayCloseAsync(
            RectTransform target, CanvasGroup cg, UIWindowStyle style,
            CancellationToken cancellationToken)
        {
            if (target == null || cg == null || style == null) return;
            cg.interactable   = false;
            cg.blocksRaycasts = false;

            await PlayAsync(target, cg, style, isOpen: false, cancellationToken);
        }

        // ── 内部 ─────────────────────────────────────────────────────

        private static async UniTask PlayAsync(
            RectTransform target, CanvasGroup cg, UIWindowStyle style, bool isOpen,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (target == null || cg == null || style == null)
                return;

            if (style.animationType == WindowAnimationType.None)
            {
                cg.alpha = isOpen ? 1f : 0f;
                return;
            }

            // 同一套插值循环同时支持淡入淡出、缩放和 Toast 纵向滑动，
            // 由 Style 决定实际启用哪些通道，避免各窗口自行维护动画协程。
            float duration  = isOpen ? style.openDuration  : style.closeDuration;
            float fromAlpha = isOpen ? 0f : cg.alpha;
            float toAlpha   = isOpen ? 1f : 0f;
            float fromScale = isOpen ? style.scaleFrom : target.localScale.x;
            float toScale   = isOpen ? 1f : style.scaleFrom;
            // basePosition 是该窗口当前应处于的布局位置。Toast 打开时从其下方滑入，
            // 关闭时从当前位置向上滑出；因此即使以后改变中心基准，也不需要改动画公式。
            Vector2 basePosition = target.anchoredPosition;
            Vector2 fromPosition = isOpen
                ? basePosition + Vector2.down * style.slideDistance
                : basePosition;
            Vector2 toPosition = isOpen
                ? basePosition
                : basePosition + Vector2.up * style.slideDistance;

            bool doFade  = style.animationType == WindowAnimationType.Fade
                        || style.animationType == WindowAnimationType.FadeAndScale
                        || style.animationType == WindowAnimationType.ToastSlide;
            bool doScale = style.animationType == WindowAnimationType.Scale
                        || style.animationType == WindowAnimationType.FadeAndScale;
            bool doSlide = style.animationType == WindowAnimationType.ToastSlide;

            cg.alpha = doFade ? fromAlpha : 1f;
            if (doScale)
                target.localScale = Vector3.one * fromScale;
            if (doSlide)
                target.anchoredPosition = fromPosition;

            if (duration <= 0f)
            {
                cg.alpha = doFade ? toAlpha : 1f;
                if (doScale) target.localScale = Vector3.one * toScale;
                if (doSlide) target.anchoredPosition = toPosition;
                return;
            }

            float elapsed = 0f;
            while (elapsed < duration && target != null && cg != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                float t    = Mathf.Clamp01(elapsed / duration);
                // Toast 关闭采用缓入缓出，让较长退场距离保持连贯；
                // 其他动画使用快速响应的四次缓出曲线。
                float ease = doSlide && !isOpen
                    ? EaseInOutCubic(t)
                    : EaseOutQuart(t);

                if (doFade)
                    cg.alpha = Mathf.Lerp(fromAlpha, toAlpha, ease);

                if (doScale)
                {
                    float s = Mathf.Lerp(fromScale, toScale, ease);
                    target.localScale = new Vector3(s, s, 1f);
                }

                if (doSlide)
                    target.anchoredPosition = Vector2.Lerp(fromPosition, toPosition, ease);

                // 使用 unscaledDeltaTime 和 Update 时序，游戏暂停或 Time.timeScale=0 时 UI 仍能退场。
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            if (cg != null)
                cg.alpha = doFade ? toAlpha : 1f;
            if (doScale && target != null)
                target.localScale = Vector3.one * toScale;
            if (doSlide && target != null)
                target.anchoredPosition = toPosition;
        }

        private static void ApplySize(RectTransform rt, UIWindowStyle style)
        {
            if (style.frameType == UIFrameType.FullScreen)
            {
                rt.anchorMin        = Vector2.zero;
                rt.anchorMax        = Vector2.one;
                rt.offsetMin        = Vector2.zero;
                rt.offsetMax        = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;
            }
            else
            {
                rt.anchorMin        = new Vector2(0.5f, 0.5f);
                rt.anchorMax        = new Vector2(0.5f, 0.5f);
                rt.pivot            = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
            }
        }

        private static float EaseOutQuart(float t) =>
            1f - Mathf.Pow(1f - t, 4f);

        private static float EaseInOutCubic(float t) =>
            t < 0.5f
                ? 4f * t * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
    }

    // ── GameObject 扩展 ───────────────────────────────────────────────

    internal static class GOExtensions
    {
        public static T GetOrAddComponent<T>(this GameObject go) where T : Component =>
            go.TryGetComponent<T>(out var c) ? c : go.AddComponent<T>();
    }
}
