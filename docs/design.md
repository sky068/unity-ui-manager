# UIManagerDemo - UISystem 使用与开发指南

## 1. Demo 目标

这个工程中的 UISystem Demo 展示窗口的创建、注册、打开、返回结果和关闭流程。框架核心位于本地 UPM 包 `Packages/com.skyxu.uisystem`；项目配置和 Demo 资源保留在 `Assets`。

核心依赖：

- **UniTask**：等待窗口打开、返回结果和关闭动画。
- **UIUnit**：框架内置的无参数或无返回值类型，不再为此引入 R3。

VContainer 不再是核心依赖。需要依赖注入的项目可安装独立适配包，它通过 `IUIObjectFactory` 接管窗口实例化。

可以直接打开 `Assets/UISystem/Scenes/UIWindowTestCases.unity` 运行。场景中的按钮分别展示已经注册的 Dialog、FullScreen、None 窗口和 Common Toast。

## 2. 资源结构

```text
Packages/com.skyxu.uisystem/
├── Runtime/Core/                 # UIManager、UIWindow 等核心代码
├── Runtime/Resources/UISystem/   # 三种 Frame 与 Common Toast
├── Runtime/Defaults/             # 默认 Style 和初始化配置模板
├── Editor/                       # 初始化与安装校验菜单
└── Samples~/UISystemDemo/        # 可选 Demo Sample

Assets/
├── UISystem/
│   ├── Examples/                 # 当前 Demo 已导入的示例代码
│   ├── Config/                   # 项目自己的注册表
│   ├── Prefabs/UISystemRoot.prefab
│   └── Scenes/UIWindowTestCases.unity
└── Resources/UISystem/Windows/   # 项目业务窗口内容 Prefab
```

三种基础 Frame、默认 Style 和 Common Toast 属于只读包核心。业务窗口 Prefab、注册配置和初始化生成的 Root Prefab 都位于项目 `Assets`，可直接编辑并提交版本控制。

### 其他项目安装

推荐通过 Package Manager 的 Git URL 先安装无业务依赖的 Installer：

```text
https://github.com/sky068/unity-ui-manager.git?path=/Packages/com.skyxu.uisystem.installer#v1.1.0
```

执行 `Tools > UISystem > Installer > Install Core Packages` 并确认后，安装器会批量补齐 UniTask 和同版本 UISystem Core。需要 VContainer 时另行执行 `Install VContainer Integration`。再依次执行 `Window > TextMeshPro > Import TMP Essential Resources`、`Tools > UISystem > Initialize Project Assets` 和 `Tools > UISystem > Validate Installation`。初始化菜单只补齐缺失的配置、Root Prefab 和 `Resources/UISystem/Windows` 目录，不覆盖已有文件。

### 包开发边界

框架代码、公共资源和 Editor 工具直接修改 `Packages/com.skyxu.uisystem`；`Assets` 下的注册表、业务窗口和场景属于当前 Demo。准备发布的示例以 `Packages/com.skyxu.uisystem/Samples~/UISystemDemo` 为准，验证 Demo 后通过 `Tools > UISystem > Development > Sync Demo To Package Sample` 安全同步。完整的目录映射、`?path=` URL 语义、依赖限制和 Git 标签发布流程见根目录 `README.md` 的“修改和发布 UPM 包”。

### 场景接入

在项目的启动场景中放置一次 `Assets/UISystem/Prefabs/UISystemRoot.prefab`。它包含：

- 一个主 Canvas，以及由 `UILayerConfig` 管理的六个 UI Layer 根节点。
- `UISystemScope`，负责注册并提供 `UIManager`。
- `EventSystem`，负责 UI 输入。

`UISystemScope` 会对整个 `UISystemRoot` 调用 `DontDestroyOnLoad`，所以切换业务场景后 UIManager、Canvas 和 EventSystem 都会保留。切换活动场景时，UIManager 会立即清理全部活动 UI（包括普通窗口、Loading 和 Toast），不播放退场动画。核心 Scope 默认使用 `UnityUIObjectFactory`，不扫描或注入场景对象。后续场景不需要重复添加 UISystemRoot，也不要再创建单独的 EventSystem。

无容器项目在 `Start` 或更晚阶段获取服务：

```csharp
private IUIManager _uiManager;

private void Start()
{
    _uiManager = UIManager.Instance;
}
```

### 可选 VContainer 适配

安装 `com.skyxu.uisystem.vcontainer` 后，在 `UISystemScope` 所在 GameObject 添加 `VContainerUISystemAdapter`。适配器会：

