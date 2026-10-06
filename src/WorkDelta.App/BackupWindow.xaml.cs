using System.Windows;
using Microsoft.Win32;
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
            Filter = "工迹备份 (*.workdelta)|*.workdelta",
            FileName = $"WorkDelta-{DateTime.Now:yyyyMMdd-HHmm}.workdelta"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            IsEnabled = false;
            await _app.Backup.ExportAsync(dialog.FileName);
            MessageBox.Show(this, "完整备份已经导出。", "工迹 WorkDelta");
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "备份失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "工迹备份 (*.workdelta)|*.workdelta" };
        if (dialog.ShowDialog(this) != true) return;
        if (MessageBox.Show(this, "恢复会替换本机现有的全部工迹记录。确定继续吗？",
                "确认恢复备份", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }
        await _app.RestartAndRestoreAsync(dialog.FileName);
    }
}
