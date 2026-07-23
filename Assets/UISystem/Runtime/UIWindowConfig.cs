using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UISystem
{
    [Serializable]
    public class UIWindowEntry
    {
        [Tooltip("唯一标识，建议使用枚举类型，保持和弹窗名字一致，便于查找和维护")]
        public UIWindowId windowId;

        [Tooltip("内容 Prefab 名称，需放在 Resources/UISystem/Windows/ 目录下")]
        public string contentPrefabAddress;

        [Tooltip("使用哪套底框样式")]
        public UIWindowStyle style;

        [Tooltip("默认显示在哪一层")]
        public UILayer defaultLayer = UILayer.Popup;
    }

    /// <summary>
    /// 全局弹窗注册表
    /// 右键 → Create → UISystem → Window Config
    /// </summary>
    [CreateAssetMenu(fileName = "UIWindowConfig", menuName = "UISystem/Window Config")]
    public class UIWindowConfig : ScriptableObject
    {
        [SerializeField]
        private List<UIWindowEntry> entries = new List<UIWindowEntry>();

        // Inspector 使用 List 保证可编辑性，运行时转换为 Dictionary 获得稳定的 O(1) 查询。
        // 该缓存不序列化，进入 Play Mode 或资源重新启用时会重新构建。
        private Dictionary<UIWindowId, UIWindowEntry> _cache;

        private void OnEnable() => BuildCache();
        private void OnValidate() => BuildCache();

        public void RebuildCache() => BuildCache();

        public UIWindowEntry Get(UIWindowId windowId)
        {
            if (_cache == null) BuildCache();
            if (_cache.TryGetValue(windowId, out var entry)) return entry;
            Debug.LogError($"[UIWindowConfig] 找不到 windowId='{windowId}'，请检查注册表配置");
            return null;
        }

        private void BuildCache()
        {
            if (entries == null)
            {
                _cache = new Dictionary<UIWindowId, UIWindowEntry>();
                Debug.LogError("[UIWindowConfig] entries 不能为空");
                return;
            }

            _cache = new Dictionary<UIWindowId, UIWindowEntry>(entries.Count);
            foreach (var e in entries)
            {
                // 无效条目只跳过并记录日志，不让单个配置错误破坏整个注册表。
                if (e == null)
                {
                    Debug.LogWarning("[UIWindowConfig] 存在空条目，已跳过");
                    continue;
                }
                if (!Enum.IsDefined(typeof(UIWindowId), e.windowId))
                {
                    Debug.LogWarning($"[UIWindowConfig] 非法 windowId='{(int)e.windowId}'，已跳过");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(e.contentPrefabAddress) || e.style == null ||
                    !Enum.IsDefined(typeof(UILayer), e.defaultLayer))
                {
                    Debug.LogWarning($"[UIWindowConfig] windowId='{e.windowId}' 配置不完整，已跳过");
                    continue;
                }
                if (_cache.ContainsKey(e.windowId))
                {
                    // 重复 ID 使用列表中先出现的条目，避免后项静默覆盖导致行为随排序变化。
                    Debug.LogWarning($"[UIWindowConfig] 重复的 windowId='{e.windowId}'，后者已忽略");
                    continue;
                }
                _cache[e.windowId] = e;
            }
        }
    }
}
