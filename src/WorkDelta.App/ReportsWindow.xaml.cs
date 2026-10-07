using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WorkDelta.App.Localization;
using WorkDelta.Core.Models;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace WorkDelta.App;

public partial class ReportsWindow : Window
{
    private readonly App _app;
    private readonly ObservableCollection<ReportRow> _rows = [];
    private bool _loaded;

    public ReportsWindow(App app, ProjectRecord? selectedProject = null)
    {
        _app = app;
        InitializeComponent();
        ReportGrid.ItemsSource = _rows;
        Loaded += async (_, _) =>
        {
            var today = DateTime.Today;
            var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            StartDatePicker.SelectedDate = monday;
            EndDatePicker.SelectedDate = today;
            var projects = await _app.Store.GetProjectsAsync();
            ProjectFilter.Items.Add(new ProjectChoice(null, Localizer.Get("AllProjects")));
            foreach (var project in projects)
            {
                ProjectFilter.Items.Add(new ProjectChoice(project.Id, project.Name));
            }
            ProjectFilter.SelectedItem = ProjectFilter.Items.Cast<ProjectChoice>()
                .FirstOrDefault(item => item.Id == selectedProject?.Id) ?? ProjectFilter.Items[0];
            _loaded = true;
            await ReloadAsync();
        };
    }

    private async void Filter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loaded)
        {
            await ReloadAsync();
        }
    }

    private async Task ReloadAsync()
    {
        if (StartDatePicker.SelectedDate is null || EndDatePicker.SelectedDate is null)
        {
            return;
        }
        var start = DateOnly.FromDateTime(StartDatePicker.SelectedDate.Value);
        var end = DateOnly.FromDateTime(EndDatePicker.SelectedDate.Value);
        var projectId = (ProjectFilter.SelectedItem as ProjectChoice)?.Id;
        var reports = await _app.Store.GetActivityReportsAsync(start, end, TimeZoneInfo.Local, projectId);
        _rows.Clear();
        foreach (var report in reports)
        {
            _rows.Add(new ReportRow(report));
        }
        var total = TimeSpan.FromTicks(reports.Sum(item => item.ActiveDuration.Ticks));
        TotalText.Text = Localizer.Format("ReportTotal", FormatDuration(total), reports.Sum(item => item.FileCount), reports.Sum(item => item.ChangeCount));
    }

    private async void ThisWeek_Click(object sender, RoutedEventArgs e)
    {
        var today = DateTime.Today;
        StartDatePicker.SelectedDate = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        EndDatePicker.SelectedDate = today;
        if (_loaded) await ReloadAsync();
    }

    private async void ThisMonth_Click(object sender, RoutedEventArgs e)
    {
        var today = DateTime.Today;
        StartDatePicker.SelectedDate = new DateTime(today.Year, today.Month, 1);
        EndDatePicker.SelectedDate = today;
        if (_loaded) await ReloadAsync();
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (StartDatePicker.SelectedDate is null || EndDatePicker.SelectedDate is null)
        {
            return;
        }
        var dialog = new SaveFileDialog
        {
            Filter = Localizer.Get("MarkdownFilter"),
            FileName = $"WorkDelta-{StartDatePicker.SelectedDate:yyyy-MM-dd}-{EndDatePicker.SelectedDate:yyyy-MM-dd}-{Localizer.Get("ReportFileSuffix")}.md"
        };
        if (dialog.ShowDialog(this) != true) return;

        var builder = new StringBuilder()
            .AppendLine($"# {Localizer.Get("ReportMarkdownTitle")}")
            .AppendLine()
            .AppendLine($"> {StartDatePicker.SelectedDate:yyyy-MM-dd} {Localizer.Get("To")} {EndDatePicker.SelectedDate:yyyy-MM-dd}")
            .AppendLine()
            .AppendLine($"| {Localizer.Get("Project")} | {Localizer.Get("Sessions")} | {Localizer.Get("Duration")} | {Localizer.Get("FilesTouched")} | {Localizer.Get("EffectiveChanges")} |")
            .AppendLine("| --- | ---: | ---: | ---: | ---: |");
        foreach (var row in _rows)
        {
            builder.AppendLine($"| {row.ProjectName.Replace("|", "\\|")} | {row.SessionCount} | {row.DurationText} | {row.FileCount} | {row.ChangeCount} |");
        }
        builder.AppendLine().AppendLine($"**{TotalText.Text}**");
        await File.WriteAllTextAsync(dialog.FileName, builder.ToString(), new UTF8Encoding(true));
        MessageBox.Show(this, Localizer.Get("ReportExported"), Localizer.Get("AppName"));
    }

    private static string FormatDuration(TimeSpan duration) => Localizer.Duration(duration);

    private sealed record ProjectChoice(string? Id, string Name);
    private sealed class ReportRow(ProjectActivityReport report)
    {
        public string ProjectName => report.ProjectName;
        public int SessionCount => report.SessionCount;
        public int FileCount => report.FileCount;
        public int ChangeCount => report.ChangeCount;
        public string DurationText => FormatDuration(report.ActiveDuration);
    }
}
