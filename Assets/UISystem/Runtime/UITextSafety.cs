using System;

namespace Game.UISystem
{
    /// <summary>公共 UI 文本和 Resources 路径的轻量边界校验。</summary>
    public static class UITextSafety
    {
        public static string NormalizePlainText(string value, int maxLength)
        {
            if (maxLength <= 0 || string.IsNullOrEmpty(value))
                return string.Empty;
            if (value.Length <= maxLength)
                return value;

            int length = maxLength;
            if (length > 0 && char.IsHighSurrogate(value[length - 1]))
                length--;
            return value.Substring(0, length);
        }

        public static string NormalizeToastIconPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string path = value.Trim();
            const string prefix = "UISystem/Icons/";
            if (path.Length > 128 || !path.StartsWith(prefix, StringComparison.Ordinal) ||
                path.Contains(".."))
                return null;

            for (int i = prefix.Length; i < path.Length; i++)
            {
                char c = path[i];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-' && c != '/')
                    return null;
            }
            return path.Length > prefix.Length ? path : null;
        }
    }
}
