# Skyxu UI System Installer

这是一个不依赖 UniTask、VContainer 或 UISystem 的轻量安装器。

通过 Unity Package Manager 的 **Add package from git URL** 安装：

```text
https://github.com/sky068/unity-ui-manager.git?path=/Packages/com.skyxu.uisystem.installer#v1.1.0
```

安装后执行：

```text
Tools > UISystem > Installer > Install Core Packages
```

确认后，安装器通过一次 `Client.AddAndRemove` 批量添加固定提交的 UniTask，以及与安装器相同 Git revision 的 UISystem Core。安装器不会覆盖目标项目已经注册的同名包。

需要 VContainer 时，再执行：

```text
Tools > UISystem > Installer > Install VContainer Integration
```

该菜单会补齐 VContainer、UISystem Core、UniTask 和同版本适配包；不使用 VContainer 的项目无需执行。

安装只允许从 Unity Editor 菜单交互确认；批处理模式检测到缺包时会拒绝修改 Package 清单。

完成后按提示导入 TMP Essential Resources、初始化项目资产并运行安装校验。

如果 GitHub、Git 凭据或 Unity Package Manager 不可用，使用 UISystem 包 README 中的 `Packages/manifest.json` 手动安装方式。
