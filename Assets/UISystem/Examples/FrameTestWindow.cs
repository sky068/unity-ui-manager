using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UISystem.Example
{
    /// <summary>三种 Frame 创建测试共用的轻量窗口脚本。</summary>
    public class FrameTestWindow : UIWindow
    {
        [SerializeField] private string windowTitle = "Frame Test";
        [SerializeField] private Button closeButton;

        protected override void OnInit(Unit _)
        {
            SetTitle(windowTitle);
            if (closeButton != null)
                closeButton.onClick.AddListener(HandleClose);
        }

        private void OnDestroy()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(HandleClose);
        }

        private void HandleClose() => Close();
    }
}
