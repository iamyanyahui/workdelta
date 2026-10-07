using System.IO;
using System.Windows;
using Microsoft.Win32;
using WorkDelta.Core.Models;
using WorkDelta.App.Localization;
using MessageBox = System.Windows.MessageBox;

namespace WorkDelta.App;

public partial class ProjectSettingsWindow : Window
{
    public bool DeleteRequested { get; private set; }
    public string ProjectName => NameTextBox.Text.Trim();
    public string ProjectPath => PathTextBox.Text.Trim();
    public string IgnorePatterns => IgnoreTextBox.Text;

    public ProjectSettingsWindow(ProjectRecord project, string ignorePatterns)
    {
        InitializeComponent();
        NameTextBox.Text = project.Name;
        PathTextBox.Text = project.Path;
        IgnoreTextBox.Text = ignorePatterns;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = Localizer.Get("ChooseNewProjectFolder") };
        if (Directory.Exists(ProjectPath)) dialog.InitialDirectory = ProjectPath;
        if (dialog.ShowDialog(this) == true) PathTextBox.Text = dialog.FolderName;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ProjectName))
        {
            MessageBox.Show(this, Localizer.Get("EnterProjectName"), Localizer.Get("AppName"));
            return;
        }
        if (!Directory.Exists(ProjectPath))
        {
            MessageBox.Show(this, Localizer.Get("ProjectFolderMissing"), Localizer.Get("AppName"));
            return;
        }
        DialogResult = true;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        DeleteRequested = true;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
