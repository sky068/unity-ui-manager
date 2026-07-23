# UIManagerDemo - UISystem 使用与开发指南

## 1. Demo 目标

这个工程中的 UISystem Demo 使用普通 Unity 资源展示窗口的创建、注册、打开、返回结果和关闭流程，不依赖任何资源生成器。

核心依赖：

- **UniTask**：等待窗口打开、返回结果和关闭动画。
- **VContainer**：实例化窗口内容并支持依赖注入。
- **R3**：用 `Unit` 表示无参数或无返回值。

可以直接打开 `Assets/UISystem/Scenes/UIWindowTestCases.unity` 运行。场景中的按钮分别测试已经注册的 Dialog、FullScreen、None 窗口和 Common Toast。

## 2. 资源结构

```text
Assets/
├── UISystem/
│   ├── Runtime/                  # UIManager、UIWindow 等核心代码
│   ├── Examples/                 # 示例窗口与测试场景 Presenter
│   ├── Config/                   # 注册表和窗口 Style
│   ├── Art/Sprites/              # UI 图片资源
│   ├── Prefabs/
│   │   └── UISystemRoot.prefab   # 持久化主 Canvas、分层节点和 EventSystem
│   └── Scenes/
│       └── UIWindowTestCases.unity
└── Resources/UISystem/
    ├── Frames/                   # 公共窗口底框 Prefab
    └── Windows/                  # 业务窗口内容 Prefab
```

所有内容 Prefab 和注册配置都是可直接编辑、提交版本控制的普通资源。

### 场景接入

在项目的启动场景中放置一次 `Assets/UISystem/Prefabs/UISystemRoot.prefab`。它包含：

- 一个主 Canvas，以及由 `UILayerConfig` 管理的六个 UI Layer 根节点。
- `UISystemScope`，负责注册并提供 `UIManager`。
- `EventSystem`，负责 UI 输入。

`UISystemScope` 会对整个 `UISystemRoot` 调用 `DontDestroyOnLoad`，所以切换业务场景后 UIManager、Canvas 和 EventSystem 都会保留。切换活动场景时，UIManager 会立即清理全部活动 UI（包括普通窗口、Loading 和 Toast），不播放退场动画，避免旧 UI 覆盖新场景。UISystemScope 使用较早的脚本执行顺序注入初始场景，并监听 `sceneLoaded` 注入之后加载的所有场景，包括 Additive 场景；每个场景按 `Scene.handle` 保证只注入一次。后续场景不需要重复添加该 Prefab，也不要再创建单独的 EventSystem；如果场景中存在额外 EventSystem，UISystemScope 会记录警告并只移除重复的 EventSystem/InputModule 组件，不会删除业务 GameObject。

业务组件推荐由 VContainer 注入 `IUIManager`：

```csharp
private IUIManager _uiManager;

[Inject]
private void Construct(IUIManager uiManager)
{
    _uiManager = uiManager;
}
```

`UIManager.Instance` 仅作为无法由容器创建或注入的旧代码兼容入口。

#### 注入时机限制（重要）

> **场景对象只能保证在 `Start` 前完成注入。禁止在场景对象的 `Awake` 或 `OnEnable` 中使用注入字段。**

原因是 Unity 对后续加载场景的生命周期顺序为：场景对象先执行 `Awake/OnEnable`，随后触发 `sceneLoaded`；`UISystemScope` 在 `sceneLoaded` 中完成注入，最后 Unity 才执行 `Start`。因此推荐写法是：

```csharp
[Inject]
private void Construct(IUIManager uiManager)
{
    _uiManager = uiManager;
}

// 错误：后续加载场景中，此时 _uiManager 可能仍为 null。
private void Awake()
{
    // _uiManager.OpenAsync(...);
}

// 正确：场景对象执行 Start 时注入已经完成。
private void Start()
{
    if (_uiManager == null)
    {
        Debug.LogError("IUIManager 注入失败");
        return;
    }

    // 可以安全使用 _uiManager。
}
```

即使初始场景通过较早执行的 `UISystemScope` 通常能在普通组件 `Awake` 前完成注入，业务代码也不应依赖这一差异；所有场景对象统一从 `Start` 或更晚阶段使用注入字段。

