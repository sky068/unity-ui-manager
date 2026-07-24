using System;
using UnityEngine;

namespace Game.UISystem
{
    /// <summary>
    /// 可跨程序集扩展的窗口强类型 ID。
    /// 业务项目通过 static readonly 字段声明自己的 ID，不需要修改 UPM 包源码。
    /// </summary>
    [Serializable]
    public struct UIWindowId : IEquatable<UIWindowId>
    {
        public const int MaxLength = 64;

        [SerializeField]
        private string value;

        public string Value => value ?? string.Empty;
        public bool IsValid => IsValidValue(value);

        public UIWindowId(string value)
        {
            if (!IsValidValue(value))
                throw new ArgumentException(
                    $"窗口 ID 必须为 1-{MaxLength} 个字符，且只能包含字母、数字、'.'、'_' 或 '-'",
                    nameof(value));

            this.value = value;
        }

        /// <summary>框架内置 Toast。业务窗口 ID 应声明在项目自己的常量类中。</summary>
        public static readonly UIWindowId CommonToast = new UIWindowId("CommonToast");

        public bool Equals(UIWindowId other) =>
            string.Equals(value, other.value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is UIWindowId other && Equals(other);

        public override int GetHashCode() =>
            value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);

        public override string ToString() => Value;

        public static bool operator ==(UIWindowId left, UIWindowId right) => left.Equals(right);
        public static bool operator !=(UIWindowId left, UIWindowId right) => !left.Equals(right);

        internal static bool IsValidValue(string candidate)
        {
            if (string.IsNullOrEmpty(candidate) || candidate.Length > MaxLength)
                return false;

            for (int i = 0; i < candidate.Length; i++)
            {
                char c = candidate[i];
                if (!char.IsLetterOrDigit(c) && c != '.' && c != '_' && c != '-')
                    return false;
            }

            return true;
        }
    }
}
