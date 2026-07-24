namespace Game.UISystem
{
    /// <summary>用于表示无参数或无返回值的轻量值类型，避免为此引入额外响应式库。</summary>
    public readonly struct UIUnit
    {
        public static readonly UIUnit Default = default;
    }
}
