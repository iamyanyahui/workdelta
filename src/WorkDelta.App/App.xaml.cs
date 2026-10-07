using System.IO;
using System.Diagnostics;
using System.Windows;
using WorkDelta.App.Localization;
using WorkDelta.App.Services;
using WorkDelta.Core.Models;
using WorkDelta.Core.Services;
using MessageBox = System.Windows.MessageBox;

namespace WorkDelta.App;

public partial class App : System.Windows.Application
{
    private TrayService? _tray;
    private MainWindow? _mainWindow;
    private bool _isExiting;

    public WorkDeltaStore Store { get; private set; } = null!;
    public TrackingEngine Engine { get; private set; } = null!;
    public AppIdentity Identity { get; private set; } = null!;
    public BackupService Backup { get; private set; } = null!;
    public string DataRoot { get; private set; } = string.Empty;
    public StartupRegistrationService StartupRegistration { get; } = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            DataRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WorkDelta");
            Directory.CreateDirectory(DataRoot);

            var restoreIndex = Array.IndexOf(e.Args, "--restore-backup");
            if (restoreIndex >= 0 && restoreIndex + 1 < e.Args.Length)
            {
                BackupService.RestoreBeforeStartup(DataRoot, e.Args[restoreIndex + 1]);
            }

            Store = new WorkDeltaStore(Path.Combine(DataRoot, "workdelta.db"));
            await Store.InitializeAsync();
            Localizer.Apply(await Store.GetSettingValueAsync("ui_language"));
            Backup = new BackupService(DataRoot, Store);
            Identity = await Store.GetOrCreateIdentityAsync();
            var pathPolicy = new PathPolicy();
            var snapshotStore = new GitSnapshotStore(Path.Combine(DataRoot, "Repositories"), pathPolicy);
            Engine = new TrackingEngine(Store, snapshotStore, pathPolicy, Identity);
            await Engine.InitializeAsync();

            _mainWindow = new MainWindow(this);
            MainWindow = _mainWindow;
            _tray = new TrayService(
                ShowMainWindow,
                async () => await ExitAsync(),
                Localizer.Get("TrayTooltip"));
            _mainWindow.Show();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                Localizer.Format("StartupFailed", exception.Message),
                Localizer.Get("AppName"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    public void HideToTray()
    {
        if (_isExiting || _mainWindow is null)
        {
            return;
        }

        _mainWindow.Hide();
        _tray?.ShowBackgroundNotice();
    }

    public void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }
        _mainWindow.Activate();
    }

    public async Task ExitAsync()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        _tray?.Dispose();
        if (Engine is not null)
        {
            await Engine.DisposeAsync();
        }
        _mainWindow?.AllowClose();
        _mainWindow?.Close();
        Shutdown();
    }

    public async Task RestartAndRestoreAsync(string backupPath)
    {
        if (_isExiting)
        {
            return;
        }
        _isExiting = true;
        _tray?.Dispose();
        await Engine.DisposeAsync();

        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException(Localizer.Get("ExecutableMissing"));
        var startInfo = new ProcessStartInfo(executable) { UseShellExecute = true };
        startInfo.ArgumentList.Add("--restore-backup");
        startInfo.ArgumentList.Add(backupPath);
        Process.Start(startInfo);
        _mainWindow?.AllowClose();
        _mainWindow?.Close();
        Shutdown();
    }

    public async Task ChangeLanguageAsync(string language)
    {
        if (language == Localizer.LanguageSetting)
        {
            return;
        }

        await Store.SetSettingValueAsync("ui_language", language);
        MessageBox.Show(
            MainWindow,
            Localizer.Get("RestartForLanguage"),
            Localizer.Get("LanguageChange"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        await RestartAsync();
    }

    private async Task RestartAsync()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        _tray?.Dispose();
        if (Engine is not null)
        {
            await Engine.DisposeAsync();
        }

        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException(Localizer.Get("ExecutableMissing"));
        Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true });
        _mainWindow?.AllowClose();
        _mainWindow?.Close();
        Shutdown();
    }
}
