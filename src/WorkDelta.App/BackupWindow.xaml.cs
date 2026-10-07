using System.Windows;
using Microsoft.Win32;
using WorkDelta.App.Localization;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace WorkDelta.App;

public partial class BackupWindow : Window
{
    private readonly App _app;

    public BackupWindow(App app)
    {
        _app = app;
        InitializeComponent();
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = Localizer.Get("BackupFilter"),
            FileName = $"WorkDelta-{DateTime.Now:yyyyMMdd-HHmm}.workdelta"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            IsEnabled = false;
            await _app.Backup.ExportAsync(dialog.FileName);
            MessageBox.Show(this, Localizer.Get("BackupExported"), Localizer.Get("AppName"));
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, Localizer.Get("BackupFailed"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = Localizer.Get("BackupFilter") };
        if (dialog.ShowDialog(this) != true) return;
        if (MessageBox.Show(this, Localizer.Get("RestoreBackupQuestion"),
                Localizer.Get("ConfirmBackupRestore"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }
        await _app.RestartAndRestoreAsync(dialog.FileName);
    }
}