- 向容器注册 `IUIManager` 和 `UISystemScope`。
- 使用 VContainer 创建 Frame 与窗口内容。
- 扫描并注入显式添加 `UISceneInjectionTarget` 的场景对象。

此时业务组件可以继续使用 `[Inject] IUIManager`。

#### 注入时机限制（重要）

> **场景对象只能保证在 `Start` 前完成注入。禁止在场景对象的 `Awake` 或 `OnEnable` 中使用注入字段。**

该限制只针对 VContainer 场景注入。Unity 对后续加载场景的生命周期顺序为：场景对象先执行 `Awake/OnEnable`，随后触发 `sceneLoaded`；适配器在 `sceneLoaded` 中完成注入，最后 Unity 才执行 `Start`。

```csharp
[Inject]
private void Construct(IUIManager uiManager)
{
    _uiManager = uiManager;
}

// 错误：后续加载场景中，此时 _uiManager 可能仍为 null。
private void Awake()
{
    // _uiManager.Open(...);
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

即使初始场景通过较早执行的适配器通常能在普通组件 `Awake` 前完成注入，业务代码也不应依赖这一差异；所有场景对象统一从 `Start` 或更晚阶段使用注入字段。

运行时动态创建的对象应使用容器实例化。VContainer 会在对象激活和执行 `Awake/OnEnable` 前完成注入：

```csharp
var instance = adapter.Container.Instantiate(prefab);
```

如果旧代码必须使用 `Object.Instantiate`，创建后需要补做注入。此方式无法追回已经执行的 `Awake/OnEnable`，只能在调用 `InjectGameObject` 之后使用注入字段：

```csharp
var instance = Object.Instantiate(prefab);
adapter.InjectGameObject(instance);
```

## 3. Demo 中的三种使用方式

### 无参数、无返回值

`SettingsWindow` 继承 `UIWindow`：

```csharp
var handle = _uiManager.Open(DemoWindowIds.SettingWindow);
await handle.Closed; // 仅在调用方需要等待完全关闭时使用
```

窗口内部调用 `Close()` 结束。

### 有参数、有返回值

`ConfirmWindow` 继承 `UIWindow<ConfirmParam, bool>`：

```csharp
bool confirmed = await _uiManager
    .OpenForResultAsync<ConfirmParam, bool>(
        DemoWindowIds.ConfirmWindow,
        new ConfirmParam
        {
            Title = "删除确认",
            Message = "确定要删除这条存档吗？",
            Confirm = "删除",
            Cancel = "取消"
        });
```

窗口内部调用 `Complete(true)` 或 `Complete(false)` 返回结果并关闭。

### 按窗口 ID 关闭

`Close(windowId)` 同步发起关闭，`CloseAsync(windowId)` 等待退场动画和清理完成。
同一 ID 同时存在多个实例时，两者都以最后打开且仍处于活动生命周期的实例为目标：

```csharp
_uiManager.Close(DemoWindowIds.ConfirmWindow);
await _uiManager.CloseAsync(DemoWindowIds.ConfirmWindow);
```

传入 `true` 可关闭调用时已存在的全部同 ID 实例：

```csharp
_uiManager.Close(DemoWindowIds.ConfirmWindow, true);
await _uiManager.CloseAsync(DemoWindowIds.ConfirmWindow, true);
```

### 窗口实例策略

`UIWindowEntry.openMode` 默认为 `Multiple`：

- `Multiple`：每次打开都创建独立实例。
- `Single`：复用同 ID 的有效实例，将它移到栈顶并调用 `OnReopen(param)`；当前设置页和自动化测试覆盖该模式。

Single 实例已进入关闭流程时不再复用，新的 `Open` 会创建新实例。

### 无公共底框窗口

`TipsWindow`、`NoneToastTest` 和 `NoneLoadingTest` 使用 `StyleNone`。它们仍由 UIManager 管理生命周期和窗口栈，但视觉背景、按钮和尺寸全部由内容 Prefab 自己提供。这些窗口在 `UIWindowConfig` 中配置为不显示遮罩但屏蔽全屏输入，避免触摸穿透到下层界面；Common Toast 则不显示遮罩也不屏蔽输入。

### Common Toast

Common Toast 不需要等待返回值，会在指定时长后自动关闭：

```csharp
_uiManager.ShowToast("保存成功");
_uiManager.ShowToast(
    "获得奖励",
    "UISystem/Icons/reward",
    ToastDuration.Long);
