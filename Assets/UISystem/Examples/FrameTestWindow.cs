using System;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UISystem.Example
{
    /// <summary>三种 Frame 创建测试共用的轻量窗口脚本。</summary>
    public class FrameTestWindow : UIWindow
    {
        [SerializeField] private string windowTitle = "Frame Test";
        [SerializeField] private Button closeButton;
        [SerializeField] private Button secondaryButton;
        [SerializeField] private TMP_Text secondaryButtonLabel;

        private Action _secondaryAction;
        private RectTransform _messageRect;
        private Vector2 _messageOffsetMin;

        protected override void OnInit(Unit _)
        {
            SetTitle(windowTitle);
            if (closeButton != null)
                closeButton.onClick.AddListener(HandleClose);
            EnsureSecondaryButton();
            if (secondaryButton != null)
            {
                secondaryButton.gameObject.SetActive(false);
                secondaryButton.onClick.AddListener(HandleSecondaryAction);
            }
        }

        public void ConfigureSecondaryAction(string label, Action action)
        {
            _secondaryAction = action;
            if (secondaryButtonLabel != null)
                secondaryButtonLabel.text = label ?? string.Empty;
            if (secondaryButton != null)
                secondaryButton.gameObject.SetActive(action != null);
            if (_messageRect != null)
            {
                var offset = _messageOffsetMin;
                if (action != null)
                    offset.y = Mathf.Max(offset.y, 68f);
                _messageRect.offsetMin = offset;
            }
        }

        private void EnsureSecondaryButton()
        {
            if (secondaryButton != null)
                return;

            var sourceText = GetComponentInChildren<TMP_Text>(true);
            if (sourceText != null)
            {
                _messageRect = sourceText.rectTransform;
                _messageOffsetMin = _messageRect.offsetMin;
            }

            var buttonGo = new GameObject(
                "OpenSecondaryButton",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonGo.transform.SetParent(transform, false);
            var buttonRect = (RectTransform)buttonGo.transform;
            buttonRect.anchorMin = new Vector2(0.5f, 0f);
            buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.anchoredPosition = new Vector2(0f, 14f);
            buttonRect.sizeDelta = new Vector2(240f, 44f);

            var image = buttonGo.GetComponent<Image>();
            image.color = new Color(0.12f, 0.48f, 0.9f, 1f);
            secondaryButton = buttonGo.GetComponent<Button>();

            var labelGo = new GameObject(
                "Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(buttonGo.transform, false);
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10f, 4f);
            labelRect.offsetMax = new Vector2(-10f, -4f);

            var label = labelGo.GetComponent<TextMeshProUGUI>();
            if (sourceText != null)
            {
                label.font = sourceText.font;
                label.fontSharedMaterial = sourceText.fontSharedMaterial;
            }
            label.fontSize = 18f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            secondaryButtonLabel = label;
        }

        private void OnDestroy()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(HandleClose);
            if (secondaryButton != null)
                secondaryButton.onClick.RemoveListener(HandleSecondaryAction);
            _secondaryAction = null;
        }

        private void HandleClose() => Close();
        private void HandleSecondaryAction() => _secondaryAction?.Invoke();
    }
}
