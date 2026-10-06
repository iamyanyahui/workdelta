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

        await RunBusyAsync("正在建立本地项目基线…", async () =>
        {
            var project = await _app.Engine.AddProjectAsync(dialog.FolderName);
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
        var today = DateOnly.FromDateTime(DateTime.Now);
        var entries = await _app.Store.GetTimelineAsync(project.Id, today, TimeZoneInfo.Local);
        var summary = await _app.Store.GetDashboardSummaryAsync(project.Id, today, TimeZoneInfo.Local);
        _timeline.Clear();
        foreach (var entry in entries)
        {
            _timeline.Add(new TimelineRow(entry));
        }

        DurationText.Text = FormatDuration(summary.ActiveDuration);
        FilesText.Text = summary.FileCount.ToString();
        ChangesText.Text = summary.ChangeCount.ToString();
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

        var today = DateOnly.FromDateTime(DateTime.Now);
        var entries = await _app.Store.GetTimelineAsync(project.Id, today, TimeZoneInfo.Local);
        var dialog = new SaveFileDialog
        {
            Title = "导出今日工作记录",
            Filter = "Markdown 文件 (*.md)|*.md",
            FileName = $"{project.Name}-{today:yyyy-MM-dd}-工作记录.md"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var markdown = new StringBuilder()
            .AppendLine($"# {project.Name} 工作记录")
            .AppendLine()
            .AppendLine($"> {today:yyyy-MM-dd} · 由工迹 WorkDelta 在本机生成")
            .AppendLine();
        if (entries.Count == 0)
        {
            markdown.AppendLine("今日尚未检测到有效的项目文件变化。");
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
        MessageBox.Show(this, "今日工作记录已经导出。", "工迹 WorkDelta", MessageBoxButton.OK, MessageBoxImage.Information);
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
