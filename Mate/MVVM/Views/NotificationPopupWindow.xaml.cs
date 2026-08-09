using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using Mate.Models;

namespace Mate.MVVM.Views;

public partial class NotificationPopupWindow : Window, IDisposable
{
    private const double DesignWidth = 360;
    private const double DesignHeight = 76;
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private readonly Queue<MateNotification> _queue = new();
    private readonly CancellationTokenSource _cancellation = new();
    private bool _isShowingNotification;
    private bool _disposed;
    private double _interfaceScale = 1;

    public NotificationPopupWindow()
    {
        InitializeComponent();
        SourceInitialized += NotificationPopupWindow_SourceInitialized;
    }

    public void ApplyScale(double scale)
    {
        _interfaceScale = Math.Max(0.5, scale);
        Width = DesignWidth * _interfaceScale;
        Height = DesignHeight * _interfaceScale;
    }

    public void ShowNotification(MateNotification notification)
    {
        if (_disposed) return;
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => ShowNotification(notification)));
            return;
        }

        _queue.Enqueue(notification);
        if (!_isShowingNotification) _ = ShowNextAsync();
    }

    private async Task ShowNextAsync()
    {
        if (_disposed || _queue.Count == 0)
        {
            _isShowingNotification = false;
            return;
        }

        _isShowingNotification = true;
        var notification = _queue.Dequeue();
        DataContext = notification;
        PositionAtTopCenter();

        PopupScaleTransform.ScaleX = 0.82;
        PopupScaleTransform.ScaleY = 0.76;
        PopupTranslateTransform.Y = -DesignHeight * 0.72;
        Opacity = 0;
        Show();

        var openEase = new CubicEase { EasingMode = EasingMode.EaseOut };
        BeginAnimation(OpacityProperty, CreateAnimation(1, 360, openEase));
        PopupScaleTransform.BeginAnimation(
            System.Windows.Media.ScaleTransform.ScaleXProperty,
            CreateAnimation(1, 420, openEase));
        PopupScaleTransform.BeginAnimation(
            System.Windows.Media.ScaleTransform.ScaleYProperty,
            CreateAnimation(1, 420, openEase));
        PopupTranslateTransform.BeginAnimation(
            System.Windows.Media.TranslateTransform.YProperty,
            CreateAnimation(0, 420, openEase));

        try
        {
            await Task.Delay(4200, _cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var closeEase = new CubicEase { EasingMode = EasingMode.EaseIn };
        BeginAnimation(OpacityProperty, CreateAnimation(0, 320, closeEase));
        PopupScaleTransform.BeginAnimation(
            System.Windows.Media.ScaleTransform.ScaleXProperty,
            CreateAnimation(0.86, 320, closeEase));
        PopupScaleTransform.BeginAnimation(
            System.Windows.Media.ScaleTransform.ScaleYProperty,
            CreateAnimation(0.78, 320, closeEase));
        PopupTranslateTransform.BeginAnimation(
            System.Windows.Media.TranslateTransform.YProperty,
            CreateAnimation(-DesignHeight * 0.72, 320, closeEase));

        try
        {
            await Task.Delay(340, _cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        Hide();
        BeginAnimation(OpacityProperty, null);
        PopupScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, null);
        PopupScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, null);
        PopupTranslateTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, null);

        _isShowingNotification = false;
        if (_queue.Count > 0) await ShowNextAsync();
    }

    private void PositionAtTopCenter()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Top + 9 * _interfaceScale;
    }

    private void NotificationPopupWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var currentStyle = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        var newStyle = new IntPtr(currentStyle | WsExTransparent | WsExToolWindow | WsExNoActivate);
        SetWindowLongPtr(handle, GwlExStyle, newStyle);
    }

    private static DoubleAnimation CreateAnimation(double to, int milliseconds, IEasingFunction easing) =>
        new()
        {
            To = to,
            Duration = TimeSpan.FromMilliseconds(milliseconds),
            EasingFunction = easing,
            FillBehavior = FillBehavior.HoldEnd
        };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cancellation.Cancel();
        _cancellation.Dispose();
        _queue.Clear();
        SourceInitialized -= NotificationPopupWindow_SourceInitialized;
        Close();
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern IntPtr GetWindowLong32(IntPtr windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr windowHandle, int index, IntPtr newLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern IntPtr SetWindowLong32(IntPtr windowHandle, int index, IntPtr newLong);

    private static IntPtr GetWindowLongPtr(IntPtr windowHandle, int index) =>
        IntPtr.Size == 8
            ? GetWindowLongPtr64(windowHandle, index)
            : GetWindowLong32(windowHandle, index);

    private static IntPtr SetWindowLongPtr(IntPtr windowHandle, int index, IntPtr newLong) =>
        IntPtr.Size == 8
            ? SetWindowLongPtr64(windowHandle, index, newLong)
            : SetWindowLong32(windowHandle, index, newLong);
}
