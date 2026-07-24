using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Game.UISystem
{
    /// <summary>
    /// UI 系统的全局组合根。默认使用 Unity 实例化，可由可选适配包替换对象工厂。
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public class UISystemScope : MonoBehaviour
    {
        [SerializeField] private UILayerConfig layerConfig;
        [SerializeField] private UIWindowConfig windowConfig;
        [SerializeField] private GameObject uiRoot;

        public static UISystemScope Instance { get; private set; }
        public IUIManager UIManager => _uiManager;
        public IUIObjectFactory ObjectFactory => _objectFactory;

        private UIManager _uiManager;
        private IUIObjectFactory _objectFactory;
        private EventSystem _eventSystem;

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                var duplicateRoot = uiRoot != null ? uiRoot : gameObject;
                duplicateRoot.SetActive(false);
                Destroy(duplicateRoot);
                return;
            }

            if (layerConfig == null || layerConfig.UICanvas == null || windowConfig == null)
                throw new InvalidOperationException(
                    $"[UISystemScope] '{name}' 缺少 UILayerConfig、主 Canvas 或 UIWindowConfig 绑定");

            Instance = this;
            DontDestroyOnLoad(uiRoot != null ? uiRoot : gameObject);

            _objectFactory = new UnityUIObjectFactory();
            _uiManager = new UIManager(_objectFactory, layerConfig, windowConfig);
            Game.UISystem.UIManager.SetInstance(_uiManager);

            _eventSystem = (uiRoot != null ? uiRoot : gameObject)
                .GetComponentInChildren<EventSystem>(true);
            EnsureSingleEventSystem();

            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        }

        protected virtual void OnDestroy()
        {
            if (Instance != this)
                return;

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            _uiManager?.CloseAllImmediately();
            _uiManager = null;
            _objectFactory = null;
            Instance = null;
            Game.UISystem.UIManager.SetInstance(null);
        }

        /// <summary>
        /// 在任何窗口打开前替换实例化策略。容器适配器应在早期 Awake 中调用。
        /// </summary>
        public void SetObjectFactory(IUIObjectFactory objectFactory)
        {
            if (objectFactory == null)
                throw new ArgumentNullException(nameof(objectFactory));
            if (_uiManager == null)
                throw new InvalidOperationException("UISystemScope 尚未初始化");

            _uiManager.SetObjectFactory(objectFactory);
            _objectFactory = objectFactory;
        }

        private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            _uiManager?.CloseAllImmediately();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            EnsureSingleEventSystem();
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
