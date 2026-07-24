using System;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UISystem.Example
{
    public sealed class FrameWindowParam
    {
        public string SecondaryButtonLabel;
        public Action SecondaryAction;
    }

    /// <summary>三种 Frame 示例共用的轻量窗口脚本。</summary>
    public class FrameTestWindow : UIWindow<FrameWindowParam, Unit>
    {
        [SerializeField] private string windowTitle = "Frame Test";
        [SerializeField] private Button closeButton;

        private Button _secondaryButton;
        private Action _secondaryAction;

        protected override void OnInit(FrameWindowParam param)
        {
            SetTitle(windowTitle);
            if (closeButton != null)
                closeButton.onClick.AddListener(HandleClose);

            if (param?.SecondaryAction == null)
                return;

            _secondaryAction = param.SecondaryAction;
            _secondaryButton = CreateSecondaryButton(param.SecondaryButtonLabel);
            _secondaryButton.onClick.AddListener(HandleSecondaryAction);
        }

        private Button CreateSecondaryButton(string buttonLabel)
        {
            var sourceText = GetComponentInChildren<TMP_Text>(true);
            if (sourceText != null)
                sourceText.rectTransform.offsetMin = new Vector2(
                    sourceText.rectTransform.offsetMin.x,
                    Mathf.Max(sourceText.rectTransform.offsetMin.y, 68f));

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

            buttonGo.GetComponent<Image>().color = new Color(0.12f, 0.48f, 0.9f, 1f);
            var button = buttonGo.GetComponent<Button>();

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
            label.richText = false;
            label.text = UITextSafety.NormalizePlainText(buttonLabel, 40);
            label.fontSize = 18f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;

            return button;
        }

        private void OnDestroy()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(HandleClose);
            if (_secondaryButton != null)
                _secondaryButton.onClick.RemoveListener(HandleSecondaryAction);
            _secondaryAction = null;
        }

        private void HandleClose() => Complete(Unit.Default);
        private void HandleSecondaryAction() => _secondaryAction?.Invoke();
    }
}
