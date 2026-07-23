using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UISystem.Example
{
    public class TipsParam
    {
        public string Title   = "提示";
        public string Message = "这是一条提示信息";
        public string OkText  = "知道了";
    }

    public class TipsWindow : UIWindow<TipsParam, R3.Unit>
    {
        [SerializeField] TMP_Text messageText;
        [SerializeField] Button okBtn;

        protected override void OnInit(TipsParam param)
        {
            if (param == null)
            {
                Debug.LogError("[TipsWindow] TipsParam 不能为空");
                Complete(R3.Unit.Default);
                return;
            }

            SetTitle(param.Title);
            messageText.text = param.Message;

            var txt = okBtn.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = param.OkText;

            okBtn.onClick.AddListener(() => Complete(R3.Unit.Default));
        }
    }
}
