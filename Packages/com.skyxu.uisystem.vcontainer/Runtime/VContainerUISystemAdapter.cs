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
            ResolveUISystemScope();
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