运行时动态创建的对象应使用容器实例化。VContainer 会在对象激活和执行 `Awake/OnEnable` 前完成注入：

```csharp
var instance = UISystemScope.Instance.Container.Instantiate(prefab);
```

如果旧代码必须使用 `Object.Instantiate`，创建后需要补做注入。此方式无法追回已经执行的 `Awake/OnEnable`，只能在调用 `InjectGameObject` 之后使用注入字段：

```csharp
var instance = Object.Instantiate(prefab);
UISystemScope.Instance.InjectGameObject(instance);
```

## 3. Demo 中的三种使用方式

### 无参数、无返回值

`SettingsWindow` 继承 `UIWindow`：

```csharp
await _uiManager.OpenAsync<SettingsWindow>(
    UIWindowId.SettingWindow);
```

窗口内部调用 `Close()` 结束。

### 有参数、有返回值

`ConfirmWindow` 继承 `UIWindow<ConfirmParam, bool>`：

```csharp
bool confirmed = await _uiManager
    .OpenAsync<ConfirmWindow, ConfirmParam, bool>(
        UIWindowId.ConfirmWindow,
        new ConfirmParam
        {
            Title = "删除确认",
            Message = "确定要删除这条存档吗？",
            Confirm = "删除",
            Cancel = "取消"
        });
```

窗口内部调用 `Complete(true)` 或 `Complete(false)` 返回结果并关闭。

### 无公共底框窗口

`TipsWindow`、`NoneToastTest` 和 `NoneLoadingTest` 使用 `StyleNone`。它们仍由 UIManager 管理生命周期和窗口栈，但视觉背景、按钮和尺寸全部由内容 Prefab 自己提供。

### Common Toast

Common Toast 不需要等待返回值，会在指定时长后自动关闭：

```csharp
_uiManager.ShowToast("保存成功");
_uiManager.ShowToast(
    "获得奖励",
    "UISystem/Icons/reward",
    ToastDuration.Long);
```

`icon` 是 `Resources` 下的 Sprite 路径，不含扩展名。它可以为 `null`；未传图标或资源不存在时，图标节点会隐藏且不占布局空间，文本自动居中。可选时长为：

- `ToastDuration.Short`：1 秒。
- `ToastDuration.Normal`：2 秒。
- `ToastDuration.Long`：3 秒。

Common Toast 使用独立的 `StyleToast`，不影响其他 None Frame 窗口的动画：

- 打开动画为 0.22 秒，从屏幕中心下方 160 像素处滑入中心并淡入。
- 关闭动画为 0.45 秒，从当前位置向上移动 160 像素并淡出，使用缓入缓出曲线。
- 连续调用 `ShowToast` 不进入队列；每个 Toast 都是独立实例并独立计时。
- 多个 Toast 不做错位排列，统一显示在屏幕中央，后打开的实例位于上层。
- Toast Layer 不参与主窗口栈，不会禁用当前业务窗口，也不会影响 `CloseTopAsync()`。

## 4. 正常新增窗口流程

以下示例新增一个 `RewardWindow`。

### 第一步：声明窗口 ID

在 `UIWindowId.cs` 中添加唯一枚举值：

```csharp
public enum UIWindowId
{
    // 已有窗口……
    RewardWindow = 40
}
```

### 第二步：编写窗口脚本

```csharp
public sealed class RewardParam
{
    public int Gold;
}

public sealed class RewardWindow : UIWindow<RewardParam, bool>
{
    [SerializeField] private Text goldText;
    [SerializeField] private Button receiveButton;
    [SerializeField] private Button cancelButton;

    protected override void OnInit(RewardParam param)
    {
        SetTitle("获得奖励");
        goldText.text = param.Gold.ToString();
        receiveButton.onClick.AddListener(() => Complete(true));
        cancelButton.onClick.AddListener(() => Complete(false));
    }
}
```

窗口初始化使用 `OnInit`，不要依赖 `Start`。可按需覆写：

- `OnOpened()`：开场动画结束后调用。
- `OnClosing()`：退场动画开始前调用。
- `OnUpdate()`：窗口有效期间的每帧逻辑。

