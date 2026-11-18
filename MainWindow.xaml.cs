using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using SvitloTray.Core;

namespace SvitloTray;

public partial class MainWindow : Window
{
    static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk-UA");
    static readonly int[] WarnOptions = [5, 10, 15, 30, 60];

    readonly ScheduleService _service;
    readonly HashSet<DateOnly> _expanded = [];
    readonly List<ScheduleBar> _todayBars = [];
    bool _loading = true;

    ScheduleSnapshot? _renderedSnapshot;
    string? _renderedGroup;
    DateOnly _renderedDate;
    bool _daysRendered;

    public MainWindow(ScheduleService service)
    {
        _service = service;
        InitializeComponent();
        Icon = TrayIcon.WindowIcon;

        RegionBox.ItemsSource = Regions.All;
        RegionBox.SelectedItem = Regions.All.FirstOrDefault(r => r.Id == service.Settings.Region) ?? Regions.All[0];

        QueueBox.ItemsSource = Regions.Groups.Select(Regions.GroupLabel).ToList();
        QueueBox.SelectedItem = Regions.GroupLabel(service.Settings.Group);

        WarnBox.ItemsSource = WarnOptions.Select(m => $"{m} хв").ToList();
        WarnBox.SelectedIndex = Math.Max(0, Array.IndexOf(WarnOptions, service.Settings.WarnMinutes));

        AutostartBox.IsChecked = Autostart.IsEnabled;

        _loading = false;
        Render();
    }

    public void Render()
    {
        var now = KyivTime.Now;
        var today = DateOnly.FromDateTime(now);
        var group = _service.Settings.Group;
        var snapshot = _service.Snapshot;

        RenderStatus(snapshot, group, now);

        if (!_daysRendered || !ReferenceEquals(snapshot, _renderedSnapshot) || group != _renderedGroup || today != _renderedDate)
        {
            RenderDays(snapshot, group, now);
            _renderedSnapshot = snapshot;
            _renderedGroup = group;
            _renderedDate = today;
            _daysRendered = true;
        }

        var fraction = (now - today.ToDateTime(TimeOnly.MinValue)).TotalMinutes / 1440;
        foreach (var bar in _todayBars) bar.NowFraction = fraction;

        RenderFooter(snapshot);
    }

    void RenderStatus(ScheduleSnapshot? snapshot, string group, DateTime now)
    {
        SubtitleText.Text = $"{Regions.NameOf(_service.Settings.Region)} · підчерга {Regions.GroupLabel(group)}";

        var view = ScheduleLogic.Present(snapshot, group, now);
        StatusBadge.Background = StatusBrushes.For(view.Status);
        ProgressFill.Background = StatusBrushes.For(view.Status);
        StatusText.Text = view.Title;
        StatusInfoText.Text = view.Info;
        CountdownText.Text = view.Countdown;
        UntilText.Text = view.Until;
        SetProgress(view.Progress);
    }

