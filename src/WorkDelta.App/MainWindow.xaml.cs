using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WorkDelta.App.Localization;
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
        LanguageCombo.SelectedValuePath = nameof(LanguageChoice.Code);
        LanguageCombo.ItemsSource = new[]
        {
            new LanguageChoice("system", Localizer.Get("FollowSystem")),
            new LanguageChoice("zh-CN", Localizer.Get("Chinese")),
            new LanguageChoice("en", Localizer.Get("English"))
        };
        LanguageCombo.SelectedValue = Localizer.LanguageSetting;
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
            Title = Localizer.Get("ChooseTrackedFolder"),
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
        await RunBusyAsync(Localizer.Get("CreatingBaseline"), async () =>
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
        StatusText.Text = Localizer.Get(project.IsPaused ? "Paused" : "Tracking");
        StatusBadge.Background = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(project.IsPaused ? "#FFF1DE" : "#E8F8F2"));
        StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(project.IsPaused ? "#9A641A" : "#16815D"));
        PauseButton.Content = Localizer.Get(project.IsPaused ? "ResumeTracking" : "PauseTracking");
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
        var date = selectedDate.ToDateTime(TimeOnly.MinValue).ToString("d", System.Globalization.CultureInfo.CurrentCulture);
        ActivityCaption.Text = Localizer.Format("ActivityCaption", date);
        TimelineCaption.Text = Localizer.Format("TimelineCaption", date);
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

        await RunBusyAsync(Localizer.Get(project.IsPaused ? "ResumingTracking" : "PausingTracking"), async () =>
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

        await RunBusyAsync(Localizer.Get("SavingCheckpoint"), async () =>
        {
            var result = await _app.Engine.CreateSnapshotNowAsync(project);
            var message = result.HasChanges
                ? Localizer.Format("CheckpointSaved", result.Added, result.Modified, result.Deleted)
                : Localizer.Get("NoCheckpointChanges");
            MessageBox.Show(this, message, Localizer.Get("AppName"), MessageBoxButton.OK, MessageBoxImage.Information);
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
            Title = Localizer.Get("ExportWorkLog"),
            Filter = Localizer.Get("MarkdownFilter"),
            FileName = $"{project.Name}-{selectedDate:yyyy-MM-dd}-{Localizer.Get("WorkLogFileSuffix")}.md"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var markdown = new StringBuilder()
            .AppendLine($"# {Localizer.Format("WorkLogTitle", project.Name)}")
            .AppendLine()
            .AppendLine($"> {selectedDate:yyyy-MM-dd} · {Localizer.Get("GeneratedLocally")}")
            .AppendLine();
        if (entries.Count == 0)
        {
            markdown.AppendLine(Localizer.Get("NoActivity"));
        }
        else
        {
            foreach (var entry in entries.OrderBy(item => item.StartedAt))
            {
                markdown.AppendLine($"## {entry.StartedAt.ToLocalTime():HH:mm}–{entry.LastActivityAt.ToLocalTime():HH:mm}")
                    .AppendLine()
                    .AppendLine($"- {Localizer.Get("ActivityDuration")}: {FormatDuration(entry.ActiveDuration)}")
                    .AppendLine($"- {Localizer.Get("FileCount")}: {entry.FileCount}")
                    .AppendLine($"- {Localizer.Get("ChangeCount")}: {entry.ChangeCount}")
                    .AppendLine($"- {Localizer.Get("ChangedFiles")}:");
                foreach (var file in entry.Files.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
                {
                    markdown.AppendLine($"  - `{file.Replace("`", "\\`")}`");
                }
                markdown.AppendLine();
            }
        }

        await File.WriteAllTextAsync(dialog.FileName, markdown.ToString(), new UTF8Encoding(true));
        MessageBox.Show(this, Localizer.Get("WorkLogExported"), Localizer.Get("AppName"), MessageBoxButton.OK, MessageBoxImage.Information);
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
                    Localizer.Get("DeleteProjectQuestion"),
                    Localizer.Get("ConfirmDeleteProject"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }
            await RunBusyAsync(Localizer.Get("DeletingProject"), async () =>
            {
                await _app.Engine.DeleteProjectAsync(project);
                await ReloadProjectsAsync();
            });
            return;
        }
        await RunBusyAsync(Localizer.Get("UpdatingProject"), async () =>
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

    private void SupportButton_Click(object sender, RoutedEventArgs e)
    {
        new SupportWindow { Owner = this }.ShowDialog();
    }

    private async void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded || LanguageCombo.SelectedValue is not string language)
        {
            return;
        }

        await _app.ChangeLanguageAsync(language);
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
            MessageBox.Show(this, exception.Message, Localizer.Get("StartupChangeFailed"), MessageBoxButton.OK, MessageBoxImage.Warning);
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
            MessageBox.Show(this, exception.Message, Localizer.Get("AppName"), MessageBoxButton.OK, MessageBoxImage.Error);
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

    private static string FormatDuration(TimeSpan duration) => Localizer.Duration(duration);

    private sealed record LanguageChoice(string Code, string Name);
}
