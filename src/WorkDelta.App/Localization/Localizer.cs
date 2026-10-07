using System.Globalization;
using System.Windows.Markup;

namespace WorkDelta.App.Localization;

public static class Localizer
{
    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>
    {
        ["AppName"] = "WorkDelta",
        ["Tagline"] = "Keep your work visible",
        ["Reports"] = "Reports",
        ["BackupAndRestore"] = "Backup & restore",
        ["RecordingLocally"] = "Recording locally",
        ["TrackedProjects"] = "Tracked projects",
        ["AddProject"] = "+  Add project folder",
        ["RunAtStartup"] = "Run automatically at startup",
        ["LocalOnly"] = "Data stays on this PC. No internet required.",
        ["Language"] = "Language",
        ["FollowSystem"] = "Follow system",
        ["Chinese"] = "简体中文",
        ["English"] = "English",
        ["AddFirstProject"] = "Add your first project folder",
        ["EmptyDescription"] = "WorkDelta quietly records meaningful text-file changes and organizes them into a work timeline.",
        ["ChooseProject"] = "Choose project folder",
        ["Tracking"] = "Tracking",
        ["Paused"] = "Paused",
        ["PauseTracking"] = "Pause tracking",
        ["ResumeTracking"] = "Resume tracking",
        ["HistoryRestore"] = "History & restore",
        ["ProjectSettings"] = "Project settings",
        ["CreateCheckpoint"] = "Create checkpoint",
        ["ExportLog"] = "Export log",
        ["SelectedDateActivity"] = "Activity on selected date",
        ["FilesTouched"] = "Files touched",
        ["EffectiveChanges"] = "Effective changes",
        ["Timeline"] = "Work timeline",
        ["TimelineHint"] = "New session after 15 minutes of inactivity",
        ["Working"] = "Working…",
        ["ChooseTrackedFolder"] = "Choose a project folder to track",
        ["CreatingBaseline"] = "Creating local project baseline…",
        ["ResumingTracking"] = "Resuming tracking…",
        ["PausingTracking"] = "Pausing tracking…",
        ["SavingCheckpoint"] = "Saving local checkpoint…",
        ["CheckpointSaved"] = "Checkpoint saved: {0} added, {1} modified, {2} deleted.",
        ["NoCheckpointChanges"] = "There are no new changes to save.",
        ["ExportWorkLog"] = "Export work log",
        ["MarkdownFilter"] = "Markdown files (*.md)|*.md",
        ["WorkLogFileSuffix"] = "work-log",
        ["WorkLogTitle"] = "{0} work log",
        ["GeneratedLocally"] = "Generated locally by WorkDelta",
        ["NoActivity"] = "No meaningful project-file changes were detected on the selected date.",
        ["ActivityDuration"] = "Activity duration",
        ["ZeroDuration"] = "0m",
        ["FileCount"] = "Files",
        ["ChangeCount"] = "Effective changes",
        ["ChangedFiles"] = "Changed files",
        ["WorkLogExported"] = "The work log has been exported.",
        ["DeleteProjectQuestion"] = "Delete this project's entire timeline and all checkpoints from WorkDelta?\nThe source project folder will not be deleted.",
        ["ConfirmDeleteProject"] = "Confirm deletion",
        ["DeletingProject"] = "Deleting project records…",
        ["UpdatingProject"] = "Updating project settings…",
        ["StartupChangeFailed"] = "Could not change the startup setting",
        ["ActivityCaption"] = "Activity · {0}",
        ["TimelineCaption"] = "Timeline · {0}",
        ["DurationHoursMinutes"] = "{0}h {1}m",
        ["DurationMinutes"] = "{0}m",
        ["TimelineSummary"] = "{0} files modified · {1} effective changes",
        ["NoFileDetails"] = "No file details",
        ["MoreFiles"] = "   and {0} files total",
        ["HistoryTitle"] = "History & restore — WorkDelta",
        ["HistoryCheckpoints"] = "History checkpoints",
        ["Time"] = "Time",
        ["Type"] = "Type",
        ["Files"] = "Files",
        ["ChangedFilesHeading"] = "Changed files",
        ["RestoreSelectedFile"] = "Restore selected file",
        ["RestoreCheckpoint"] = "Restore entire checkpoint",
        ["SelectFileForDiff"] = "Select a file to view its changes",
        ["NoTextChanges"] = "This checkpoint has no text changes to display",
        ["ReadHistoryFailed"] = "Could not read history",
        ["SelectCheckpointAndFile"] = "Select a checkpoint and a file first.",
        ["RestoreFileQuestion"] = "Restore “{0}” to this checkpoint?\nThe current file will be overwritten.",
        ["ConfirmRestore"] = "Confirm restore",
        ["FileRestored"] = "The file was restored and a new restore checkpoint was saved.",
        ["RestoreProjectQuestion"] = "Restore all tracked text files in the project to this checkpoint?\nUntracked media, database, and cache files will not be affected.",
        ["ConfirmRestoreProject"] = "Confirm project restore",
        ["ProjectRestored"] = "The project was restored and a new restore checkpoint was saved.",
        ["KindBaseline"] = "Baseline", ["KindManual"] = "Manual", ["KindAutomatic"] = "Automatic",
        ["KindReconcile"] = "Reconcile", ["KindRestore"] = "Restore", ["KindShutdown"] = "Shutdown",
        ["BackupTitle"] = "Backup & restore — WorkDelta",
        ["ExportFullBackup"] = "Export full backup",
        ["BackupDescription"] = "Includes projects, work timelines, and all local Git checkpoints.",
        ["ExportBackupButton"] = "Export .workdelta backup",
        ["RestoreFromBackup"] = "Restore from backup",
        ["RestoreDescription"] = "Restoring replaces current local records and restarts the app.",
        ["ChooseBackupButton"] = "Choose backup and restore",
        ["BackupFilter"] = "WorkDelta backups (*.workdelta)|*.workdelta",
        ["BackupExported"] = "The full backup has been exported.",
        ["BackupFailed"] = "Backup failed",
        ["RestoreBackupQuestion"] = "Restoring replaces all current WorkDelta records on this PC. Continue?",
        ["ConfirmBackupRestore"] = "Confirm backup restore",
        ["DisplayName"] = "Display name",
        ["ProjectFolder"] = "Project folder",
        ["Browse"] = "Browse…",
        ["CustomIgnoreRules"] = "Custom ignore rules",
        ["IgnoreRulesHint"] = "One per line. * and ? are supported, for example generated/* or *.secret",
        ["DeleteProjectRecords"] = "Delete project records",
        ["Cancel"] = "Cancel", ["Save"] = "Save",
        ["ChooseNewProjectFolder"] = "Choose a new project folder",
        ["EnterProjectName"] = "Enter a project name.",
        ["ProjectFolderMissing"] = "The project folder does not exist.",
        ["ReportTitle"] = "Work reports — WorkDelta",
        ["WorkReports"] = "Work reports",
        ["Range"] = "Range", ["To"] = "to", ["ThisWeek"] = "This week", ["ThisMonth"] = "This month",
        ["Project"] = "Project", ["Sessions"] = "Sessions", ["Duration"] = "Duration",
        ["ExportReport"] = "Export Markdown report",
        ["AllProjects"] = "All projects",
        ["ReportTotal"] = "Total: {0} · {1} files · {2} changes",
        ["ReportFileSuffix"] = "work-report",
        ["ReportMarkdownTitle"] = "WorkDelta work report",
        ["ReportExported"] = "The report has been exported.",
        ["TrayOpen"] = "Open WorkDelta", ["TrayExit"] = "Exit",
        ["TrayStillRunning"] = "WorkDelta is still running",
        ["TrayNotice"] = "Project changes will continue to be recorded locally. Double-click the tray icon to reopen.",
        ["TrayTooltip"] = "WorkDelta is recording project changes locally",
        ["StartupFailed"] = "WorkDelta failed to start.\n\n{0}",
        ["ExecutableMissing"] = "Could not locate the WorkDelta executable.",
        ["RestartForLanguage"] = "WorkDelta will restart to apply the language change.",
        ["LanguageChange"] = "Change language",
        ["SupportAuthor"] = "Support the author",
        ["SupportDescription"] = "If WorkDelta helps you, you can voluntarily support its continued development.",
        ["SupportOptionalNotice"] = "Supporting is entirely optional and does not unlock products, subscriptions, or additional features.",
        ["Alipay"] = "Alipay", ["WechatPay"] = "WeChat Pay", ["PayPal"] = "PayPal",
        ["ScanWithAlipay"] = "Scan with Alipay", ["ScanWithWechat"] = "Scan with WeChat",
        ["ScanOrPayWithPayPal"] = "Scan the QR code, or pay with PayPal. You choose the amount.",
        ["PayWithPayPal"] = "Pay with PayPal", ["ClickToEnlarge"] = "Click to enlarge the QR code",
        ["QrPreview"] = "Payment QR code", ["Close"] = "Close", ["PaymentOpenFailed"] = "Could not open the payment page."
    };

