using UnityEngine;

namespace Game.UISystem
{
    public enum WindowAnimationType
    {
        None = 0,
        Fade = 1,
        Scale = 2,
        FadeAndScale = 3,
        ToastSlide = 4
    }

    public enum UIFrameType
    {
        FullScreen = 0,
        Dialog = 1,
        None = 2
    }

    /// <summary>
    /// 窗口样式配置，一个 Style = 一套底框皮肤
    /// 右键 → Create → UISystem → Window Style
    /// </summary>
    [CreateAssetMenu(fileName = "NewWindowStyle", menuName = "UISystem/Window Style")]
    public class UIWindowStyle : ScriptableObject
    {
        [Header("底框 Prefab")]
        [Tooltip("底框 Prefab 的名称，需放在 Resources/UISystem/Frames/ 目录下")]
        public string framePrefabAddress = "UIFrameDialog";

        [Header("窗口框架")]
        public UIFrameType frameType = UIFrameType.Dialog;

        /// <summary>使用该 Style 的窗口显示遮罩时采用的颜色与透明度。</summary>
        [Header("遮罩外观")]
        [Tooltip("窗口开启 Show Mask 时使用的遮罩颜色与透明度。是否显示遮罩由 UIWindowConfig 的单窗口条目决定。")]
        public Color maskColor = new Color(0f, 0f, 0f, 0.65f);

        [Header("动画")]
        public WindowAnimationType animationType = WindowAnimationType.FadeAndScale;

        [Range(0.05f, 0.8f)]
        public float openDuration = 0.22f;

        [Range(0.05f, 0.8f)]
        public float closeDuration = 0.15f;

        [Range(0.5f, 1f)]
        public float scaleFrom = 0.88f;

        [Range(20f, 300f)]
        [Tooltip("ToastSlide 动画的纵向移动距离")]
        public float slideDistance = 80f;

    }
}
