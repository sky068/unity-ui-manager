using Game.UISystem;

namespace Game.UISystem.Example
{
    /// <summary>Demo 自己声明窗口 ID；业务项目也应采用同样方式，不修改框架包源码。</summary>
    public static class DemoWindowIds
    {
        public static readonly UIWindowId ConfirmWindow = new UIWindowId("ConfirmWindow");
        public static readonly UIWindowId SettingWindow = new UIWindowId("SettingWindow");
        public static readonly UIWindowId TipsWindow = new UIWindowId("TipsWindow");

        public static readonly UIWindowId FullScreenInfoTest = new UIWindowId("FullScreenInfoTest");
        public static readonly UIWindowId FullScreenListTest = new UIWindowId("FullScreenListTest");
        public static readonly UIWindowId DialogCompactTest = new UIWindowId("DialogCompactTest");
        public static readonly UIWindowId DialogContentTest = new UIWindowId("DialogContentTest");
        public static readonly UIWindowId NoneToastTest = new UIWindowId("NoneToastTest");
        public static readonly UIWindowId NoneLoadingTest = new UIWindowId("NoneLoadingTest");
    }
}
