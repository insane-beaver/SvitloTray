using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SvitloTray.Core;

namespace SvitloTray;

// Draws a whole day in one element instead of 48 Borders
public sealed class ScheduleBar : FrameworkElement
{
    const double HourGap = 2;

    readonly ToolTip _tip = new();
    SlotStatus[] _slots = [];
    DateTime _dayStart;
    double _nowFraction = -1;
    int _tipIndex = -1;

    public ScheduleBar()
    {
        ToolTip = _tip;
        ToolTipService.SetInitialShowDelay(this, 100);
        ToolTipService.SetPlacement(this, System.Windows.Controls.Primitives.PlacementMode.Mouse);
    }

    public double CornerRadius { get; set; } = 4;

    public void SetDay(SlotStatus[] slots, DateTime dayStart)
    {
        _slots = slots;
        _dayStart = dayStart;
        _tipIndex = -1;
        InvalidateVisual();
    }

    // 0..1 position of the "now" line, negative = hidden
    public double NowFraction
    {
        get => _nowFraction;
        set
        {
            if (Math.Abs(value - _nowFraction) < 0.0001) return;
            _nowFraction = value;
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));
        if (_slots.Length < 2 || w <= 0) return;

        var hours = _slots.Length / 2;
        var hourW = w / hours;
        var cellW = Math.Max(1, hourW - HourGap);
        var r = Math.Min(CornerRadius, h / 2);

        for (var i = 0; i < hours; i++)
        {
            var cell = new Rect(i * hourW + HourGap / 2, 0, cellW, h);
            var a = _slots[i * 2];
            var b = _slots[i * 2 + 1];

            if (a == b)
            {
                dc.DrawRoundedRectangle(StatusBrushes.For(a), null, cell, r, r);
                continue;
            }

            dc.PushClip(new RectangleGeometry(cell, r, r));
            dc.DrawRectangle(StatusBrushes.For(a), null, new Rect(cell.X, 0, cellW / 2, h));
            dc.DrawRectangle(StatusBrushes.For(b), null, new Rect(cell.X + cellW / 2, 0, cellW / 2, h));
            dc.Pop();
        }

        if (_nowFraction is >= 0 and <= 1)
        {
            var x = Math.Round(_nowFraction * w) + 0.5;
            var brush = TryFindResource("TextFillColorPrimaryBrush") as Brush ?? Brushes.Black;
            dc.DrawLine(new Pen(brush, 2), new Point(x, -4), new Point(x, h + 4));
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_slots.Length == 0 || ActualWidth <= 0) return;

        var i = Math.Clamp((int)(e.GetPosition(this).X / (ActualWidth / _slots.Length)), 0, _slots.Length - 1);
        if (i == _tipIndex) return;

        _tipIndex = i;
        var start = _dayStart.AddMinutes(30 * i);
        _tip.Content = $"{start:HH:mm}–{start.AddMinutes(30):HH:mm} · {ScheduleLogic.StatusName(_slots[i])}";
    }
}

public static class StatusBrushes
{
    public static readonly Brush On = Make(0x30, 0xA4, 0x6C);
    public static readonly Brush Off = Make(0xE5, 0x48, 0x4D);
    public static readonly Brush Maybe = Make(0xF5, 0xA5, 0x24);
    public static readonly Brush Unknown = Make(0x8B, 0x92, 0x9C);

    public static Brush For(SlotStatus? status) => status switch
    {
        SlotStatus.On => On,
        SlotStatus.Off => Off,
        SlotStatus.Maybe => Maybe,
        _ => Unknown,
    };

    static Brush Make(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
