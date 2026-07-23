using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.UISystem.Example
{
    /// <summary>
    /// 测试场景入口：每个按钮固定打开一个窗口用例。
    /// 本类故意不使用 UIManager.Instance 兜底，用于验证场景对象的 VContainer 注入链路；
    /// 如果注入失败，Start 会明确报错并停止绑定按钮。
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
            // 不使用静态单例兜底，避免测试场景掩盖注入配置或时序错误。
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
        }

        private async UniTaskVoid OpenConfirm()
        {
            SetResult("Dialog · 删除确认：等待选择");
            bool confirmed = await _ui.OpenAsync<ConfirmWindow, ConfirmParam, bool>(
                UIWindowId.ConfirmWindow,
                new ConfirmParam
                {
                    Title = "删除确认",
                    Message = "确定要删除这条测试存档吗？\n此操作仅用于验证弹窗返回值。",
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
                    Message = "这是无底框窗口的参数与完成回调测试。",
                    OkText = "明白了"
                });
            SetResult("None · 新手提示：已关闭");
        }

        private async UniTaskVoid OpenFrame(UIWindowId windowId, string label)
        {
            SetResult(label + "：已打开");
            await _ui.OpenAsync<FrameTestWindow>(windowId);
            SetResult(label + "：已关闭");
        }

        private void ShowCommonToast()
        {
            _ui.ShowToast("这是一条不带图标的 Common Toast", null, ToastDuration.Normal);
            SetResult("Toast · CommonToast：已显示，将自动关闭");
        }

        private void SetResult(string value)
        {
            if (resultText != null)
                resultText.text = value;
        }
    }
}
