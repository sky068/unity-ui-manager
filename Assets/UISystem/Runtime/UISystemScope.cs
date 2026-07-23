using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace Game.UISystem
{
    /// <summary>
    /// UI 系统的全局组合根，挂在 UISystemRoot Prefab 内。
    ///
    /// 主要职责：
    /// 1. 创建并持有 VContainer 容器，注册 UIManager 和全局 UI 配置；
    /// 2. 保证 UISystemRoot、主 Canvas 和 EventSystem 跨场景保留；
    /// 3. 为初始场景和后续加载场景中的 MonoBehaviour 补做依赖注入；
    /// 4. 活动场景切换时关闭旧场景遗留的全部 UI；
    /// 5. 维持全局只有一个 UISystemRoot 和一个 EventSystem。
    ///
    /// 当前实现按“一个全局 UISystemScope”设计。如果业务场景以后引入自己的
    /// LifetimeScope，应改用父子 Scope，而不是继续递归注入整个场景。
    /// </summary>
    // 尽量早于普通业务 MonoBehaviour 执行，使初始场景能在大多数 Awake 前完成注入。
    // 后续加载场景的注入由 sceneLoaded 触发，只保证在 Start 前完成，不能保证 Awake/OnEnable。
    [DefaultExecutionOrder(-10000)]
    public class UISystemScope : LifetimeScope
    {
        [SerializeField] private UILayerConfig  layerConfig;
        [SerializeField] private UIWindowConfig windowConfig;
        [SerializeField] private GameObject uiRoot;

        /// <summary>
        /// 兼容普通 Object.Instantiate 和旧代码的访问入口。
        /// 推荐业务代码直接注入 IUIManager，不要把该静态入口当作常规取服务方式。
        /// </summary>
        public static UISystemScope Instance { get; private set; }

        private IUIManager _uiManager;
        private EventSystem _eventSystem;
        private readonly HashSet<int> _injectedSceneHandles = new HashSet<int>();

        protected override void Configure(IContainerBuilder builder)
        {
            // UILayerConfig 是 UISystemRoot 上的场景组件，容器直接注册该现有实例。
            builder.RegisterComponent(layerConfig);

            // UIWindowConfig 是全局注册表 ScriptableObject，由 Prefab 序列化绑定。
            builder.RegisterInstance(windowConfig);

            // UIManager 在本 Scope 内保持单例；Scope 持久化，因此 UIManager 也会跨场景保留。
            builder.Register<UIManager>(Lifetime.Singleton)
                .As<IUIManager>();
        }
        
        protected override void Awake()
        {
            // 新场景如果误放了第二份 UISystemRoot，只保留最先初始化的全局实例。
            // Destroy 在帧末生效，因此重复对象本帧仍可能短暂执行 OnEnable。
            if (Instance != null && Instance != this)
            {
                var duplicateRoot = uiRoot != null ? uiRoot : gameObject;
                // Destroy 会延迟到帧末；先停用可避免重复 Canvas/EventSystem 在本帧继续工作。
                duplicateRoot.SetActive(false);
                Destroy(duplicateRoot);
                return;
            }

            if (layerConfig == null || layerConfig.UICanvas == null || windowConfig == null)
                throw new System.InvalidOperationException(
                    $"[UISystemScope] '{name}' 缺少 UILayerConfig、主 Canvas 或 UIWindowConfig 绑定");
            Instance = this;

            // 必须持久化最外层 uiRoot，而不是仅持久化 UISystemScope 子节点，
            // 否则 Canvas、各 Layer 和 EventSystem 会在切场景时被销毁。
            DontDestroyOnLoad(uiRoot != null ? uiRoot : gameObject);

            // base.Awake() 会构建 VContainer。Container 在此调用完成前不可使用。
            base.Awake();

            _uiManager = Container.Resolve<IUIManager>();
            _eventSystem = (uiRoot != null ? uiRoot : gameObject)
                .GetComponentInChildren<EventSystem>(true);
            EnsureSingleEventSystem();

            // 主动注入当前活动场景，避免初始场景错过 sceneLoaded 订阅。
            // InjectScene 会按 Scene.handle 去重，即使 Unity 随后再次派发 sceneLoaded，
            // 同一场景中的 [Inject] 方法也只会执行一次。
            InjectScene(SceneManager.GetActiveScene());

            // sceneLoaded 覆盖 Single 和 Additive 加载；activeSceneChanged 只表示活动场景发生切换。
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        }

        protected override void OnDestroy()
        {
            if (Instance == this)
            {
                // 场景事件是静态事件，必须成对解绑，否则会持有已销毁 Scope。
                SceneManager.sceneLoaded -= HandleSceneLoaded;
                SceneManager.sceneUnloaded -= HandleSceneUnloaded;
                SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
                _injectedSceneHandles.Clear();
                Instance = null;
                UIManager.ResetInstance(null);
            }
            base.OnDestroy();
        }

        private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            // activeSceneChanged 位于新场景 Start 之前。这里同步移除旧 UI，
            // 不播放退场动画，确保新场景业务开始运行时不会看到上一场景残留窗口。
            _uiManager?.CloseAllImmediately();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            // Unity 的 sceneLoaded 位于新场景对象 Awake/OnEnable 之后、Start 之前。
            // 因此场景对象只能从 Start 起安全使用注入字段；Awake/OnEnable 中不得依赖它们。
            InjectScene(scene);
            EnsureSingleEventSystem();
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            // Unity 的 Scene.handle 在场景生命周期内唯一。卸载后移除记录，
            // 允许未来重新加载同一场景时重新执行注入。
            _injectedSceneHandles.Remove(scene.handle);
        }

        private void InjectScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || Container == null)
                return;

            // Awake 主动注入与 sceneLoaded 可能针对同一启动场景连续触发。
            // HashSet 保证每个场景生命周期只注入一次，避免 Inject 方法重复订阅事件。
            if (!_injectedSceneHandles.Add(scene.handle))
                return;

            // 对场景所有根节点递归注入，覆盖未由 Container.Instantiate 创建的场景对象。
            // 这里只会处理场景加载时已经存在的对象；之后用 Object.Instantiate 创建的对象
            // 不会自动进入本次扫描，必须使用 Container.Instantiate 或手动调用 InjectGameObject。
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                    Container.InjectGameObject(root);
            }
            catch
            {
                // 注入失败时允许修正配置后再次尝试，不能永久把该场景标记为已完成。
                _injectedSceneHandles.Remove(scene.handle);
                throw;
            }
        }

        /// <summary>
        /// 为普通 Object.Instantiate 创建的对象补做依赖注入。
        /// 优先使用 Container.Instantiate，此方法仅作为兼容入口。
        /// 注意：调用本方法时目标对象的 Awake/OnEnable 通常已经执行，补注入无法追回这些回调；
        /// 目标组件应从 Start 或调用完成后的业务方法中使用注入字段。
        /// </summary>
        public void InjectGameObject(GameObject target)
        {
            if (target == null || Container == null)
                return;

            Container.InjectGameObject(target);
        }

        private void EnsureSingleEventSystem()
        {
            if (_eventSystem == null)
            {
                Debug.LogError("[UISystemScope] UISystemRoot 中缺少持久化 EventSystem");
                return;
            }

            foreach (var eventSystem in FindObjectsByType<EventSystem>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (eventSystem == null || eventSystem == _eventSystem)
                    continue;

                // UISystemRoot 内的 EventSystem 是唯一保留对象。只移除重复输入组件，
                // 不销毁其 GameObject，避免误删挂在同一节点上的业务组件。
                Debug.LogWarning(
                    $"[UISystemScope] 场景中存在重复 EventSystem，已移除 '{eventSystem.name}'");
                eventSystem.enabled = false;
                foreach (var inputModule in eventSystem.GetComponents<BaseInputModule>())
                {
                    inputModule.enabled = false;
                    Destroy(inputModule);
                }
                Destroy(eventSystem);
            }
        }
    }
}