    void SetProgress(double fraction)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        ProgressGrid.ColumnDefinitions[0].Width = new GridLength(fraction, GridUnitType.Star);
        ProgressGrid.ColumnDefinitions[1].Width = new GridLength(1 - fraction, GridUnitType.Star);
    }

    void RenderDays(ScheduleSnapshot? snapshot, string group, DateTime now)
    {
        DaysPanel.Children.Clear();
        _todayBars.Clear();

        var today = DateOnly.FromDateTime(now);
        var days = snapshot?.Days.Where(d => d.Date >= today).ToList() ?? [];

        if (days.Count == 0)
        {
            var empty = Caption(snapshot is null
                ? "Завантажую графік..."
                : "Графіка на сьогодні немає: або відключень не планують, або обленерго ще не опублікувало.");
            empty.FontSize = 13;
            empty.TextWrapping = TextWrapping.Wrap;
            DaysPanel.Children.Add(Card(empty));
            return;
        }

        foreach (var day in days)
        {
            var dayStart = day.Date.ToDateTime(TimeOnly.MinValue);
            var isToday = day.Date == today;
            var content = new StackPanel();

            var header = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var prefix = isToday ? "Сьогодні" : day.Date == today.AddDays(1) ? "Завтра" : "";
            var date = dayStart.ToString("dddd, d MMMM", Uk);
            header.Children.Add(new TextBlock
            {
                Text = prefix.Length > 0 ? $"{prefix}, {date}" : date,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
            });

            if (day.Groups.TryGetValue(group, out var mine))
            {
                var summary = Caption(DaySummary(mine));
                summary.FontSize = 13;
                summary.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(summary, 1);
                header.Children.Add(summary);
            }
            content.Children.Add(header);

            content.Children.Add(Row("", HourAxis(), 16));
            if (mine is not null)
                content.Children.Add(Row(Regions.GroupLabel(group), NewBar(mine, dayStart, isToday, 34), 34, bold: true));

            var all = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            foreach (var g in Regions.Groups.Where(day.Groups.ContainsKey))
                all.Children.Add(Row(Regions.GroupLabel(g), NewBar(day.Groups[g], dayStart, isToday, 16), 16, bold: g == group));

            var expander = new Expander
            {
                Header = "Усі підчерги",
                Content = all,
                Margin = new Thickness(0, 10, 0, 0),
                IsExpanded = _expanded.Contains(day.Date),
            };
            var key = day.Date;
            expander.Expanded += (_, _) => _expanded.Add(key);
            expander.Collapsed += (_, _) => _expanded.Remove(key);
            content.Children.Add(expander);

            DaysPanel.Children.Add(Card(content));
        }
    }

    ScheduleBar NewBar(SlotStatus[] slots, DateTime dayStart, bool isToday, double height)
    {
        var bar = new ScheduleBar { Height = height, CornerRadius = height > 20 ? 5 : 3 };
        bar.SetDay(slots, dayStart);
        if (isToday) _todayBars.Add(bar);
        return bar;
    }

    void RenderFooter(ScheduleSnapshot? snapshot)
    {
        FooterText.Inlines.Clear();
        var (sourceName, sourceUrl) = ScheduleSource.Credit(_service.Settings.Region);
        FooterText.Inlines.Add(new Run("Дані: "));
        var link = new Hyperlink(new Run(sourceName)) { ToolTip = sourceUrl };
        link.Click += (_, _) => Process.Start(new ProcessStartInfo(sourceUrl) { UseShellExecute = true });
        FooterText.Inlines.Add(link);

        if (snapshot?.LastUpdated is { } updated)
            FooterText.Inlines.Add(new Run($" · графік оновлено {KyivTime.FromOffset(updated):dd.MM HH:mm}"));
        if (_service.LastFetch is { } fetched)
            FooterText.Inlines.Add(new Run($" · перевірено {fetched:HH:mm}"));

        ErrorText.Text = _service.LastError is { } error ? $"Не вдалося оновити: {error}" : "";
        ErrorText.Visibility = _service.LastError is null ? Visibility.Collapsed : Visibility.Visible;
    }

    static string DaySummary(SlotStatus[] slots)
    {
        var (off, maybe) = ScheduleLogic.Totals(slots);
        if (off == TimeSpan.Zero && maybe == TimeSpan.Zero) return "Без відключень";

        var parts = new List<string>();
        if (off > TimeSpan.Zero) parts.Add($"без світла {ScheduleLogic.FormatDuration(off)}");
        if (maybe > TimeSpan.Zero) parts.Add($"можливо ще {ScheduleLogic.FormatDuration(maybe)}");
        var text = string.Join(" · ", parts);
        return char.ToUpper(text[0]) + text[1..];
    }

    static Border Card(UIElement child)
    {
        var card = new Border { Child = child, Margin = new Thickness(0, 0, 0, 12) };
        card.SetResourceReference(StyleProperty, "Card");
        return card;
    }

    static TextBlock Caption(string text)
    {
        var t = new TextBlock { Text = text };
        t.SetResourceReference(StyleProperty, "Caption");
        return t;
    }

    static Grid Row(string label, UIElement content, double height, bool bold = false)
    {
        var row = new Grid { Height = height, Margin = new Thickness(0, 0, 0, 4) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
        row.ColumnDefinitions.Add(new ColumnDefinition());

        var text = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = height < 20 ? 11 : 14,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
        };
        if (!bold) text.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        row.Children.Add(text);

        Grid.SetColumn(content, 1);
        row.Children.Add(content);
        return row;
    }

    static Grid HourAxis()
    {
        var axis = new Grid();
        for (var h = 0; h < 24; h++)
        {
            axis.ColumnDefinitions.Add(new ColumnDefinition());
            var t = new TextBlock
            {
                Text = h.ToString("00"),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
            };
            t.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorTertiaryBrush");
            Grid.SetColumn(t, h);
            axis.Children.Add(t);
        }
        return axis;
    }

    async void RegionBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || RegionBox.SelectedItem is not Region region) return;
        await _service.SetRegionAsync(region.Id);
    }

    void QueueBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || QueueBox.SelectedItem is not string label) return;
        _service.SetGroup("GPV" + label);
    }

    void WarnBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || WarnBox.SelectedIndex < 0) return;
        _service.SetWarnMinutes(WarnOptions[WarnBox.SelectedIndex]);
    }

    void AutostartBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        Autostart.Set(AutostartBox.IsChecked == true);
    }

    async void Refresh_Click(object sender, RoutedEventArgs e) => await _service.RefreshAsync();
}
