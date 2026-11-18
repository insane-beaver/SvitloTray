using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using SvitloTray.Core;

namespace SvitloTray;

// Small popup above the tray, closes when it loses focus
public partial class TrayFlyout : Window
{
    readonly ScheduleService _service;
    bool _closing;

    public event Action? OpenRequested;

    public TrayFlyout(ScheduleService service)
    {
        _service = service;
        InitializeComponent();

        var light = IsLightTheme();
        Background = new SolidColorBrush(light ? Color.FromRgb(0xF9, 0xF9, 0xF9) : Color.FromRgb(0x20, 0x20, 0x20));
        Foreground = light ? Brushes.Black : Brushes.White;

        SourceInitialized += (_, _) => RoundCorners();
        Loaded += (_, _) =>
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - ActualWidth - 12;
            Top = area.Bottom - ActualHeight - 12;
            Opacity = 1;
        };
        Closing += (_, _) => _closing = true;
        Deactivated += (_, _) => SafeClose();
        Render();
    }

    public void Render()
    {
        var now = KyivTime.Now;
        var group = _service.Settings.Group;
        var view = ScheduleLogic.Present(_service.Snapshot, group, now);

        StatusBadge.Background = StatusBrushes.For(view.Status);
        StatusText.Text = view.Title;
        SubtitleText.Text = $"{Regions.NameOf(_service.Settings.Region)} · підчерга {Regions.GroupLabel(group)}";
        CountdownText.Text = view.Countdown;
        UntilText.Text = view.Until;
        InfoText.Text = view.Info;
        InfoText.Visibility = view.Info.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        var today = DateOnly.FromDateTime(now);
        var day = _service.Snapshot?.Days.FirstOrDefault(d => d.Date == today);
        if (day is not null && day.Groups.TryGetValue(group, out var slots))
        {
            var start = today.ToDateTime(TimeOnly.MinValue);
            TodayBar.SetDay(slots, start);
            TodayBar.NowFraction = (now - start).TotalMinutes / 1440;
            TodayBar.Visibility = Visibility.Visible;
            NoTodayText.Visibility = Visibility.Collapsed;
        }
        else
        {
            TodayBar.Visibility = Visibility.Collapsed;
            NoTodayText.Visibility = Visibility.Visible;
        }
    }

    void Open_Click(object sender, RoutedEventArgs e)
    {
        SafeClose();
        OpenRequested?.Invoke();
    }

    void SafeClose()
    {
        if (_closing) return;
        _closing = true;
        Close();
    }

    async void Refresh_Click(object sender, RoutedEventArgs e) => await _service.RefreshAsync();

    static bool IsLightTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is not int v || v != 0;
    }

    void RoundCorners()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var preference = 2; // DWMWCP_ROUND
        DwmSetWindowAttribute(hwnd, 33, ref preference, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
