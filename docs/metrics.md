# 产品统计

WorkDelta 当前只统计 GitHub Release 软件包的公开下载次数，不在桌面应用中加入遥测。

## 查看方式

- 数据页：`https://work-delta.github.io/workdelta/stats.html`
- GitHub Actions：`deploy-website` 每天北京时间 00:17 自动更新一次
- 原始数据：`site/data/downloads.json`

统计脚本会分别汇总：

- Windows 安装包（`.exe`、`.msi`）
- 便携版（`.zip`）
- 全部软件包
- 最新正式版本安装包
- 最近 730 天的每日累计值

## 指标边界

GitHub 的下载次数是 Release 资源被下载的次数，不是唯一用户数。重复下载会重复计数，转发后的离线安装不会增加计数。因此它适合观察增长趋势，不能代表实际安装量、活跃用户或留存率。

官网访问量和下载入口点击来源需要外部统计接收端。为了避免在没有明确隐私说明和配置的情况下引入第三方追踪，当前版本不采集这两项。

## 本地验证

脚本支持通过 `RELEASES_FILE` 传入 GitHub Releases API 格式的测试数据：

```bash
RELEASES_FILE=/path/to/releases.json \
STATS_OUTPUT=/tmp/downloads.json \
node scripts/update-download-stats.mjs
```
