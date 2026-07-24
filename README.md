# UIManagerDemo

一个面向 Unity 2022.3 LTS 的异步 UI 窗口管理框架与示例。项目使用 uGUI、TextMeshPro 和 UniTask，实现窗口栈、分层渲染、参数与返回值、动画、Toast 及跨场景生命周期管理。核心包不依赖 DI 容器，VContainer 通过独立适配包按需接入。

## 主要特性

- 基于 `UniTask` 的异步窗口打开与关闭 API
- 支持有参窗口和强类型返回值
- `Dialog`、`FullScreen`、`None` 三种窗口框架
- Background、Normal、Popup、Loading、Toast、Debug 分层管理
- 窗口栈、栈顶交互控制及 Esc 返回键关闭
- 可配置的安全遮挡裁剪，减少被完全覆盖窗口的 Draw Call
- 遮罩点击、淡入淡出、缩放和 Toast 滑动动画
- 独立于主窗口栈的自动计时 Toast
- 可替换的 `IUIObjectFactory`，以及可选 VContainer 注入适配包
- 跨场景持久化的 Canvas 和 EventSystem
- TextMeshPro 动态中文字体与多图集支持
- Unity Input System 输入支持

## 环境要求

- Unity `2022.3.62f3c1`
- TextMeshPro `3.0.7`
- Input System `1.19.0`
- UniTask
- VContainer（仅使用可选适配包时需要）

核心包只有 UniTask 是需要目标项目显式安装的外部 Git 依赖；Input System、TextMeshPro 和 uGUI 由包清单声明。本开发工程额外安装 VContainer 与适配包，用于持续编译和测试可选集成。

## 快速开始

1. 使用 Unity Hub 添加并打开本项目。
2. 等待 Unity 完成 Package 导入和脚本编译。
3. 打开 `Assets/UISystem/Scenes/UIWindowTestCases.unity`。
4. 进入 Play Mode，通过测试台验证 Dialog、FullScreen、None 和 Toast 用例。

测试场景已经加入 Build Settings。

## 在其他项目安装

推荐先在 Package Manager 中选择 **Add package from git URL**，只添加轻量安装器：

```text
https://github.com/sky068/unity-ui-manager.git?path=/Packages/com.skyxu.uisystem.installer#v1.1.0
```

安装后执行 `Tools > UISystem > Installer > Install Core Packages`。确认列表后，安装器会一次性添加缺失的 UniTask 和同版本 UISystem Core；已安装的同名包会跳过。需要 VContainer 时再执行 `Install VContainer Integration`，它会补齐 VContainer 与同版本适配包。正式发布前联调可把 `#v1.1.0` 临时换成 `#main`。

随后完成项目初始化：

1. 执行 `Window > TextMeshPro > Import TMP Essential Resources`。
2. 执行 `Tools > UISystem > Initialize Project Assets`。
3. 将生成的 `Assets/UISystem/Prefabs/UISystemRoot.prefab` 放入启动场景一次。
4. 执行 `Tools > UISystem > Validate Installation`。

初始化菜单具有幂等性，只创建缺失的配置、Root Prefab 和业务窗口目录，不覆盖已有项目资产。三种 Frame、默认 Style、Common Toast 及其必需字体是包内核心；三种框体用例、示例窗口与场景通过 Package Manager 的 **UISystem Demo** Sample 按需导入。完整安装器、手动回退和依赖地址见 [包安装文档](Packages/com.skyxu.uisystem/README.md)。

## 修改和发布 UPM 包

本仓库同时是可运行的 Demo 工程和 UPM 包仓库。修改位置取决于内容归属：

- 修改框架 API、窗口管理逻辑、公共 Frame、默认 Style、Common Toast、字体、初始化菜单时，直接修改 `Packages/com.skyxu.uisystem/`。这是发布给其他项目的包源码。
- 修改当前 Demo 的注册表、示例窗口、测试场景或业务 Prefab 时，修改 `Assets/`。这些内容只影响当前 Demo，不会自动进入 UPM 核心。
- 修改准备发布的 Sample 时，以 `Packages/com.skyxu.uisystem/Samples~/UISystemDemo/` 为最终发布内容。当前工程 `Assets` 中的 Demo 是开发工作副本，验证后需要同步到对应 Sample 路径。

Demo 与 Sample 的对应关系：

| Demo 工作副本 | 包内发布位置 |
|---|---|
| `Assets/UISystem/Examples/` | `Samples~/UISystemDemo/UISystem/Examples/` |
| `Assets/UISystem/Config/UIWindowConfig.asset` | `Samples~/UISystemDemo/UISystem/Config/UIWindowConfig.asset` |
| `Assets/UISystem/Prefabs/UISystemRoot.prefab` | `Samples~/UISystemDemo/UISystem/Prefabs/UISystemRoot.prefab` |
| `Assets/UISystem/Scenes/` | `Samples~/UISystemDemo/UISystem/Scenes/` |
| `Assets/Resources/UISystem/Windows/` | `Samples~/UISystemDemo/Resources/UISystem/Windows/` |

