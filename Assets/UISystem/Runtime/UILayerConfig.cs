using System.Collections.Generic;
using UnityEngine;

namespace Game.UISystem
{
    public enum UILayer
    {
        Background = 0,
        Normal     = 100,
        Popup      = 200,
        Loading    = 300,
        Toast      = 400,
        Debug      = 999
    }

    [System.Serializable]
    public class UILayerEntry
    {
        public UILayer layer;
        public RectTransform root;
    }

    /// <summary>
    /// 挂在 UISystemRoot 上，保存主 Canvas 和各 UI Layer 的 RectTransform 映射。
    ///
    /// 单 Canvas 下的真实前后关系由各 Layer 根节点的 sibling 顺序决定。
    /// BuildLayerDict 会按 UILayer 枚举值从小到大校正 sibling 顺序，确保
    /// Background → Normal → Popup → Loading → Toast → Debug 的渲染关系稳定。
    /// </summary>
    public class UILayerConfig : MonoBehaviour
    {
        [SerializeField] private Canvas uiCanvas;
        [SerializeField] private List<UILayerEntry> layers = new List<UILayerEntry>();

        private Dictionary<UILayer, RectTransform> _dict;

        public Canvas UICanvas => uiCanvas;

        public Dictionary<UILayer, RectTransform> BuildLayerDict()
        {
            // Layer 数量很少，初始化或 Inspector 修改时整体重建比维护增量缓存更可靠。
            _dict = new Dictionary<UILayer, RectTransform>();
            var validEntries = new List<UILayerEntry>();
            foreach (var e in layers)
            {
                if (e == null || !System.Enum.IsDefined(typeof(UILayer), e.layer))
                {
                    Debug.LogWarning("[UILayerConfig] 存在非法 Layer 条目，已跳过");
                }
                else if (e.root == null)
                    Debug.LogWarning($"[UILayerConfig] Layer {e.layer} 的根节点未配置");
                else
                {
                    if (uiCanvas == null || e.root.parent != uiCanvas.transform)
                    {
                        Debug.LogWarning($"[UILayerConfig] Layer {e.layer} 不属于主 Canvas，已跳过");
                        continue;
                    }

                    if (_dict.ContainsKey(e.layer))
                    {
                        Debug.LogWarning($"[UILayerConfig] Layer {e.layer} 重复配置，将使用后者");
                        validEntries.RemoveAll(item => item.layer == e.layer);
                    }
                    _dict[e.layer] = e.root;
                    validEntries.Add(e);
                }
            }

            // SetAsLastSibling 按顺序执行后，数值更大的 Layer 会位于更上方。
            // 即使 Prefab 中误拖动了层级，运行时也能恢复正确渲染顺序。
            validEntries.Sort((left, right) => left.layer.CompareTo(right.layer));
            foreach (var entry in validEntries)
                entry.root.SetAsLastSibling();

            return _dict;
        }

        public RectTransform GetLayerRoot(UILayer layer)
        {
            // 正常运行时只在第一次查询时构建一次；OnValidate 后由下次查询使用新缓存。
            if (_dict == null) BuildLayerDict();
            if (_dict.TryGetValue(layer, out var root)) return root;
            Debug.LogError($"[UILayerConfig] 找不到 Layer {layer} 对应的根节点");
            return null;
        }
    }
}
