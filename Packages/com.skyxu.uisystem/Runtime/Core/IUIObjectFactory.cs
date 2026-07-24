using System;
using UnityEngine;

namespace Game.UISystem
{
    /// <summary>
    /// 创建窗口 Frame 和内容实例。业务可通过适配器接入任意依赖注入容器。
    /// </summary>
    public interface IUIObjectFactory
    {
        GameObject Instantiate(GameObject prefab, Transform parent);
    }

    /// <summary>
    /// 不依赖第三方容器的默认实例化实现。
    /// </summary>
    public sealed class UnityUIObjectFactory : IUIObjectFactory
    {
        public GameObject Instantiate(GameObject prefab, Transform parent)
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            return UnityEngine.Object.Instantiate(prefab, parent);
        }
    }
}
