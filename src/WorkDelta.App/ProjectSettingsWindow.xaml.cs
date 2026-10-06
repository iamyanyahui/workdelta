using System.IO;
using System.Windows;
using Microsoft.Win32;
using WorkDelta.Core.Models;
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
        var dialog = new OpenFolderDialog { Title = "选择新的项目文件夹" };
        if (Directory.Exists(ProjectPath)) dialog.InitialDirectory = ProjectPath;
        if (dialog.ShowDialog(this) == true) PathTextBox.Text = dialog.FolderName;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ProjectName))
        {
            MessageBox.Show(this, "请输入项目名称。", "工迹 WorkDelta");
            return;
        }
        if (!Directory.Exists(ProjectPath))
        {
            MessageBox.Show(this, "项目文件夹不存在。", "工迹 WorkDelta");
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
