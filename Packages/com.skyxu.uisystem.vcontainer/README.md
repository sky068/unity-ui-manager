# Skyxu UI System VContainer Integration

这是 UISystem 的可选 VContainer 适配包。它提供：

- 使用 VContainer 实例化并注入 Frame 与窗口内容。
- 向容器注册核心 `IUIManager` 与 `UISystemScope` 实例。
- 通过 `UISceneInjectionTarget` 注入显式标记的场景对象。

推荐通过 UISystem Installer 的 `Install VContainer Integration` 菜单安装。手动安装时，目标项目必须同时包含：

```text
https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#a9e27c03d411d2fca01cc7410c24c97cd77cb539
https://github.com/sky068/unity-ui-manager.git?path=/Packages/com.skyxu.uisystem#v1.1.0
https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer#49bdeaa1d9b0558b45ecc6f28f6078223d4ca5a4
https://github.com/sky068/unity-ui-manager.git?path=/Packages/com.skyxu.uisystem.vcontainer#v1.1.0
```

随后在 `UISystemScope` 所在 GameObject 添加 `VContainerUISystemAdapter`。适配器的早期执行顺序会确保核心 Scope 先创建 `IUIManager`，再构建 VContainer 并替换窗口对象工厂。

需要注入场景对象时添加 `UISceneInjectionTarget`。后续加载场景只能保证在 `Start` 前完成注入，不能在 `Awake/OnEnable` 使用注入字段。

普通 UISystem 项目不需要安装本包。
