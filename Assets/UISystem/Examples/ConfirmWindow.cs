using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UISystem.Example
{
    public class ConfirmParam
    {
        public string Title   = "提示";
        public string Message = "确定要执行此操作吗？";
        public string Confirm = "确定";
        public string Cancel  = "取消";
    }

    public class ConfirmWindow : UIWindow<ConfirmParam, bool>
    {
        [SerializeField] TMP_Text messageText;
        [SerializeField] Button confirmBtn;
        [SerializeField] Button cancelBtn;

        protected override void OnInit(ConfirmParam param)
        {
            if (param == null)
            {
                Debug.LogError("[ConfirmWindow] ConfirmParam 不能为空");
                Complete(false);
                return;
            }

            SetTitle(param.Title);
            messageText.richText = false;
            messageText.text = UITextSafety.NormalizePlainText(param.Message, 512);
            SetBtnLabel(confirmBtn, param.Confirm);
            SetBtnLabel(cancelBtn,  param.Cancel);

            confirmBtn.onClick.AddListener(() => Complete(true));
            cancelBtn.onClick.AddListener(()  => Complete(false));
        }

        void SetBtnLabel(Button btn, string label)
        {
            var txt = btn.GetComponentInChildren<TMP_Text>();
            if (txt != null)
            {
                txt.richText = false;
                txt.text = UITextSafety.NormalizePlainText(label, 40);
            }
        }
    }
}
