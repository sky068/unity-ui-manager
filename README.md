# UIManagerDemo

一个面向 Unity 2022.3 LTS 的异步 UI 窗口管理示例。项目使用 uGUI、TextMeshPro、UniTask、R3 和 VContainer，实现窗口栈、分层渲染、参数与返回值、动画、Toast 及跨场景生命周期管理。

## 主要特性

- 基于 `UniTask` 的异步窗口打开与关闭 API
- 支持有参窗口和强类型返回值
- `Dialog`、`FullScreen`、`None` 三种窗口框架
- Background、Normal、Popup、Loading、Toast、Debug 分层管理
- 窗口栈、栈顶交互控制及 Esc 返回键关闭
- 可配置的安全遮挡裁剪，减少被完全覆盖窗口的 Draw Call
- 遮罩点击、淡入淡出、缩放和 Toast 滑动动画
- 独立于主窗口栈的自动计时 Toast
- VContainer 构造注入及场景对象注入
- 跨场景持久化的 Canvas 和 EventSystem
- TextMeshPro 动态中文字体与多图集支持
- Unity Input System 输入支持

## 环境要求

- Unity `2022.3.62f3c1`
- TextMeshPro `3.0.7`
- Input System `1.19.0`
- UniTask
- R3
- VContainer

依赖已写入 `Packages/manifest.json`，首次打开项目时由 Unity Package Manager 自动恢复。Git 依赖恢复需要能够访问 GitHub。

## 快速开始

1. 使用 Unity Hub 添加并打开本项目。
2. 等待 Unity 完成 Package 导入和脚本编译。
3. 打开 `Assets/UISystem/Scenes/UIWindowTestCases.unity`。
4. 进入 Play Mode，通过测试台验证 Dialog、FullScreen、None 和 Toast 用例。

测试场景已经加入 Build Settings。

## 基本用法

推荐通过 VContainer 注入 `IUIManager`：

```csharp
using Cysharp.Threading.Tasks;
using Game.UISystem;
using Game.UISystem.Example;
using VContainer;

public sealed class ExamplePresenter
{
    private readonly IUIManager ui;

    [Inject]
    public ExamplePresenter(IUIManager ui)
    {
        this.ui = ui;
    }

    public async UniTask<bool> ShowConfirmAsync()
    {
        return await ui.OpenAsync<ConfirmWindow, ConfirmParam, bool>(
            UIWindowId.ConfirmWindow,
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

打开无参数、无返回值窗口：

```csharp
await ui.OpenAsync<SettingsWindow>(UIWindowId.SettingWindow);
```

显示 Toast：

```csharp
ui.ShowToast("保存成功", time: ToastDuration.Normal);
```

查询当前仍处于开场、显示或退场生命周期中的全屏窗口数量：

```csharp
int fullScreenCount = ui.GetOpenFullScreenCount();
```

获取指定 Layer 根节点并挂载外部 UI：

```csharp
RectTransform debugLayer = ui.GetLayerRoot(UILayer.Debug);
var customView = Object.Instantiate(customViewPrefab, debugLayer, false);
```

通过 Layer 根节点手动添加的对象不归 `UIManager.CloseAllAsync` 管理，外部代码需要自行负责销毁和事件解绑；不要修改 Layer 根节点本身的父节点或 sibling 顺序。

## 添加新窗口

1. 在 `UIWindowId` 中增加唯一 ID。
2. 创建继承 `UIWindow` 或 `UIWindow<TParam, TResult>` 的窗口脚本。
3. 创建内容 Prefab，并放入 `Assets/Resources/UISystem/Windows/`。
4. 按需创建或复用 `UIWindowStyle`。
5. 在 `Assets/UISystem/Config/UIWindowConfig.asset` 中注册窗口 ID、Prefab、样式、默认层级和遮挡策略。
6. 通过 `IUIManager.OpenAsync` 打开窗口。

窗口内容应在 `OnInit` 中初始化，在 `OnOpened` 中启动仅应在完整显示后执行的逻辑，在 `OnClosing` 中解绑业务监听。

## 目录结构

```text
Assets/
├── Fonts/                         TMP 中文字体资源
├── Resources/UISystem/
│   ├── Frames/                    公共窗口框架 Prefab
│   └── Windows/                   窗口内容 Prefab
└── UISystem/
    ├── Config/                    窗口注册表与样式配置
    ├── Examples/                  示例窗口和测试入口
    ├── Prefabs/UISystemRoot.prefab
    ├── Runtime/                   UI 系统核心代码
    └── Scenes/UIWindowTestCases.unity
```

## 设计说明

`UISystemScope` 是全局组合根，负责构建 VContainer、持久化 UI 根节点、注入场景对象并维持唯一 EventSystem。`UIManager` 根据 `UIWindowConfig` 加载 Frame 和内容 Prefab，并负责窗口栈、动画、交互状态与资源清理。

`UIWindowStyle` 只负责 Frame、遮罩颜色和动画等外观；每个 `UIWindowConfig` 条目独立配置 `showMask`、`blockInput`、`closeOnOutsideClick` 和 `closeOnEsc`。因此单个窗口可以在不显示遮罩时阻止点击穿透，不需要与 Style 进行布尔值合并。普通 Dialog 默认使用 `KeepVisible`；确认存在完整不透明区域的大 Dialog 可使用 `HideFullyCovered`；`HideAllBelow` 仅允许用于 FullScreen，并且必须同时开启 `allowFullOcclusion` 明确确认窗口完全不透明。裁剪通过 `CanvasRenderer.cull` 完成，不会触发下层对象的 `OnDisable/OnEnable`。Frame 被裁剪时它自己的 Mask 会同时裁剪；多层窗口采用单一 Mask 所有者，新旧 Mask 交接会继承完整 RGBA 并插值到目标颜色，避免遮罩叠加、跳色和闪烁。系统仅在开场动画结束后裁剪 Frame，并在退场动画开始前恢复；每次栈变化都会从栈顶重新计算，异常关闭或非栈顶关闭也不会遗留错误状态。

`HideFullyCovered` 只依据 Frame 中显式配置的 `OcclusionRect` 判断，透明圆角和阴影不计入不透明区域；未配置该区域时会保守地保持下层可见。测试场景中直接配置了“大窗口覆盖小窗口”“小窗口覆盖大窗口”“全屏覆盖 Dialog”和无 Mask 信息页入口。点击覆盖用例时只打开一级窗口 A；通过 A 内的按钮手动打开二级窗口 B，便于逐帧观察覆盖关系，并可在关闭 B 后重复测试。

主窗口栈只允许在当前栈顶同层或更高的 Layer 打开新窗口；更低 Layer 会在实例化前抛出明确异常，避免视觉顺序、输入焦点、Esc 和 Mask 所有权错位。Toast 不进入主窗口栈，不受该限制。

业务代码应优先注入 `IUIManager`。`UIManager.Instance` 仅作为非容器代码和旧代码的兼容入口。

## 许可证

项目自身代码与资源采用 [MIT License](LICENSE)。第三方包、字体及其附带资源不因本项目的 MIT License 而重新授权，仍遵循各自许可证。
