using System;
using System.Windows;
using System.Windows.Interop;

namespace Mate.MVVM.Views;

public partial class HotZoneWindow : Window
{
    public const int ZoneWidth = 260;
    public const int ZoneHeight = 5;

    private const int WmNcHitTest = 0x0084;
    private static readonly IntPtr HitTestTransparent = new(-1);
    private HwndSource? _source;

    public HotZoneWindow()
    {
        InitializeComponent();
        SourceInitialized += HotZoneWindow_SourceInitialized;
        Closed += HotZoneWindow_Closed;
    }

    public void PositionAtTopCenter()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Top;
    }

    private void HotZoneWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(handle);
        _source?.AddHook(WindowProc);
    }

    private void HotZoneWindow_Closed(object? sender, EventArgs e)
    {
        _source?.RemoveHook(WindowProc);
        _source = null;
        SourceInitialized -= HotZoneWindow_SourceInitialized;
        Closed -= HotZoneWindow_Closed;
    }

    private static IntPtr WindowProc(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message != WmNcHitTest) return IntPtr.Zero;

        handled = true;
        return HitTestTransparent;
    }
}
