using System.Windows;
using Microsoft.Toolkit.Uwp.Notifications;

namespace SvitloTray;

// Windows toasts (stay in Action Center); TrayIcon falls back to balloon tips if these fail
public static class Toasts
{
    static bool _available = true;

    public static event Action? Clicked;

    public static bool LaunchedFromToast
    {
        get
        {
            try { return ToastNotificationManagerCompat.WasCurrentProcessToastActivated(); }
            catch { return false; }
        }
    }

    public static void Init()
    {
        try
        {
            ToastNotificationManagerCompat.OnActivated += _ =>
                Application.Current?.Dispatcher.InvokeAsync(() => Clicked?.Invoke());
        }
        catch
        {
            _available = false;
        }
    }

    public static bool TryShow(string title, string text)
    {
        if (!_available) return false;
        try
        {
            new ToastContentBuilder()
                .AddArgument("action", "open")
                .AddText(title)
                .AddText(text)
                .Show();
            return true;
        }
        catch
        {
            _available = false;
            return false;
        }
    }
}
