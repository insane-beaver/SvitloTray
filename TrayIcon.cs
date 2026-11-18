using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SvitloTray.Core;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace SvitloTray;

public sealed class TrayIcon : IDisposable
{
    static readonly Drawing.Color OnColor = Drawing.Color.FromArgb(0x30, 0xA4, 0x6C);
    static readonly Drawing.Color OffColor = Drawing.Color.FromArgb(0xE5, 0x48, 0x4D);
    static readonly Drawing.Color MaybeColor = Drawing.Color.FromArgb(0xF5, 0xA5, 0x24);
    static readonly Drawing.Color UnknownColor = Drawing.Color.FromArgb(0x8B, 0x92, 0x9C);

    static readonly Drawing.Icon OnIcon = MakeIcon(OnColor, null);

    readonly Forms.NotifyIcon _icon = new();
    Drawing.Icon? _current;
    string? _currentKey;

    public event Action? FlyoutRequested;
    public event Action? OpenRequested;
    public event Action? RefreshRequested;
    public event Action? ExitRequested;

    public TrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Відкрити графік", null, (_, _) => OpenRequested?.Invoke());
        menu.Items.Add("Оновити зараз", null, (_, _) => RefreshRequested?.Invoke());

        var autostart = new Forms.ToolStripMenuItem("Запускати з Windows") { CheckOnClick = true };
        menu.Opening += (_, _) => autostart.Checked = Autostart.IsEnabled;
        autostart.Click += (_, _) => Autostart.Set(autostart.Checked);
        menu.Items.Add(autostart);

        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Вихід", null, (_, _) => ExitRequested?.Invoke());

        _icon.ContextMenuStrip = menu;
        _icon.Text = "SvitloTray";
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) FlyoutRequested?.Invoke();
        };
        _icon.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) OpenRequested?.Invoke();
        };
        Update(null, null, "SvitloTray");
        _icon.Visible = true;
    }

    public static ImageSource WindowIcon { get; } = Imaging.CreateBitmapSourceFromHIcon(
        OnIcon.Handle, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

    // minutesLeft draws a countdown on the icon instead of the bolt
    public void Update(SlotStatus? status, int? minutesLeft, string tooltip)
    {
        var key = $"{status}|{minutesLeft}";
        if (key != _currentKey)
        {
            var color = status switch
            {
                SlotStatus.On => OnColor,
                SlotStatus.Off => OffColor,
                SlotStatus.Maybe => MaybeColor,
                _ => UnknownColor,
            };
            var next = MakeIcon(color, minutesLeft?.ToString());
            _icon.Icon = next;
            _current?.Dispose();
            _current = next;
            _currentKey = key;
        }
        _icon.Text = tooltip.Length > 127 ? tooltip[..127] : tooltip;
    }

    public void Notify(string title, string text)
    {
        if (!Toasts.TryShow(title, text))
            _icon.ShowBalloonTip(5000, title, text, Forms.ToolTipIcon.None);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _current?.Dispose();
    }

    static Drawing.Icon MakeIcon(Drawing.Color color, string? text)
    {
        using var bmp = new Drawing.Bitmap(32, 32);
        using (var g = Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.Clear(Drawing.Color.Transparent);
            using var fill = new Drawing.SolidBrush(color);
            using var white = new Drawing.SolidBrush(Drawing.Color.White);

            if (text is null)
            {
                g.FillEllipse(fill, 2, 2, 28, 28);
                g.FillPolygon(white, new Drawing.Point[]
                {
                    new(18, 5), new(9, 18), new(15, 18), new(13, 27), new(23, 13), new(17, 13), new(20, 5),
                });
            }
            else
            {
                g.FillEllipse(fill, 0, 0, 32, 32);
                using var font = new Drawing.Font("Segoe UI", text.Length > 1 ? 15 : 18, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Pixel);
                using var format = new Drawing.StringFormat
                {
                    Alignment = Drawing.StringAlignment.Center,
                    LineAlignment = Drawing.StringAlignment.Center,
                };
                g.DrawString(text, font, white, new Drawing.RectangleF(0, 1, 32, 32), format);
            }
        }

        var handle = bmp.GetHicon();
        try { return (Drawing.Icon)Drawing.Icon.FromHandle(handle).Clone(); }
        finally { DestroyIcon(handle); }
    }

    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr handle);
}