```

`icon` 是 `Resources/UISystem/Icons/` 下的 Sprite 路径，不含扩展名，例如 `UISystem/Icons/reward`。它可以为 `null`；非法路径、未传图标或资源不存在时，图标节点会隐藏且不占布局空间，文本自动居中。Toast 文本以纯文本显示并限制为 256 个 UTF-16 字符；单次运行最多接受 32 个不同图标路径。可选时长为：

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

在业务程序集声明 ID 常量，不要修改 UPM 包源码：

```csharp
public static class GameWindowIds
{
    public static readonly UIWindowId RewardWindow =
        new UIWindowId("RewardWindow");
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

- `OnReopen(param)`：Single 实例被再次打开时刷新内容。
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
- Prefab 文件名建议与窗口类名、窗口 ID 值保持一致。

### 第四步：注册窗口

打开 `Assets/UISystem/Config/UIWindowConfig.asset`，在 `Entries` 中增加：

```text
Window Id: RewardWindow
Content Prefab Address: RewardWindow
Style: StyleDialog
Default Layer: Popup
```

Style 决定公共 Frame、遮罩颜色和动画；窗口条目决定是否显示遮罩、是否屏蔽输入、外部点击及 ESC 行为；Layer 决定窗口挂到主 Canvas 下的哪个分层节点。
运行时会按 `UILayer` 枚举值自动校正各 Layer 根节点的 sibling 顺序，数值越大的 Layer 显示越靠上。

常用 Layer：

- `Popup`：普通弹窗和全屏业务页。
- `Loading`：加载窗口。
- `Toast`：轻提示。
- `Debug`：调试界面。

### 第五步：打开并等待结果

```csharp
bool received = await _uiManager
    .OpenForResultAsync<RewardParam, bool>(
        GameWindowIds.RewardWindow,
        new RewardParam { Gold = 100 });
```

### 第六步：在 Demo 场景体验

在 `UIWindowTestCases.unity` 中增加示例按钮并绑定到 Presenter，即可直观看到：

- Prefab 的加载与展示。
- 参数传入和返回值接收。
- 确认、取消、遮罩、ESC 和 Frame 关闭按钮的用法。
- 多个窗口的叠加与栈顶交互效果。

## 5. 注册与加载规则

`UIWindowConfig.asset` 是唯一窗口注册表。每条记录包含：

- `windowId`：唯一窗口 ID。
- `contentPrefabAddress`：`Resources/UISystem/Windows` 下的 Prefab 名称，不含扩展名。
- `style`：窗口使用的公共 Frame 与交互样式。
- `defaultLayer`：主 Canvas 下的默认分层节点。
- `openMode`：同 ID 窗口使用 `Multiple` 多实例或 `Single` 复用实例。

UIManager 按以下路径加载资源：

```text
Resources/UISystem/Frames/{style.framePrefabAddress}
Resources/UISystem/Windows/{entry.contentPrefabAddress}
```

加载后会校验内容 Prefab 根节点是否包含调用方指定的窗口脚本类型。

## 6. UIManager 生命周期

1. 根据 `UIWindowId` 查询并校验注册项。
2. 选择主 Canvas 下的目标 Layer 根节点。
3. Single 模式下如果已有有效实例，调用 `OnReopen` 并移到栈顶后直接返回原 Handle。
4. 否则加载并实例化 Frame 和内容 Prefab。
5. 调用窗口 `OnInit` 绑定参数和事件。
6. 根据内容首选尺寸布局 Frame。
7. 将窗口压入栈并播放入场动画。
8. 动画结束后调用 `OnOpened`。
9. 等待 `Close()`、`Complete(result)`、ESC、外部点击或 Frame 关闭按钮。
10. 调用 `OnClosing` 并播放退场动画。
11. 销毁窗口并恢复下一栈顶窗口的交互。

任何加载或初始化异常都会清理已经创建的 Frame、Mask 和栈条目。

## 7. Demo 资源说明

| 示例 | 脚本 | Style | 演示内容 |
|---|---|---|---|
| 删除确认 | `ConfirmWindow` | Dialog | 参数输入与 `bool` 返回值 |
| 设置页 | `SettingsWindow` | FullScreen | 无参数、无返回值窗口 |
| 新手提示 | `TipsWindow` | None | 参数输入与 `UIUnit` 返回值 |
| 紧凑/长内容 | `FrameTestWindow` | Dialog | 内容尺寸驱动 Frame |
| 信息/列表页 | `FrameTestWindow` | FullScreen | 全屏内容布局 |
| Toast/Loading | `FrameTestWindow` | None | 无框轻量窗口 |

这些资源仅作为使用示例，可以直接复制结构创建新的业务窗口，不需要执行任何生成命令。
