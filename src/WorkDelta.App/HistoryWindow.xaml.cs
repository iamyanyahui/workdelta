using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using WorkDelta.Core.Models;
using MessageBox = System.Windows.MessageBox;

namespace WorkDelta.App;

public partial class HistoryWindow : Window
{
    private readonly App _app;
    private readonly ProjectRecord _project;
    private readonly ObservableCollection<SnapshotRow> _snapshots = [];
    private readonly ObservableCollection<SnapshotFileRow> _files = [];

    public HistoryWindow(App app, ProjectRecord project)
    {
        _app = app;
        _project = project;
        InitializeComponent();
        Title = $"{project.Name} · 历史与恢复";
        SnapshotGrid.ItemsSource = _snapshots;
        FileList.ItemsSource = _files;
        Loaded += async (_, _) => await LoadSnapshotsAsync();
    }

    private async Task LoadSnapshotsAsync()
    {
        var snapshots = await _app.Store.GetSnapshotsAsync(_project.Id);
        _snapshots.Clear();
        foreach (var snapshot in snapshots)
        {
            _snapshots.Add(new SnapshotRow(snapshot));
        }
        if (_snapshots.Count > 0)
        {
            SnapshotGrid.SelectedIndex = 0;
        }
    }

    private async void SnapshotGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SnapshotGrid.SelectedItem is not SnapshotRow row)
        {
            return;
        }
        try
        {
            var changes = await _app.Engine.GetSnapshotChangesAsync(_project, row.Record.CommitId);
            _files.Clear();
            foreach (var change in changes)
            {
                _files.Add(new SnapshotFileRow(change));
            }
            DiffText.Clear();
            DiffTitle.Text = changes.Count == 0 ? "该检查点没有可显示的文本变化" : "选择文件查看具体变化";
            if (_files.Count > 0)
            {
                FileList.SelectedIndex = 0;
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "无法读取历史", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FileList.SelectedItem is not SnapshotFileRow row)
        {
            return;
        }
        DiffTitle.Text = row.Change.Path;
        DiffText.Text = row.Change.Diff;
        DiffText.ScrollToHome();
    }

    private async void RestoreFile_Click(object sender, RoutedEventArgs e)
    {
        if (SnapshotGrid.SelectedItem is not SnapshotRow snapshot || FileList.SelectedItem is not SnapshotFileRow file)
        {
            MessageBox.Show(this, "请先选择检查点和文件。", "工迹 WorkDelta");
            return;
        }
        if (MessageBox.Show(this, $"把“{file.Change.Path}”恢复到该检查点的状态？\n当前文件会被覆盖。",
                "确认恢复", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }
        await _app.Engine.RestoreFilesAsync(_project, snapshot.Record.CommitId, [file.Change.Path]);
        MessageBox.Show(this, "文件已经恢复，并保存了新的恢复检查点。", "工迹 WorkDelta");
        await LoadSnapshotsAsync();
    }

    private async void RestoreSnapshot_Click(object sender, RoutedEventArgs e)
    {
        if (SnapshotGrid.SelectedItem is not SnapshotRow snapshot)
        {
            return;
        }
        if (MessageBox.Show(this,
                "将整个项目中的可跟踪文本文件恢复到这个检查点？\n未被工迹跟踪的媒体、数据库和缓存文件不会受到影响。",
                "确认恢复整个项目", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }
        await _app.Engine.RestoreSnapshotAsync(_project, snapshot.Record.CommitId);
        MessageBox.Show(this, "项目已经恢复，并保存了新的恢复检查点。", "工迹 WorkDelta");
        await LoadSnapshotsAsync();
    }

    private sealed class SnapshotRow
    {
        public SnapshotRow(SnapshotRecord record) => Record = record;
        public SnapshotRecord Record { get; }
        public string LocalTime => Record.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        public string KindText => Record.Kind switch
        {
            "baseline" => "基线",
            "manual" => "手动",
            "automatic" => "自动",
            "reconcile" => "校验",
            "restore" => "恢复",
            "shutdown" => "退出",
            _ => Record.Kind
        };
        public string Summary => $"+{Record.Added}  ~{Record.Modified}  -{Record.Deleted}";
    }

    private sealed class SnapshotFileRow
    {
        public SnapshotFileRow(SnapshotFileChange change) => Change = change;
        public SnapshotFileChange Change { get; }
        public string Path => Change.Path;
        public string Summary => $"{Change.Status} · +{Change.AddedLines}  -{Change.DeletedLines}";
    }
}
