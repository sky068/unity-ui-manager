using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.UISystem.VContainerIntegration
{
    /// <summary>
    /// 使用 VContainer 创建并在激活前注入窗口实例。
    /// </summary>
    public sealed class VContainerUIObjectFactory : IUIObjectFactory
    {
        private readonly IObjectResolver _resolver;

        public VContainerUIObjectFactory(IObjectResolver resolver)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        public GameObject Instantiate(GameObject prefab, Transform parent)
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            return _resolver.Instantiate(prefab, parent);
        }
    }
}
