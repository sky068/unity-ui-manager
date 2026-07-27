using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace Game.UISystem.VContainerIntegration
{
    /// <summary>
    /// 将核心 UISystem 接入 VContainer，并为显式标记的场景对象补做注入。
    /// 必须与 UISystemScope 同时存在，且会在普通业务脚本之前完成初始化。
    ///
    /// 业务依赖注入：本适配器是一个独立的 <see cref="LifetimeScope"/>，默认只注册
    /// <see cref="UISystemScope"/> 与 <see cref="IUIManager"/>。若希望窗口 Prefab 或
    /// <see cref="UISceneInjectionTarget"/> 能注入业务服务（如 PlayerService），必须让
    /// 本容器成为业务根 Scope 的子容器：在 Inspector 的 Parent 中设置业务根 Scope 类型
    /// （继承自 LifetimeScope 的 parentReference）。未设置父 Scope 时，解析未注册的
    /// 业务依赖会抛出 VContainerException，并中止对应的窗口创建或场景对象注入。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9999)]
    public sealed class VContainerUISystemAdapter : LifetimeScope
    {
        [SerializeField] private UISystemScope uiSystemScope;

        private readonly HashSet<int> _injectedSceneHandles = new HashSet<int>();

        protected override void Configure(IContainerBuilder builder)
        {
            ResolveUISystemScope();
            if (uiSystemScope.UIManager == null)
                throw new InvalidOperationException(
                    "UISystemScope 必须先于 VContainerUISystemAdapter 初始化");

            builder.RegisterInstance(uiSystemScope);
            builder.RegisterInstance(uiSystemScope.UIManager);
        }

        protected override void Awake()
        {
            // UISystemScope 的执行顺序（-10000）早于本适配器（-9999），因此正常情况下
            // 此刻 Scope 已初始化完成。找不到绑定的 Scope 属于配置错误，ResolveUISystemScope 抛出。
            ResolveUISystemScope();

            // 重复 UISystemRoot：UISystemScope 只保留最先初始化的实例，并停用/销毁多余的根。
            // 此时本适配器所在的是被销毁的重复根，其 Scope 尚未创建 UIManager；若继续
            // base.Awake() 会在 Configure 中因 UIManager 为空而抛异常。安静退出，随根一起销毁即可。
            if (uiSystemScope.UIManager == null || UISystemScope.Instance != uiSystemScope)
                return;

            base.Awake();

            uiSystemScope.SetObjectFactory(new VContainerUIObjectFactory(Container));
            InjectScene(SceneManager.GetActiveScene());
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        protected override void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            _injectedSceneHandles.Clear();
            base.OnDestroy();
        }

        /// <summary>
        /// 为 Object.Instantiate 创建的对象补做注入。其 Awake/OnEnable 已经执行，
        /// 因此注入字段只能从调用完成后的业务逻辑使用。
        /// </summary>
        public void InjectGameObject(GameObject target)
        {
            if (target != null && Container != null)
                Container.InjectGameObject(target);
        }

        private void ResolveUISystemScope()
        {
            if (uiSystemScope == null)
                uiSystemScope = GetComponent<UISystemScope>() ??
                                GetComponentInParent<UISystemScope>();
            if (uiSystemScope == null)
                throw new InvalidOperationException(
                    "VContainerUISystemAdapter 未绑定 UISystemScope");
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            InjectScene(scene);
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            _injectedSceneHandles.Remove(scene.handle);
        }

        private void InjectScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || Container == null)
                return;
            if (!_injectedSceneHandles.Add(scene.handle))
                return;

            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    var targets = root.GetComponentsInChildren<UISceneInjectionTarget>(true);
                    for (int i = 0; i < targets.Length; i++)
                        targets[i].Inject(Container);
                }
            }
            catch
            {
                _injectedSceneHandles.Remove(scene.handle);
                throw;
            }
        }
    }
}
