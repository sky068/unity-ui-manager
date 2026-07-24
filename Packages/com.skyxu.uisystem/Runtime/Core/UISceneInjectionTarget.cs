using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.UISystem
{
    /// <summary>
    /// 显式声明需要由全局 UISystemScope 注入的场景对象。
    /// 默认只注入当前 GameObject 上的组件，避免递归扫描时再次注入由容器创建的子对象。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UISceneInjectionTarget : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("是否同时注入所有子节点。仅当子树完全由场景序列化创建时开启。")]
        private bool includeChildren;

        internal void Inject(IObjectResolver container)
        {
            if (container == null)
                return;

            if (includeChildren)
            {
                container.InjectGameObject(gameObject);
                return;
            }

            var components = GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component != null && component != this)
                    container.Inject(component);
            }
        }
    }
}