验证当前 Demo 后，执行 `Tools > UISystem > Development > Sync Demo To Package Sample` 自动重建 `Samples~/UISystemDemo`。工具会按 `UIWindowConfig` 收集已注册的示例窗口、保留 `.meta`、排除 Runtime 中的 CommonToast，并使用临时目录替换以避免半同步状态。该菜单只在本源码仓库的 Embedded Package 中启用，不会在普通 Git 安装项目中开放。`Samples~` 不会在包开发工程中直接参与编译，因此同步前后仍应使用当前 `Assets` Demo 运行测试。

发布新版本时：

1. 同步修改 UISystem、Installer 和 VContainer Integration 三个 `package.json` 的 `version`。
2. 更新三个包内的 `CHANGELOG.md`。
3. 完成 Unity 编译、`Validate Installation` 和 PlayMode 测试。
4. 提交并推送整个仓库，然后创建与版本一致的 Git 标签。

```bash
git add -A
git commit -m "发布 UISystem v1.1.0"
git push origin main
git tag v1.1.0
git push origin v1.1.0
```

UPM URL 中，`?path=/Packages/com.skyxu.uisystem` 表示只安装整个仓库里的该子目录，`#v1.1.0` 表示固定到对应 Git 标签：

```text
https://github.com/sky068/unity-ui-manager.git?path=/Packages/com.skyxu.uisystem#v1.1.0
```

因此不需要为包单独创建仓库。开发联调可以临时使用 `#main`；正式项目应使用不可变的版本标签，升级时改为新的标签，不要移动旧标签。

UISystem 的 `package.json` 不能可靠地把 Git URL 形式的 UniTask 声明为传递依赖，因此推荐先安装无业务依赖的 Installer。VContainer 已不属于核心依赖，只在安装 `com.skyxu.uisystem.vcontainer` 时需要。无法使用安装器时，按包 README 的手动方式编辑 `Packages/manifest.json`。

## 基本用法

不使用容器时，从全局 Scope 获取 `IUIManager`：

```csharp
using Cysharp.Threading.Tasks;
using Game.UISystem;
using Game.UISystem.Example;

public sealed class ExamplePresenter
{
    private IUIManager ui;

    public void Start() => ui = UIManager.Instance;

    public async UniTask<bool> ShowConfirmAsync()
    {
        return await ui.OpenForResultAsync<ConfirmParam, bool>(
            DemoWindowIds.ConfirmWindow,
            new ConfirmParam
            {
                Title = "确认",
                Message = "是否继续？",
                Confirm = "继续",
                Cancel = "取消"
            });
    }
}
```

使用 VContainer 的项目安装可选适配包，并在 `UISystemScope` 所在 GameObject 添加 `VContainerUISystemAdapter` 后，仍可照常通过 `[Inject]` 获取 `IUIManager`。

打开无参数、无返回值窗口：

```csharp
var handle = ui.Open(DemoWindowIds.SettingWindow);
await handle.Opened; // 仅在确实需要等待开场动画时使用
```

按窗口 ID 关闭最后打开的同 ID 实例：

```csharp
ui.Close(DemoWindowIds.SettingWindow);             // 同步发起关闭
await ui.CloseAsync(DemoWindowIds.SettingWindow);  // 等待退场和清理完成

ui.Close(DemoWindowIds.SettingWindow, true);             // 关闭全部同 ID 实例
await ui.CloseAsync(DemoWindowIds.SettingWindow, true);  // 等待全部同 ID 实例清理完成
```

显示 Toast：

```csharp
ui.ShowToast("保存成功", time: ToastDuration.Normal);
```

Toast 文本按纯文本显示并限制长度；图标路径仅允许 `UISystem/Icons/` 目录，避免不可信文本改变提示语义或任意探测 Resources。

查询当前仍处于开场、显示或退场生命周期中的全屏窗口数量：

```csharp
int fullScreenCount = ui.GetOpenFullScreenCount();
```

获取指定 Layer 根节点并挂载外部 UI：

```csharp
RectTransform debugLayer = ui.GetLayerRoot(UILayer.Debug);
var customView = Object.Instantiate(customViewPrefab, debugLayer, false);
```

通过 Layer 根节点手动添加的对象不归 `UIManager.CloseAll/CloseAllAsync` 管理，外部代码需要自行负责销毁和事件解绑；不要修改 Layer 根节点本身的父节点或 sibling 顺序。

## 添加新窗口

