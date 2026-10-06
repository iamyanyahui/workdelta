using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WorkDelta.App.ViewModels;
using WorkDelta.Core.Models;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace WorkDelta.App;

public partial class MainWindow : Window
{
    private readonly App _app;
    private readonly ObservableCollection<ProjectRecord> _projects = [];
    private readonly ObservableCollection<TimelineRow> _timeline = [];
    private bool _allowClose;
    private bool _loaded;

    public MainWindow(App app)
    {
        _app = app;
        InitializeComponent();
        ProjectList.ItemsSource = _projects;
        TimelineList.ItemsSource = _timeline;
        IdentityText.Text = $"{app.Identity.DisplayName} · {app.Identity.DeviceName}";
        StartupCheckBox.IsChecked = app.StartupRegistration.IsEnabled;
        ReportDatePicker.SelectedDate = DateTime.Today;
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        _app.Engine.ActivityRecorded += Engine_ActivityRecorded;
    }

    public void AllowClose() => _allowClose = true;

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loaded)
        {
            return;
        }
        _loaded = true;
        await ReloadProjectsAsync();
    }

    private async Task ReloadProjectsAsync(string? selectProjectId = null)
    {
        var selectedId = selectProjectId ?? (ProjectList.SelectedItem as ProjectRecord)?.Id;
        var projects = await _app.Store.GetProjectsAsync();
        _projects.Clear();
        foreach (var project in projects)
        {
            _projects.Add(project);
        }

        if (_projects.Count == 0)
        {
            EmptyPanel.Visibility = Visibility.Visible;
            DashboardPanel.Visibility = Visibility.Collapsed;
            return;
        }

        ProjectList.SelectedItem = _projects.FirstOrDefault(project => project.Id == selectedId) ?? _projects[0];
    }

    private async void AddProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择需要记录的项目文件夹",
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        await AddProjectPathAsync(dialog.FolderName);
    }

    private async Task AddProjectPathAsync(string path)
    {
        await RunBusyAsync("正在建立本地项目基线…", async () =>
        {
            var project = await _app.Engine.AddProjectAsync(path);
            if (_projects.Count == 0)
            {
                _app.StartupRegistration.SetEnabled(true);
                StartupCheckBox.IsChecked = true;
            }
            await ReloadProjectsAsync(project.Id);
        });
    }

    private async void ProjectList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProjectList.SelectedItem is not ProjectRecord project)
        {
            return;
        }

        EmptyPanel.Visibility = Visibility.Collapsed;
        DashboardPanel.Visibility = Visibility.Visible;
        ProjectTitle.Text = project.Name;
        ProjectPathText.Text = project.Path;
        StatusText.Text = project.IsPaused ? "已暂停" : "正在跟踪";
        StatusBadge.Background = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(project.IsPaused ? "#FFF1DE" : "#E8F8F2"));
        StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(project.IsPaused ? "#9A641A" : "#16815D"));
        PauseButton.Content = project.IsPaused ? "继续跟踪" : "暂停跟踪";
        await LoadDashboardAsync(project);
    }

    private async Task LoadDashboardAsync(ProjectRecord project)
    {
        var selectedDate = DateOnly.FromDateTime(ReportDatePicker.SelectedDate ?? DateTime.Today);
        var entries = await _app.Store.GetTimelineAsync(project.Id, selectedDate, TimeZoneInfo.Local);
        var summary = await _app.Store.GetDashboardSummaryAsync(project.Id, selectedDate, TimeZoneInfo.Local);
        _timeline.Clear();
        foreach (var entry in entries)
        {
            _timeline.Add(new TimelineRow(entry));
        }

        DurationText.Text = FormatDuration(summary.ActiveDuration);
        FilesText.Text = summary.FileCount.ToString();
        ChangesText.Text = summary.ChangeCount.ToString();
        ActivityCaption.Text = $"{selectedDate:MM月dd日}项目活动";
        TimelineCaption.Text = $"{selectedDate:yyyy年MM月dd日}工作时间线";
    }

    private async void ReportDatePicker_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loaded && ProjectList.SelectedItem is ProjectRecord project)
        {
            await LoadDashboardAsync(project);
        }
    }

    private async void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (ProjectList.SelectedItem is not ProjectRecord project)
        {
            return;
        }

        await RunBusyAsync(project.IsPaused ? "正在继续跟踪…" : "正在暂停跟踪…", async () =>
        {
            await _app.Engine.SetPausedAsync(project, !project.IsPaused);
            await ReloadProjectsAsync(project.Id);
        });
    }

    private async void SnapshotButton_Click(object sender, RoutedEventArgs e)
    {
        if (ProjectList.SelectedItem is not ProjectRecord project)
        {
            return;
        }

        await RunBusyAsync("正在保存本地检查点…", async () =>
        {
            var result = await _app.Engine.CreateSnapshotNowAsync(project);
            var message = result.HasChanges
                ? $"检查点已保存：新增 {result.Added}、修改 {result.Modified}、删除 {result.Deleted} 个文件。"
                : "当前没有需要保存的新变化。";
            MessageBox.Show(this, message, "工迹 WorkDelta", MessageBoxButton.OK, MessageBoxImage.Information);
            await ReloadProjectsAsync(project.Id);
        });
    }

    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (ProjectList.SelectedItem is not ProjectRecord project)
        {
            return;
        }

        var selectedDate = DateOnly.FromDateTime(ReportDatePicker.SelectedDate ?? DateTime.Today);
        var entries = await _app.Store.GetTimelineAsync(project.Id, selectedDate, TimeZoneInfo.Local);
        var dialog = new SaveFileDialog
        {
            Title = "导出工作记录",
            Filter = "Markdown 文件 (*.md)|*.md",
            FileName = $"{project.Name}-{selectedDate:yyyy-MM-dd}-工作记录.md"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var markdown = new StringBuilder()
            .AppendLine($"# {project.Name} 工作记录")
            .AppendLine()
            .AppendLine($"> {selectedDate:yyyy-MM-dd} · 由工迹 WorkDelta 在本机生成")
            .AppendLine();
        if (entries.Count == 0)
        {
            markdown.AppendLine("所选日期尚未检测到有效的项目文件变化。");
        }
        else
        {
            foreach (var entry in entries.OrderBy(item => item.StartedAt))
            {
                markdown.AppendLine($"## {entry.StartedAt.ToLocalTime():HH:mm}–{entry.LastActivityAt.ToLocalTime():HH:mm}")
                    .AppendLine()
                    .AppendLine($"- 活动时长：{FormatDuration(entry.ActiveDuration)}")
                    .AppendLine($"- 文件数量：{entry.FileCount}")
                    .AppendLine($"- 有效变化：{entry.ChangeCount}")
                    .AppendLine("- 涉及文件：");
                foreach (var file in entry.Files.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
                {
                    markdown.AppendLine($"  - `{file.Replace("`", "\\`")}`");
                }
                markdown.AppendLine();
            }
        }

        await File.WriteAllTextAsync(dialog.FileName, markdown.ToString(), new UTF8Encoding(true));
        MessageBox.Show(this, "工作记录已经导出。", "工迹 WorkDelta", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void HistoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (ProjectList.SelectedItem is ProjectRecord project)
        {
            new HistoryWindow(_app, project) { Owner = this }.ShowDialog();
        }
    }

    private async void ProjectSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (ProjectList.SelectedItem is not ProjectRecord project)
        {
            return;
        }
        var patterns = await _app.Store.GetIgnorePatternsAsync(project.Id);
        var dialog = new ProjectSettingsWindow(project, patterns) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }
        if (dialog.DeleteRequested)
        {
            if (MessageBox.Show(this,
                    "删除这个项目在工迹中的全部时间线和历史检查点？\n源项目文件夹不会被删除。",
                    "确认删除项目记录", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }
            await RunBusyAsync("正在删除项目记录…", async () =>
            {
                await _app.Engine.DeleteProjectAsync(project);
                await ReloadProjectsAsync();
            });
            return;
        }
        await RunBusyAsync("正在更新项目设置…", async () =>
        {
            var updated = await _app.Engine.UpdateProjectAsync(
                project,
                dialog.ProjectName,
                dialog.ProjectPath,
                dialog.IgnorePatterns);
            await ReloadProjectsAsync(updated.Id);
        });
    }

    private void ReportsButton_Click(object sender, RoutedEventArgs e)
    {
        new ReportsWindow(_app, ProjectList.SelectedItem as ProjectRecord) { Owner = this }.ShowDialog();
    }

    private void BackupButton_Click(object sender, RoutedEventArgs e)
    {
        new BackupWindow(_app) { Owner = this }.ShowDialog();
    }

    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] paths)
        {
            return;
        }
        var directory = paths.FirstOrDefault(Directory.Exists);
        if (directory is not null)
        {
            await AddProjectPathAsync(directory);
        }
    }

    private void StartupCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded)
        {
            return;
        }

        try
        {
            _app.StartupRegistration.SetEnabled(StartupCheckBox.IsChecked == true);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "无法修改开机启动", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Engine_ActivityRecorded(object? sender, ActivityRecordedEventArgs e)
    {
        _ = Dispatcher.InvokeAsync(async () =>
        {
            if (ProjectList.SelectedItem is ProjectRecord selected && selected.Id == e.Project.Id)
            {
                var latest = await _app.Store.GetProjectAsync(selected.Id);
                if (latest is not null)
                {
                    await LoadDashboardAsync(latest);
                }
            }
        });
    }

    private async Task RunBusyAsync(string message, Func<Task> action)
    {
        BusyText.Text = message;
        BusyOverlay.Visibility = Visibility.Visible;
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "工迹 WorkDelta", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BusyOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            _app.Engine.ActivityRecorded -= Engine_ActivityRecorded;
            return;
        }

        e.Cancel = true;
        _app.HideToTray();
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}小时{duration.Minutes}分钟";
        }
        return $"{Math.Max(0, duration.Minutes)}分钟";
    }
}
