namespace Game.UISystem
{
    /// <summary>
    /// 所有弹窗的唯一 ID 枚举，作为窗口的标识符使用, 枚举名字保持和弹框名字一致。
    /// </summary>
    public enum UIWindowId
    {
        ConfirmWindow = 1,
        SettingWindow,
        TipsWindow,

        FullScreenInfoTest = 10,
        FullScreenListTest = 11,
        DialogCompactTest = 20,
        DialogContentTest = 21,
        NoneToastTest = 30,
        NoneLoadingTest = 31,
        CommonToast = 32
    }
}
