# Skyxu UI System

适用于 Unity 2022.3 LTS 的异步 uGUI 窗口管理包，提供三种基础 Frame、窗口栈、分层、Toast 和动画。核心不依赖 DI 容器。

## 依赖

| 依赖 | 版本 | 获取方式 | 是否必需 |
| --- | --- | --- | --- |
| Unity Input System | 1.7.0 | 由 `package.json` 声明，自动获取 | 必需 |
| TextMeshPro | 3.0.7 | 由 `package.json` 声明，自动获取 | 必需 |
| uGUI | 1.0.0 | 由 `package.json` 声明，自动获取 | 必需 |
| UniTask | 见下方 Git URL | **目标项目手动添加** | 必需 |
| VContainer + `com.skyxu.uisystem.vcontainer` | 见下方 Git URL | 手动添加 / 安装器可选安装 | 可选 |

Input System、TextMeshPro、uGUI 属于注册表包，已在本包 `package.json` 中声明，会随本包自动获取。

UniTask 与 VContainer 都通过 **Git URL** 分发。UPM 的 `dependencies` 只支持注册表语义版本，**无法声明 Git 依赖**，因此它们不会作为本包的传递依赖自动安装，必须由目标项目在 `Packages/manifest.json` 中显式添加。推荐用下方的安装器自动完成；手动方式见「手动回退」。只安装本包而不装 UniTask 会导致 `UniTask` 程序集缺失、编译失败。

## 安装

### 推荐：使用轻量安装器

在 Package Manager 中选择 **Add package from git URL**，添加：

```text
https://github.com/sky068/unity-ui-manager.git?path=/Packages/com.skyxu.uisystem.installer#v1.1.0
```

然后执行：

```text
Tools > UISystem > Installer > Install Core Packages
```

确认后，安装器会通过一次 Package Manager 请求添加缺失的 UniTask 和同 Git revision 的 UISystem Core；目标项目已经注册的同名包会跳过。正式发布使用固定标签；仓库开发联调时可临时使用 `#main`。

### 可选：VContainer 集成

需要窗口与场景注入时，执行 `Tools > UISystem > Installer > Install VContainer Integration`。它会安装 VContainer 与同版本 `com.skyxu.uisystem.vcontainer` 适配包。然后在 `UISystemScope` 所在 GameObject 添加 `VContainerUISystemAdapter`。普通项目不需要安装这两项。

### 手动回退

如果安装器无法访问 GitHub、Git 凭据异常或 Package Manager 安装失败，可在目标项目 `Packages/manifest.json` 中安装并固定以下依赖：

```json
{
  "dependencies": {
    "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#a9e27c03d411d2fca01cc7410c24c97cd77cb539",
    "com.skyxu.uisystem": "https://github.com/sky068/unity-ui-manager.git?path=/Packages/com.skyxu.uisystem#v1.1.0"
  }
}
```

Git URL 依赖不能可靠地作为 UPM 包的传递依赖，因此 UniTask 必须由目标项目显式安装。Unity 的 Input System、TextMeshPro 和 uGUI 已由 `package.json` 声明。

安装后执行 `Window > TextMeshPro > Import TMP Essential Resources`，确保包内中文字体使用的 TMP Shader 可用。

上面的 `#v1.1.0` 要在仓库已创建对应标签后使用；正式发布前联调可临时换成 `#main`，避免把不存在的标签写入项目。

## 初始化

安装完成后执行：

```text
Tools > UISystem > Initialize Project Assets
```

菜单只创建缺失内容，不覆盖已有文件：

- `Assets/UISystem/Config/UIWindowConfig.asset`
- `Assets/UISystem/Prefabs/UISystemRoot.prefab`
- `Assets/Resources/UISystem/Windows/`

把生成的 `UISystemRoot.prefab` 放入启动场景一次，然后运行 `Tools > UISystem > Validate Installation`。

三种 Frame、默认 Style、Common Toast 及其必需字体属于包内核心资源；项目注册表和业务窗口保留在 `Assets`，可以正常修改和提交。

## 声明窗口 ID

窗口 ID 可由业务程序集扩展，无需修改只读包源码：

```csharp
public static class GameWindowIds
{
    public static readonly UIWindowId Reward = new UIWindowId("Reward");
}
```

Inspector 中填写相同字符串，并把业务 Prefab 放到 `Assets/Resources/UISystem/Windows/`。

## 示例

在 Package Manager 中选择本包，从 Samples 页导入 **UISystem Demo**。示例包含三种 Frame 的用法、窗口 Prefab、配置和测试场景，不属于运行时核心。

## 开发与发布

在本仓库开发时，`Packages/com.skyxu.uisystem` 是嵌入式包，可以直接编辑：

- `Runtime/`：框架代码和随包发布的公共资源。
- `Editor/`：初始化、校验及 Inspector 工具。
- `Samples~/UISystemDemo/`：Package Manager 中可选导入的示例源文件。
- `package.json`、`CHANGELOG.md`：包版本和变更记录。

仓库根目录 `Assets/` 是 Demo/使用方工作区，不是包核心。修改 `Assets` 不会实时更新 `Samples~`；验证后执行 `Tools > UISystem > Development > Sync Demo To Package Sample`。工具会按 Demo 注册表同步示例代码、配置、Root、场景和窗口 Prefab，保留 `.meta`，并排除已经位于 Runtime 的 Common Toast、三种 Frame 和字体。菜单只在源码仓库的 Embedded Package 中启用。

本包不需要独立 Git 仓库。Git UPM URL 使用 `path` 定位当前仓库中的包目录，并使用片段指定分支、提交或标签：

```text
https://github.com/sky068/unity-ui-manager.git?path=/Packages/com.skyxu.uisystem#v1.1.0
```

- `?path=/Packages/com.skyxu.uisystem`：只读取该子目录作为 UPM 包。
- `#v1.1.0`：固定到版本标签；开发联调可临时改为 `#main` 或提交 SHA。

发布时先更新 `package.json` 的版本和 `CHANGELOG.md`，测试通过后提交整个仓库并创建同名标签。已发布标签不要覆盖或移动，修复应发布新的补丁版本。

UniTask 使用 Git URL，不能作为本 Git UPM 包的可靠传递依赖。推荐由独立 Installer 在 UISystem 导入前安装；如果不使用 Installer，使用方必须先把它显式加入目标项目的 `Packages/manifest.json`。

## 从 1.0 升级

`UISystemScope` 在 1.1 中不再继承 `LifetimeScope`。不使用 VContainer 时无需额外处理，业务可通过 `UIManager.Instance` 获取服务。继续使用 `[Inject]`、容器实例化或 `UISceneInjectionTarget` 的项目，需要安装 VContainer 适配包并在 `UISystemRoot` 上添加 `VContainerUISystemAdapter`；旧 `UISceneInjectionTarget` 的 Unity GUID 保持不变。
