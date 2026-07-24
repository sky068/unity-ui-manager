using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UISystem
{
    /// <summary>
    /// 挂在底框 Prefab 根节点上，暴露 ContentRoot 供内容挂入
    ///
    /// 底框 Prefab 推荐结构：
    ///  FrameRoot (UIWindowFrame)
    ///   ├── Background (Image)
    ///   ├── TitleBar
    ///   │    ├── TitleText (TextMeshProUGUI)
    ///   │    └── CloseButton (Button)
    ///   └── FrameContent  ← contentRoot；运行时适配 WindowContent 的 preferred size
    ///        └── WindowContent  ← 提供 preferred size，随后零偏移铺满 FrameContent
    /// </summary>
    public class UIWindowFrame : MonoBehaviour
    {
        [Tooltip("内容区域，内容 Prefab 会作为它的子节点加载")]
        [SerializeField] private RectTransform contentRoot;

        [Tooltip("标题文本（可选）")]
        [SerializeField] private TMP_Text titleText;

        [Tooltip("关闭按钮（可选）")]
        [SerializeField] private Button closeButton;

        [Tooltip("该 Prefab 对应的框架类型")]
        [SerializeField] private UIFrameType frameType = UIFrameType.Dialog;

        [Tooltip("可确认完全不透明的矩形区域。HideFullyCovered 只使用此区域；为空时不会裁剪下层窗口")]
        [SerializeField] private RectTransform occlusionRect;

        [Tooltip("无法获取内容 preferred size 时的回退窗口尺寸")]
        [SerializeField] private Vector2 minSize = new Vector2(320f, 160f);

        [Tooltip("自适应窗口的最大尺寸")]
        [SerializeField] private Vector2 maxSize = new Vector2(1200f, 900f);

        public RectTransform ContentRoot => contentRoot;
        public UIFrameType FrameType => frameType;
        public RectTransform OcclusionRect => occlusionRect;

        public void PrepareContent(RectTransform content)
        {
            if (contentRoot == null || content == null)
                return;

            if (frameType == UIFrameType.FullScreen)
            {
                Stretch(content);
                return;
            }

            if (frameType == UIFrameType.Dialog)
            {
                content.localScale = Vector3.one;
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
                if (transform is RectTransform dialogRect)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(dialogRect);
                return;
            }

            // None 模式没有公共 Layout，由内容 preferred size 驱动容器和 Frame。
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            float preferredWidth = Mathf.Max(LayoutUtility.GetPreferredWidth(content), content.sizeDelta.x);
            float preferredHeight = Mathf.Max(LayoutUtility.GetPreferredHeight(content), content.sizeDelta.y);

            var frameRect = transform as RectTransform;
            if (frameRect != null)
            {
                // Prefab 中 Frame 与 FrameContent 的初始尺寸差定义边框和标题占用。
                // 运行时先让 FrameContent 适配业务内容，再据此调整外层 Frame。
                float horizontalInset = Mathf.Max(0f, frameRect.rect.width - contentRoot.rect.width);
                float verticalInset = Mathf.Max(0f, frameRect.rect.height - contentRoot.rect.height);
                Vector2 contentPosition = contentRoot.anchoredPosition;

                if (preferredWidth <= 0f)
                    preferredWidth = Mathf.Max(0f, minSize.x - horizontalInset);
                if (preferredHeight <= 0f)
                    preferredHeight = Mathf.Max(0f, minSize.y - verticalInset);

                contentRoot.anchorMin = new Vector2(0.5f, 0.5f);
                contentRoot.anchorMax = new Vector2(0.5f, 0.5f);
                contentRoot.pivot = new Vector2(0.5f, 0.5f);
                contentRoot.anchoredPosition = contentPosition;

                float width = preferredWidth + horizontalInset;
                float height = preferredHeight + verticalInset;
                float frameWidth = Mathf.Min(width, Mathf.Max(0f, maxSize.x));
                float frameHeight = Mathf.Min(height, Mathf.Max(0f, maxSize.y));
                frameRect.sizeDelta = new Vector2(frameWidth, frameHeight);
                contentRoot.sizeDelta = new Vector2(
                    Mathf.Min(preferredWidth, Mathf.Max(0f, frameWidth - horizontalInset)),
                    Mathf.Min(preferredHeight, Mathf.Max(0f, frameHeight - verticalInset)));
            }

            Stretch(content);
        }

        public void SetTitle(string title)
        {
            if (titleText != null)
                titleText.text = title;
        }

        public void SetCloseAction(System.Action onClose)
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(() => onClose?.Invoke());
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.anchoredPosition = Vector2.zero;
        }

        private void Reset()
        {
            var content = transform.Find("FrameContent");
            if (content != null)
                contentRoot = content as RectTransform;
        }
    }
}