    private static readonly IReadOnlyDictionary<string, string> Chinese = new Dictionary<string, string>
    {
        ["AppName"] = "工迹 WorkDelta", ["Tagline"] = "工作有迹，变化可见", ["Reports"] = "工作报表",
        ["BackupAndRestore"] = "备份与导入", ["RecordingLocally"] = "本地记录中", ["TrackedProjects"] = "跟踪的项目",
        ["AddProject"] = "＋  添加项目文件夹", ["RunAtStartup"] = "开机后自动运行", ["LocalOnly"] = "数据只保存在本机，无需联网",
        ["Language"] = "语言", ["FollowSystem"] = "跟随系统", ["Chinese"] = "简体中文", ["English"] = "English",
        ["AddFirstProject"] = "添加第一个项目文件夹", ["EmptyDescription"] = "工迹会在后台安静记录文本文件的有效变化，并自动整理成工作时间线。",
        ["ChooseProject"] = "选择项目文件夹", ["Tracking"] = "正在跟踪", ["Paused"] = "已暂停", ["PauseTracking"] = "暂停跟踪",
        ["ResumeTracking"] = "继续跟踪", ["HistoryRestore"] = "历史与恢复", ["ProjectSettings"] = "项目设置",
        ["CreateCheckpoint"] = "立即检查点", ["ExportLog"] = "导出记录", ["SelectedDateActivity"] = "所选日期项目活动",
        ["FilesTouched"] = "涉及文件", ["EffectiveChanges"] = "有效变化", ["Timeline"] = "工作时间线",
        ["TimelineHint"] = "按 15 分钟无活动自动分段", ["Working"] = "正在处理…", ["ChooseTrackedFolder"] = "选择需要记录的项目文件夹",
        ["CreatingBaseline"] = "正在建立本地项目基线…", ["ResumingTracking"] = "正在继续跟踪…", ["PausingTracking"] = "正在暂停跟踪…",
        ["SavingCheckpoint"] = "正在保存本地检查点…", ["CheckpointSaved"] = "检查点已保存：新增 {0}、修改 {1}、删除 {2} 个文件。",
        ["NoCheckpointChanges"] = "当前没有需要保存的新变化。", ["ExportWorkLog"] = "导出工作记录",
        ["MarkdownFilter"] = "Markdown 文件 (*.md)|*.md", ["WorkLogFileSuffix"] = "工作记录", ["WorkLogTitle"] = "{0} 工作记录",
        ["GeneratedLocally"] = "由工迹 WorkDelta 在本机生成", ["NoActivity"] = "所选日期尚未检测到有效的项目文件变化。",
        ["ActivityDuration"] = "活动时长", ["FileCount"] = "文件数量", ["ChangeCount"] = "有效变化", ["ChangedFiles"] = "涉及文件",
        ["ZeroDuration"] = "0分钟",
        ["WorkLogExported"] = "工作记录已经导出。", ["DeleteProjectQuestion"] = "删除这个项目在工迹中的全部时间线和历史检查点？\n源项目文件夹不会被删除。",
        ["ConfirmDeleteProject"] = "确认删除项目记录", ["DeletingProject"] = "正在删除项目记录…", ["UpdatingProject"] = "正在更新项目设置…",
        ["StartupChangeFailed"] = "无法修改开机启动", ["ActivityCaption"] = "{0}项目活动", ["TimelineCaption"] = "{0}工作时间线",
        ["DurationHoursMinutes"] = "{0}小时{1}分钟", ["DurationMinutes"] = "{0}分钟", ["TimelineSummary"] = "修改 {0} 个文件 · {1} 次有效变化",
        ["NoFileDetails"] = "暂无文件明细", ["MoreFiles"] = "   等 {0} 个文件", ["HistoryTitle"] = "历史与恢复 - 工迹 WorkDelta",
        ["HistoryCheckpoints"] = "历史检查点", ["Time"] = "时间", ["Type"] = "类型", ["Files"] = "文件", ["ChangedFilesHeading"] = "发生变化的文件",
        ["RestoreSelectedFile"] = "恢复选中文件", ["RestoreCheckpoint"] = "恢复整个检查点", ["SelectFileForDiff"] = "选择一个文件查看具体变化",
        ["NoTextChanges"] = "该检查点没有可显示的文本变化", ["ReadHistoryFailed"] = "无法读取历史", ["SelectCheckpointAndFile"] = "请先选择检查点和文件。",
        ["RestoreFileQuestion"] = "把“{0}”恢复到该检查点的状态？\n当前文件会被覆盖。", ["ConfirmRestore"] = "确认恢复",
        ["FileRestored"] = "文件已经恢复，并保存了新的恢复检查点。", ["RestoreProjectQuestion"] = "将整个项目中的可跟踪文本文件恢复到这个检查点？\n未被工迹跟踪的媒体、数据库和缓存文件不会受到影响。",
        ["ConfirmRestoreProject"] = "确认恢复整个项目", ["ProjectRestored"] = "项目已经恢复，并保存了新的恢复检查点。",
        ["KindBaseline"] = "基线", ["KindManual"] = "手动", ["KindAutomatic"] = "自动", ["KindReconcile"] = "校验", ["KindRestore"] = "恢复", ["KindShutdown"] = "退出",
        ["BackupTitle"] = "备份与导入 - 工迹 WorkDelta", ["ExportFullBackup"] = "导出完整备份", ["BackupDescription"] = "包含项目列表、工作时间线以及所有本地 Git 检查点。",
        ["ExportBackupButton"] = "导出 .workdelta 备份文件", ["RestoreFromBackup"] = "从备份恢复", ["RestoreDescription"] = "恢复会替换本机现有记录，应用将自动重新启动。",
        ["ChooseBackupButton"] = "选择备份并恢复", ["BackupFilter"] = "工迹备份 (*.workdelta)|*.workdelta", ["BackupExported"] = "完整备份已经导出。",
        ["BackupFailed"] = "备份失败", ["RestoreBackupQuestion"] = "恢复会替换本机现有的全部工迹记录。确定继续吗？", ["ConfirmBackupRestore"] = "确认恢复备份",
        ["DisplayName"] = "显示名称", ["ProjectFolder"] = "项目文件夹", ["Browse"] = "选择…", ["CustomIgnoreRules"] = "自定义排除规则",
        ["IgnoreRulesHint"] = "每行一条，可使用 * 和 ?，例如 generated/* 或 *.secret", ["DeleteProjectRecords"] = "删除项目记录", ["Cancel"] = "取消", ["Save"] = "保存",
        ["ChooseNewProjectFolder"] = "选择新的项目文件夹", ["EnterProjectName"] = "请输入项目名称。", ["ProjectFolderMissing"] = "项目文件夹不存在。",
        ["ReportTitle"] = "工作报表 - 工迹 WorkDelta", ["WorkReports"] = "工作报表", ["Range"] = "范围", ["To"] = "至", ["ThisWeek"] = "本周", ["ThisMonth"] = "本月",
        ["Project"] = "项目", ["Sessions"] = "工作时段", ["Duration"] = "活动时长", ["ExportReport"] = "导出 Markdown 报表", ["AllProjects"] = "全部项目",
        ["ReportTotal"] = "合计：{0} · {1} 个文件 · {2} 次变化", ["ReportFileSuffix"] = "工作报表", ["ReportMarkdownTitle"] = "工迹 WorkDelta 工作报表",
        ["ReportExported"] = "报表已经导出。", ["TrayOpen"] = "打开工迹", ["TrayExit"] = "退出", ["TrayStillRunning"] = "工迹仍在运行",
        ["TrayNotice"] = "项目变化会继续在本机记录。双击托盘图标可重新打开。", ["TrayTooltip"] = "工迹 WorkDelta 正在本地记录项目变化",
        ["StartupFailed"] = "WorkDelta 启动失败。\n\n{0}", ["ExecutableMissing"] = "无法定位工迹程序文件。",
        ["RestartForLanguage"] = "WorkDelta 将重新启动以应用语言设置。", ["LanguageChange"] = "切换语言",
        ["SupportAuthor"] = "支持作者", ["SupportDescription"] = "如果 WorkDelta 对你有帮助，可以自愿支持后续开发。",
        ["SupportOptionalNotice"] = "支持完全自愿，不会解锁商品、订阅或额外功能。",
        ["Alipay"] = "支付宝", ["WechatPay"] = "微信支付", ["PayPal"] = "PayPal",
        ["ScanWithAlipay"] = "使用支付宝扫码", ["ScanWithWechat"] = "使用微信扫码",
        ["ScanOrPayWithPayPal"] = "扫描二维码，或通过 PayPal 付款。金额由你填写。",
        ["PayWithPayPal"] = "通过 PayPal 付款", ["ClickToEnlarge"] = "点击放大二维码",
        ["QrPreview"] = "收款二维码", ["Close"] = "关闭", ["PaymentOpenFailed"] = "无法打开付款页面。"
    };

    public static string LanguageSetting { get; private set; } = "system";
    public static bool IsChinese => CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

    public static void Apply(string? setting)
    {
        LanguageSetting = setting is "zh-CN" or "en" ? setting : "system";
        var name = LanguageSetting == "system"
            ? (CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh-CN" : "en-US")
            : LanguageSetting == "en" ? "en-US" : LanguageSetting;
        var culture = CultureInfo.GetCultureInfo(name);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
    }

    public static string Get(string key) => (IsChinese ? Chinese : English).TryGetValue(key, out var value) ? value : key;
    public static string Format(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, Get(key), args);
    public static string Duration(TimeSpan duration, bool atLeastOneMinute = false) => duration.TotalHours >= 1
        ? Format("DurationHoursMinutes", (int)duration.TotalHours, duration.Minutes)
        : Format("DurationMinutes", Math.Max(atLeastOneMinute ? 1 : 0, duration.Minutes));
}

[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key)
    {
        Key = key;
    }

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) => Localizer.Get(Key);
}
