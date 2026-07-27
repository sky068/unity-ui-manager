# Changelog

## Unreleased

- `VContainerUISystemAdapter` 在重复 `UISystemRoot` 场景下安全跳过初始化，不再因 `UIManager` 未就绪抛异常。
- 文档：README 新增依赖表，并补充业务依赖注入（parent Scope）接入说明。

## 1.1.0 - 2026-07-24

- 首次拆分为独立可选包。
- 提供 VContainer 窗口工厂、`IUIManager` 注册和场景对象注入。
