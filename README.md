# 工迹 WorkDelta

> 让每一份工作，都有迹可循。

[官网](https://work-delta.github.io/workdelta/) · [下载最新版](https://github.com/work-delta/workdelta/releases/latest) · [社区讨论](https://github.com/work-delta/workdelta/discussions)

WorkDelta 是一款本地优先的 Windows 工作记录工具。添加项目文件夹后，它会根据文本文件变化
自动整理工作时间线，让你知道自己在什么时间、为哪个项目、修改了哪些内容。

## 当前能力

- 拖入或选择项目文件夹后自动跟踪，可同时管理多个项目
- 合并编辑器产生的重复文件事件
- 使用内容哈希过滤“时间戳变化但内容没变”的无效事件
- 15 分钟无活动后自动划分新的工作时段
- SQLite 保存项目活动、会话与设备身份
- 使用隔离的 Git 镜像创建内容检查点，不修改源项目 `.git`
- 浏览历史检查点，查看逐行文本差异
- 将单个文件或整个项目恢复到任一检查点
- 默认排除依赖、构建结果、缓存、媒体和临时文件
- 支持项目重命名、迁移目录、删除记录和自定义排除规则
- 查看任意日期，生成本周、本月或自定义范围的跨项目报表
- 关闭窗口后继续在系统托盘运行
- 可选开机自动启动
- 导出中文 Markdown 日报与汇总报表
- 导出和恢复包含数据库及全部检查点的 `.workdelta` 完整备份
- 完全本地运行，不需要账号和网络

## 数据位置

```text
%LOCALAPPDATA%\WorkDelta
```

源项目中不会创建 `.git`。WorkDelta 的数据库和历史仓库均保存在上述应用数据目录。

## 隐私边界

WorkDelta 记录的是“项目文件发生变化的时间”，不是键盘监控，也不会读取真实邮箱。Git 检查点
使用自动生成的本地虚拟邮箱。初始版本不提供静态数据加密，因此请保护好自己的 Windows 账户。

## 开发

要求：.NET 10 SDK。Windows UI 使用 WPF；核心项目和测试可在其他平台构建。

```powershell
dotnet restore WorkDelta.slnx
dotnet test WorkDelta.slnx
dotnet run --project src/WorkDelta.App
```

生成 Windows x64 自包含版本：

```powershell
./scripts/publish.ps1
```

## 技术结构

- `WorkDelta.App`：WPF 桌面界面、托盘和开机启动
- `WorkDelta.Core`：文件监控、工作会话、SQLite 和 Git 快照
- `WorkDelta.Core.Tests`：核心行为测试

更多说明见 [架构文档](docs/architecture.md)。

## 状态

当前为 0.2 版本。工作时间是根据有效文件变化推断的“项目活动时间”，不等同于精确工时。
软件不会自动联网检查更新；新版本继续通过 GitHub Releases 手动下载安装。

## License

[MIT](LICENSE)