1. 在业务项目中声明唯一的 `static readonly UIWindowId`，不修改包源码。
2. 创建继承 `UIWindow` 或 `UIWindow<TParam, TResult>` 的窗口脚本。
3. 创建内容 Prefab，并放入 `Assets/Resources/UISystem/Windows/`。
4. 按需复用包内默认 `UIWindowStyle`，或在项目中创建自己的 Style。
5. 在 `Assets/UISystem/Config/UIWindowConfig.asset` 中注册窗口 ID、Prefab、样式、实例策略、默认层级和遮挡策略。
6. 通过 `IUIManager.Open` 打开窗口；需要等待返回值时使用 `OpenForResultAsync`。

窗口内容应在 `OnInit` 中初始化；Single 窗口可在 `OnReopen` 中使用新参数刷新内容。`OnReopen` 应先校验参数再一次性更新 UI；抛出异常时管理器会关闭该实例，避免半刷新状态继续存在。在 `OnOpened` 中启动仅应在完整显示后执行的逻辑，在 `OnClosing` 中解绑业务监听。

## 目录结构

```text
Packages/com.skyxu.uisystem/
├── Runtime/                       核心代码、三种 Frame、默认 Style 与 Toast
├── Editor/                        初始化、安装校验和 Inspector 支持
└── Samples~/UISystemDemo/         可选示例窗口与场景

Packages/com.skyxu.uisystem.vcontainer/
└── Runtime/                       可选 VContainer 工厂与场景注入适配器

Assets/
├── Resources/UISystem/Windows/    项目业务窗口 Prefab
└── UISystem/
    ├── Config/UIWindowConfig.asset
    └── Prefabs/UISystemRoot.prefab
```

## 设计说明

`UISystemScope` 是不依赖容器的全局组合根，负责创建 `UIManager`、持久化 UI 根节点并维持唯一 EventSystem。`UIManager` 通过 `IUIObjectFactory` 创建 Frame 和内容；默认实现是 `UnityUIObjectFactory`。

安装可选适配包并添加 `VContainerUISystemAdapter` 后，窗口工厂会切换为 VContainer。需要注入的场景对象再显式添加 `UISceneInjectionTarget`；默认只注入标记对象本身。

`Open()` 和 `CloseAll()` 都同步发起操作。每次 `CloseAll()` 只关闭调用当刻已有的窗口，之后新开的窗口不会被旧批次关闭；`CloseAllAsync()` 语义相同，但会等待该批窗口完成退场和清理。需要严格视觉串行时先 `await CloseAllAsync()`，再调用 `Open()`。

`UIWindowStyle` 只负责 Frame、遮罩颜色和动画等外观；每个 `UIWindowConfig` 条目独立配置 `showMask`、`blockInput`、`closeOnOutsideClick` 和 `closeOnEsc`。因此单个窗口可以在不显示遮罩时阻止点击穿透，不需要与 Style 进行布尔值合并。普通 Dialog 默认使用 `KeepVisible`；确认存在完整不透明区域的大 Dialog 可使用 `HideFullyCovered`；`HideAllBelow` 仅允许用于 FullScreen，并且必须同时开启 `allowFullOcclusion` 明确确认窗口完全不透明。裁剪通过 `CanvasRenderer.cull` 完成，不会触发下层对象的 `OnDisable/OnEnable`。Frame 被裁剪时它自己的 Mask 会同时裁剪；多层窗口采用单一 Mask 所有者，新旧 Mask 交接会继承完整 RGBA 并插值到目标颜色，避免遮罩叠加、跳色和闪烁。系统仅在开场动画结束后裁剪 Frame，并在退场动画开始前恢复；每次栈变化都会从栈顶重新计算，异常关闭或非栈顶关闭也不会遗留错误状态。

`HideFullyCovered` 只依据 Frame 中显式配置的 `OcclusionRect` 判断，透明圆角和阴影不计入不透明区域；未配置该区域时会保守地保持下层可见。测试场景中直接配置了“大窗口覆盖小窗口”“小窗口覆盖大窗口”“全屏覆盖 Dialog”和无 Mask 信息页入口。点击覆盖用例时只打开一级窗口 A；通过 A 内的按钮手动打开二级窗口 B，便于逐帧观察覆盖关系，并可在关闭 B 后重复测试。

主窗口栈只允许在当前栈顶同层或更高的 Layer 打开新窗口；更低 Layer 会在实例化前抛出明确异常，避免视觉顺序、输入焦点、Esc 和 Mask 所有权错位。Toast 不进入主窗口栈，不受该限制。

无容器项目使用 `UIManager.Instance` 或 `UISystemScope.Instance.UIManager`；VContainer 项目优先注入 `IUIManager`。

## 许可证

项目自身代码与资源采用 [MIT License](LICENSE)。第三方包、字体及其附带资源不因本项目的 MIT License 而重新授权，仍遵循各自许可证。
