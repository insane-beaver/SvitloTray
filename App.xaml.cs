using System.Windows;
using System.Windows.Threading;
using SvitloTray.Core;

namespace SvitloTray;

public partial class App : Application
{
    Mutex? _mutex;
    ScheduleService? _service;
    TrayIcon? _tray;
    MainWindow? _window;
    TrayFlyout? _flyout;
    DateTime _flyoutClosedAt;

    readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(30) };
    readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromMinutes(5) };
    readonly HashSet<DateTime> _warned = [];
    SlotStatus? _lastStatus;

    protected override async void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, "SvitloTray.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);

        var firstRun = !AppSettings.Exists;
        var service = _service = new ScheduleService(AppSettings.Load());
        service.LoadCache();

        var tray = _tray = new TrayIcon();
        tray.FlyoutRequested += ToggleFlyout;
        tray.OpenRequested += ShowWindow;
        tray.RefreshRequested += async () => await service.RefreshAsync();
        tray.ExitRequested += () => Shutdown();

        Toasts.Init();
        Toasts.Clicked += ShowWindow;

        service.Updated += OnTick;
        service.SettingsChanged += () => { _warned.Clear(); _lastStatus = null; };
        service.ScheduleChanged += msg => tray.Notify("Графік оновився", msg);

        _tick.Tick += (_, _) => OnTick();
        _poll.Tick += async (_, _) => await service.RefreshAsync();
        _tick.Start();
        _poll.Start();

        OnTick();

        if (firstRun)
        {
            service.Settings.Save();
            ShowWindow();
        }
        else if (Toasts.LaunchedFromToast)
        {
            ShowWindow();
        }

        await service.RefreshAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    void OnTick()
    {
        if (_service is null || _tray is null) return;

        var now = KyivTime.Now;
        var group = _service.Settings.Group;
        var label = Regions.GroupLabel(group);
        var state = ScheduleLogic.GetState(_service.Snapshot, group, now);
        var view = ScheduleLogic.Present(_service.Snapshot, group, now);

        var tooltip = view.Until.Length > 0 && view.Countdown.Length > 0
            ? $"Підчерга {label}: {view.Title}\n{view.Until} · ще {view.Countdown}"
            : $"Підчерга {label}: {view.Title}";
        _tray.Update(view.Status, view.MinutesLeft, tooltip);

        if (state.NextOff is { } off)
        {
            var left = off - now;
            if (left > TimeSpan.Zero && left.TotalMinutes <= _service.Settings.WarnMinutes && _warned.Add(off))
                _tray.Notify($"Відключення о {ScheduleLogic.FormatTime(off, now)}",
                    $"Через {ScheduleLogic.FormatDuration(left)}, підчерга {label}");
        }

        if (_lastStatus is SlotStatus.Off && state.Current is SlotStatus.On)
            _tray.Notify("Світло мало з'явитися", $"За графіком підчерга {label} вже зі світлом");
        _lastStatus = state.Current;

        _window?.Render();
        _flyout?.Render();
    }

    void ToggleFlyout()
    {
        if (_service is null) return;

        // A click on the tray icon first deactivates (closes) an open flyout; don't reopen it right away
        if (_flyout is not null || DateTime.UtcNow - _flyoutClosedAt < TimeSpan.FromMilliseconds(400)) return;

        var flyout = _flyout = new TrayFlyout(_service);
        flyout.OpenRequested += ShowWindow;
        flyout.Closed += (_, _) =>
        {
            _flyout = null;
            _flyoutClosedAt = DateTime.UtcNow;
        };
        flyout.Show();
        flyout.Activate();
    }

    void ShowWindow()
    {
        if (_service is null) return;

        if (_window is null)
        {
            _window = new MainWindow(_service);
            _window.Closed += (_, _) => _window = null;
            _window.Show();
        }

        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
    }
}
