using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.UISystem.Example
{
    /// <summary>
    /// Demo 场景入口：每个按钮展示一种窗口调用方式。
    /// 本类使用 VContainer 注入 IUIManager，业务代码可以沿用相同写法。
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

        [Header("Window Stack")]
        [SerializeField] private Button dialogOcclusionButton;
        [SerializeField] private Button reverseDialogOcclusionButton;
        [SerializeField] private Button fullScreenOcclusionButton;

        [SerializeField] private TMP_Text resultText;

        private IUIManager _ui;

        [Inject]
        private void Construct(IUIManager uiManager)
        {
            // 初始场景由 UISystemScope.Awake 扫描注入；后续场景由 sceneLoaded 注入。
            // 对后续加载场景，Construct 保证在 Start 前调用，但不会早于 Awake/OnEnable。
            _ui = uiManager;
        }

        private void Start()
        {
            // 场景组件从 Start 起即可安全使用注入完成的 IUIManager。
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
                () => OpenFrame(UIWindowId.FullScreenInfoTest, "FullScreen · 信息页").Forget());
            fullScreenListButton.onClick.AddListener(
                () => OpenFrame(UIWindowId.FullScreenListTest, "FullScreen · 列表页").Forget());

            tipsButton.onClick.AddListener(() => OpenTips().Forget());
            noneToastButton.onClick.AddListener(
                () => OpenFrame(UIWindowId.NoneToastTest, "None · 轻提示").Forget());
            noneLoadingButton.onClick.AddListener(
                () => OpenFrame(UIWindowId.NoneLoadingTest, "None · 加载提示").Forget());
            commonToastButton.onClick.AddListener(ShowCommonToast);
            dialogOcclusionButton.onClick.AddListener(
                () => OpenDialogStackExample(largeOverSmall: true).Forget(Debug.LogException));
            reverseDialogOcclusionButton.onClick.AddListener(
                () => OpenDialogStackExample(largeOverSmall: false).Forget(Debug.LogException));
            fullScreenOcclusionButton.onClick.AddListener(
                () => OpenFullScreenStackExample().Forget(Debug.LogException));
        }

        private async UniTaskVoid OpenConfirm()
        {
            SetResult("Dialog · 删除确认：等待选择");
            bool confirmed = await _ui.OpenAsync<ConfirmWindow, ConfirmParam, bool>(
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
                    Message = "这个无底框窗口演示参数传入与完成回调。",
                    OkText = "明白了"
                });
            SetResult("None · 新手提示：已关闭");
        }

        private async UniTaskVoid OpenFrame(UIWindowId windowId, string label)
        {
            SetResult(label + "：已打开");
            await _ui.OpenAsync<FrameTestWindow, FrameWindowParam, Unit>(windowId, null);
            SetResult(label + "：已关闭");
        }

        private void ShowCommonToast()
        {
            _ui.ShowToast("这是一条不带图标的 Common Toast", null, ToastDuration.Normal);
            SetResult("Toast · CommonToast：已显示，将自动关闭");
        }

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

        private UniTask OpenFullScreenStackExample()
        {
            return OpenStackExample(
                UIWindowId.DialogCompactTest,
                UIWindowId.FullScreenListTest,
                "全屏窗口覆盖 Dialog",
                "打开全屏窗口");
        }

        private async UniTask OpenStackExample(
            UIWindowId primaryId,
            UIWindowId secondaryId,
            string label,
            string buttonLabel)
        {
            await _ui.CloseAllAsync();
            await UniTask.NextFrame();

            SetResult($"窗口叠加示例 · {label}：请点击一级窗口内的按钮");
            await _ui.OpenAsync<FrameTestWindow, FrameWindowParam, Unit>(
                primaryId,
                new FrameWindowParam
                {
                    SecondaryButtonLabel = buttonLabel,
                    SecondaryAction = () => OpenFrame(secondaryId, label).Forget()
                });
            SetResult($"窗口叠加示例 · {label}：一级窗口已关闭");
        }

        private void SetResult(string value)
        {
            if (resultText != null)
                resultText.text = value;
        }
    }
}
