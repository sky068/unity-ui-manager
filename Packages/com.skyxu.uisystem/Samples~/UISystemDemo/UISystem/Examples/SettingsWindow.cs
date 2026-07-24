using UnityEngine;
using UnityEngine.UI;

namespace Game.UISystem.Example
{
    public class SettingsWindow : UIWindow
    {
        [SerializeField] Slider musicSlider;

        protected override void OnInit(UIUnit _)
        {
            SetTitle("设置");

            if (musicSlider != null)
                musicSlider.onValueChanged.AddListener(
                    v => UnityEngine.Debug.Log($"[Settings] 音乐音量: {v:F2}"));
        }

        protected override void OnOpened()  => Debug.Log("[SettingsWindow] 已打开");
        protected override void OnReopen(UIUnit _) =>
            Debug.Log("[SettingsWindow] Single 实例已复用");
        protected override void OnClosing() => Debug.Log("[SettingsWindow] 正在关闭");
    }
}
