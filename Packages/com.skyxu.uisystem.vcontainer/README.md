# Skyxu UI System VContainer Integration

这是 UISystem 的可选 VContainer 适配包。它提供：

- 使用 VContainer 实例化并注入 Frame 与窗口内容。
- 向容器注册核心 `IUIManager` 与 `UISystemScope` 实例。
- 通过 `UISceneInjectionTarget` 注入显式标记的场景对象。

## 依赖

本适配包必须与以下包共存，缺一不可。它们同样通过 Git URL 分发，**无法作为传递依赖自动获取**：

| 依赖 | 是否必需 |
| --- | --- |
| `com.skyxu.uisystem`（UISystem 核心） | 必需 |
| `com.cysharp.unitask` | 必需（核心依赖） |
| `jp.hadashikick.vcontainer` | 必需 |

只安装本包而不安装以上依赖，会导致 `Game.UISystem.Runtime` 或 `VContainer` 程序集缺失、编译失败。请使用安装器的 `Install VContainer Integration`，或按下方列出的**全部** URL 手动安装。

## 安装

推荐通过 UISystem Installer 的 `Install VContainer Integration` 菜单安装。手动安装时，目标项目必须同时包含：

```text
https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#a9e27c03d411d2fca01cc7410c24c97cd77cb539
https://github.com/sky068/unity-ui-manager.git?path=/Packages/com.skyxu.uisystem#v1.1.0
https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer#49bdeaa1d9b0558b45ecc6f28f6078223d4ca5a4
https://github.com/sky068/unity-ui-manager.git?path=/Packages/com.skyxu.uisystem.vcontainer#v1.1.0
```

随后在 `UISystemScope` 所在 GameObject 添加 `VContainerUISystemAdapter`。适配器的早期执行顺序会确保核心 Scope 先创建 `IUIManager`，再构建 VContainer 并替换窗口对象工厂。适配器必须与 `UISystemScope` 位于同一个 `UISystemRoot` 上；场景中误放第二个 `UISystemRoot` 时，核心会保留最先初始化的实例并销毁多余的根，重复根上的适配器会安全跳过初始化。

需要注入场景对象时添加 `UISceneInjectionTarget`。后续加载场景只能保证在 `Start` 前完成注入，不能在 `Awake/OnEnable` 使用注入字段。

## 业务依赖注入（parent Scope）

`VContainerUISystemAdapter` 是一个独立的 `LifetimeScope`，默认只向容器注册 `IUIManager` 与 `UISystemScope`。因此在**默认配置下**，窗口 Prefab 与 `UISceneInjectionTarget` 只能解析这两者。

如果窗口或场景对象需要注入业务服务（例如 `PlayerService`、`IAudio`），必须让本适配器成为业务根 Scope 的**子容器**：在 `VContainerUISystemAdapter` 的 Inspector 中，把 **Parent**（`parentReference`）设为业务根 `LifetimeScope` 的类型。

设为子容器后，VContainer 会先在本容器解析，未命中再向父容器解析，于是窗口既能拿到 `IUIManager`，也能拿到父 Scope 注册的业务服务。

> ⚠️ 未设置父 Scope 时，VContainer 会对未注册的业务依赖抛出 `VContainerException`，并中止对应的窗口创建或场景对象注入。遇到 `No such registration` 时请先检查 Parent 设置和父 Scope 的服务注册。

普通 UISystem 项目不需要安装本包。
