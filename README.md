# 工迹 WorkDelta

> 让每一份工作，都有迹可循。

WorkDelta 是一款本地优先的 Windows 工作记录工具。添加项目文件夹后，它会根据文本文件变化
自动整理工作时间线，让你知道自己在什么时间、为哪个项目、修改了哪些内容。

## 当前能力

- 拖入或选择项目文件夹后自动跟踪
- 合并编辑器产生的重复文件事件
- 15 分钟无活动后自动划分新的工作时段
- SQLite 保存项目活动、会话与设备身份
- 使用隔离的 Git 镜像创建内容检查点，不修改源项目 `.git`
- 默认排除依赖、构建结果、缓存、媒体和临时文件
- 关闭窗口后继续在系统托盘运行
- 可选开机自动启动
- 导出中文 Markdown 日报
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

当前为早期 MVP。工作时间是根据有效文件变化推断的“项目活动时间”，不等同于精确工时。

## License

[MIT](LICENSE)
