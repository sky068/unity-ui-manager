# Changelog

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
