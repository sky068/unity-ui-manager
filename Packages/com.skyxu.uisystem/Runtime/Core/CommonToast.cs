using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UISystem
{
    public sealed class CommonToastParam
    {
        /// <summary>显示文本；null 会按空字符串处理。</summary>
        public string Text;

        /// <summary>Resources 下的 Sprite 路径，不含扩展名；为空时不占用图标位置。</summary>
        public string IconPath;

        /// <summary>Toast 完全打开后的停留时长档位。</summary>
        public ToastDuration Duration = ToastDuration.Normal;
    }

    public sealed class CommonToast : UIWindow<CommonToastParam, UIUnit>
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text messageText;

        private ToastDuration _duration;

        protected override void OnInit(CommonToastParam param)
        {
            // OnInit 位于开场动画之前，先完成内容和布局切换，避免动画过程中跳变。
            param ??= new CommonToastParam();

            if (messageText == null)
            {
                // Prefab 漏挂引用时给出可读报错并立即关闭，而不是在后续访问处抛 NRE。
                Debug.LogError("[CommonToast] Prefab 缺少 messageText 引用，无法显示 Toast");
                Complete(UIUnit.Default);
                return;
            }

            if (!System.Enum.IsDefined(typeof(ToastDuration), param.Duration))
            {
                Debug.LogWarning($"[CommonToast] 非法时长枚举 '{param.Duration}'，已回退为 Normal");
                _duration = ToastDuration.Normal;
            }
            else
            {
                _duration = param.Duration;
            }
            messageText.richText = false;
            messageText.text = UITextSafety.NormalizePlainText(param.Text, 256);

            string iconPath = UITextSafety.NormalizeToastIconPath(param.IconPath);
            if (!string.IsNullOrWhiteSpace(param.IconPath) && iconPath == null)
                Debug.LogWarning("[CommonToast] 图标路径必须位于 UISystem/Icons 且只能包含安全字符");

            // Resources.Load 返回的资源由 Unity 缓存，同一路径重复加载不会重复占用内存；
            // 因此这里按需直接加载，不再维护会静默丢弃新图标的静态去重表。
            Sprite icon = iconPath != null ? Resources.Load<Sprite>(iconPath) : null;
            if (iconPath != null && icon == null)
                Debug.LogWarning($"[CommonToast] 找不到图标资源：{iconPath}");

            if (icon != null && iconImage == null)
            {
                Debug.LogError("[CommonToast] Prefab 缺少 iconImage 引用，无法显示请求的图标");
                Complete(UIUnit.Default);
                return;
            }

            bool hasIcon = icon != null;
            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(hasIcon);
                iconImage.sprite = icon;
            }
            messageText.alignment = hasIcon
                ? TextAlignmentOptions.MidlineLeft
                : TextAlignmentOptions.Center;
        }

        protected override void OnOpened()
        {
            // 展示时长从开场动画完成后开始计算，因此用户能看到完整的 1/2/3 秒内容。
            CloseAfterDelay().Forget();
        }

        private async UniTaskVoid CloseAfterDelay()
        {
            // 使用 Destroy token：切场景或外部清理提前销毁 Toast 时，延迟任务会静默取消，
            // 不会在已销毁组件上再次调用 Complete。
            bool canceled = await UniTask.Delay(
                    (int)(GetDurationSeconds(_duration) * 1000f),
                    cancellationToken: this.GetCancellationTokenOnDestroy())
                .SuppressCancellationThrow();

            if (!canceled)
                Complete(UIUnit.Default);
        }

        private static float GetDurationSeconds(ToastDuration duration)
        {
            return duration switch
            {
                ToastDuration.Short => 1f,
                ToastDuration.Long => 3f,
                _ => 2f
            };
        }
    }
}
