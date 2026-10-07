using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using WorkDelta.Core.Models;
using WorkDelta.App.Localization;
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
        Title = $"{project.Name} · {Localizer.Get("HistoryRestore")}";
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
            DiffTitle.Text = Localizer.Get(changes.Count == 0 ? "NoTextChanges" : "SelectFileForDiff");
            if (_files.Count > 0)
            {
                FileList.SelectedIndex = 0;
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, Localizer.Get("ReadHistoryFailed"), MessageBoxButton.OK, MessageBoxImage.Warning);
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
            MessageBox.Show(this, Localizer.Get("SelectCheckpointAndFile"), Localizer.Get("AppName"));
            return;
        }
        if (MessageBox.Show(this, Localizer.Format("RestoreFileQuestion", file.Change.Path),
                Localizer.Get("ConfirmRestore"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }
        await _app.Engine.RestoreFilesAsync(_project, snapshot.Record.CommitId, [file.Change.Path]);
        MessageBox.Show(this, Localizer.Get("FileRestored"), Localizer.Get("AppName"));
        await LoadSnapshotsAsync();
    }

    private async void RestoreSnapshot_Click(object sender, RoutedEventArgs e)
    {
        if (SnapshotGrid.SelectedItem is not SnapshotRow snapshot)
        {
            return;
        }
        if (MessageBox.Show(this,
                Localizer.Get("RestoreProjectQuestion"),
                Localizer.Get("ConfirmRestoreProject"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }
        await _app.Engine.RestoreSnapshotAsync(_project, snapshot.Record.CommitId);
        MessageBox.Show(this, Localizer.Get("ProjectRestored"), Localizer.Get("AppName"));
        await LoadSnapshotsAsync();
    }

    private sealed class SnapshotRow
    {
        public SnapshotRow(SnapshotRecord record) => Record = record;
        public SnapshotRecord Record { get; }
        public string LocalTime => Record.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        public string KindText => Record.Kind switch
        {
            "baseline" => Localizer.Get("KindBaseline"),
            "manual" => Localizer.Get("KindManual"),
            "automatic" => Localizer.Get("KindAutomatic"),
            "reconcile" => Localizer.Get("KindReconcile"),
            "restore" => Localizer.Get("KindRestore"),
            "shutdown" => Localizer.Get("KindShutdown"),
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
