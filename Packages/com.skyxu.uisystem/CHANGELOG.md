# Changelog

## Unreleased

- 修复 `CommonToast` 图标去重上限会在累计出现 32 个不同图标后永久静默丢弃新图标的问题，改为按需加载（Unity 自带资源缓存）。
- `CommonToast` 缺失 `messageText`，或在请求图标时缺失 `iconImage` 引用，会给出可读报错并安全关闭，不再抛 `NullReferenceException`。
- 修复关闭动画交接期间，非栈顶输入屏蔽层可能残留 `raycastTarget` 拦截点击的边界问题。
- 编辑器工具：安装器域重载后主动清理残留进度条；项目初始化的包路径前缀匹配补 `/` 避免误判同前缀包；Sample 同步保留原始异常、禁止 `..` 地址穿越。
- 文档：README 新增依赖表并说明 Git 依赖无法作为 UPM 传递依赖。

## 1.1.0 - 2026-07-24

- 核心包移除对 VContainer 的强制依赖，默认使用 Unity 对象工厂。
- 新增 `IUIObjectFactory` 扩展点，支持可选容器适配器。
- 将 VContainer 实例化与场景注入迁移到独立适配包。

## 1.0.0 - 2026-07-24

- 首次整理为可通过 Git URL 安装的 UPM 包。
- 核心包含 Runtime、三种 Frame、默认 Style、UISystemRoot 模板、Common Toast 和它们必需的中文字体。
- 示例窗口和测试场景放入可选 `Samples~`。
- 新增幂等的项目初始化与安装校验菜单。
- 窗口 ID 改为业务程序集可扩展的强类型值。
- 移除仅用于空返回值的 R3 依赖。