### 第三步：制作内容 Prefab

创建：

```text
Assets/Resources/UISystem/Windows/RewardWindow.prefab
```

要求：

- 根节点使用 `RectTransform`。
- 根节点挂载 `RewardWindow`。
- 在 Inspector 中绑定所有序列化字段。
- Dialog 内容可用根节点 `LayoutElement.preferredWidth/Height` 控制窗口尺寸。
- Prefab 文件名建议与窗口类名、枚举名保持一致。

### 第四步：注册窗口

打开 `Assets/UISystem/Config/UIWindowConfig.asset`，在 `Entries` 中增加：

```text
Window Id: RewardWindow
Content Prefab Address: RewardWindow
Style: StyleDialog
Default Layer: Popup
```

Style 决定公共 Frame、遮罩、动画和 ESC 行为；Layer 决定窗口挂到主 Canvas 下的哪个分层节点。
运行时会按 `UILayer` 枚举值自动校正各 Layer 根节点的 sibling 顺序，数值越大的 Layer 显示越靠上。

常用 Layer：

- `Popup`：普通弹窗和全屏业务页。
- `Loading`：加载窗口。
- `Toast`：轻提示。
- `Debug`：调试界面。

### 第五步：打开并等待结果

```csharp
bool received = await _uiManager
    .OpenAsync<RewardWindow, RewardParam, bool>(
        UIWindowId.RewardWindow,
        new RewardParam { Gold = 100 });
```

### 第六步：在 Demo 场景验证

在 `UIWindowTestCases.unity` 中增加测试按钮，绑定到 Presenter，并验证：

- Prefab 能被加载。
- 参数正确显示。
- 确认、取消、遮罩、ESC 和 Frame 关闭按钮行为正确。
- 返回值正确。
- 连续打开多个窗口时只有栈顶窗口可交互。

## 5. 注册与加载规则

`UIWindowConfig.asset` 是唯一窗口注册表。每条记录包含：

- `windowId`：唯一窗口 ID。
- `contentPrefabAddress`：`Resources/UISystem/Windows` 下的 Prefab 名称，不含扩展名。
- `style`：窗口使用的公共 Frame 与交互样式。
- `defaultLayer`：主 Canvas 下的默认分层节点。

UIManager 按以下路径加载资源：

```text
Resources/UISystem/Frames/{style.framePrefabAddress}
Resources/UISystem/Windows/{entry.contentPrefabAddress}
```

加载后会校验内容 Prefab 根节点是否包含调用方指定的窗口脚本类型。

## 6. UIManager 生命周期

1. 根据 `UIWindowId` 查询并校验注册项。
2. 选择主 Canvas 下的目标 Layer 根节点。
3. 加载并实例化 Frame 和内容 Prefab。
4. 调用窗口 `OnInit` 绑定参数和事件。
5. 根据内容首选尺寸布局 Frame。
6. 将窗口压入栈并播放入场动画。
7. 动画结束后调用 `OnOpened`。
8. 等待 `Close()`、`Complete(result)`、ESC、遮罩或 Frame 关闭按钮。
9. 调用 `OnClosing` 并播放退场动画。
10. 销毁窗口并恢复下一栈顶窗口的交互。

任何加载或初始化异常都会清理已经创建的 Frame、Mask 和栈条目。

## 7. Demo 资源说明

| 示例 | 脚本 | Style | 演示内容 |
|---|---|---|---|
| 删除确认 | `ConfirmWindow` | Dialog | 参数输入与 `bool` 返回值 |
| 设置页 | `SettingsWindow` | FullScreen | 无参数、无返回值窗口 |
| 新手提示 | `TipsWindow` | None | 参数输入与 `Unit` 返回值 |
| 紧凑/长内容 | `FrameTestWindow` | Dialog | 内容尺寸驱动 Frame |
| 信息/列表页 | `FrameTestWindow` | FullScreen | 全屏内容布局 |
| Toast/Loading | `FrameTestWindow` | None | 无框轻量窗口 |

这些资源仅作为使用示例，可以直接复制结构创建新的业务窗口，不需要执行任何生成命令。
