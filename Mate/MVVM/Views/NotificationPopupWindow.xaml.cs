using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Mate.Models;

namespace Mate.MVVM.Views;

public partial class NotificationPopupWindow : Window, IDisposable
{
    private const double DesignWidth = 360;
    private const double DesignHeight = 76;
    private const double StackOffset = 68;
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private readonly CancellationTokenSource _cancellation = new();
    private TaskCompletionSource<bool>? _dismissCompletion;
    private bool _isShowingNotification;
    private bool _disposed;
    private int _stackIndex;
    private double _interfaceScale = 1;

    public NotificationPopupWindow()
    {
        InitializeComponent();
        SourceInitialized += NotificationPopupWindow_SourceInitialized;
    }

    public event EventHandler<MateNotification>? NotificationActivated;

    public event EventHandler<MateNotification>? NotificationHidden;

    public bool IsBusy => _isShowingNotification;

    public void ApplyScale(double scale)
    {
        _interfaceScale = Math.Max(0.5, scale);
        Width = DesignWidth * _interfaceScale;
        Height = DesignHeight * _interfaceScale;
        if (IsVisible) PositionAtTopCenter();
    }

    public bool TryShowNotification(MateNotification notification, int stackIndex)
    {
        if (_disposed || _isShowingNotification) return false;
        if (!Dispatcher.CheckAccess()) return false;

        _stackIndex = Math.Max(0, stackIndex);
        _isShowingNotification = true;
        _ = ShowNotificationAsync(notification);
        return true;
    }

    public void SetStackIndex(int stackIndex, bool animate)
    {
        _stackIndex = Math.Max(0, stackIndex);
        if (!IsVisible) return;

        var targetTop = GetTargetTop();
        if (!animate || Math.Abs(Top - targetTop) < 0.1)
        {
            BeginAnimation(TopProperty, null);
            Top = targetTop;
            return;
        }

        var currentTop = Top;
        Top = targetTop;
        BeginAnimation(
            TopProperty,
            new DoubleAnimation(currentTop, targetTop, TimeSpan.FromMilliseconds(260))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            });
    }

    private async Task ShowNotificationAsync(MateNotification notification)
    {
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
            ScaleTransform.ScaleXProperty,
            CreateAnimation(1, 420, openEase));
        PopupScaleTransform.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            CreateAnimation(1, 420, openEase));
        PopupTranslateTransform.BeginAnimation(
            TranslateTransform.YProperty,
            CreateAnimation(0, 420, openEase));

        _dismissCompletion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var automaticDismiss = notification.IsPersistent
            ? Task.Delay(Timeout.InfiniteTimeSpan, _cancellation.Token)
            : Task.Delay(4200, _cancellation.Token);
        await Task.WhenAny(automaticDismiss, _dismissCompletion.Task);
        _dismissCompletion = null;
        if (_cancellation.IsCancellationRequested) return;

        var closeEase = new CubicEase { EasingMode = EasingMode.EaseIn };
        BeginAnimation(OpacityProperty, CreateAnimation(0, 320, closeEase));
        PopupScaleTransform.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            CreateAnimation(0.86, 320, closeEase));
        PopupScaleTransform.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            CreateAnimation(0.78, 320, closeEase));
        PopupTranslateTransform.BeginAnimation(
            TranslateTransform.YProperty,
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
        BeginAnimation(TopProperty, null);
        PopupScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        PopupScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        PopupTranslateTransform.BeginAnimation(TranslateTransform.YProperty, null);

        DataContext = null;
        _isShowingNotification = false;
        NotificationHidden?.Invoke(this, notification);
    }

    private void PopupSurface_MouseLeftButtonUp(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && IsInsideButton(source)) return;
        if (DataContext is not MateNotification { HasAction: true } notification) return;

        NotificationActivated?.Invoke(this, notification);
        _dismissCompletion?.TrySetResult(true);
        e.Handled = true;
    }

    private static bool IsInsideButton(DependencyObject source)
    {
        DependencyObject? current = source;
        while (current is not null)
        {
            if (current is System.Windows.Controls.Button) return true;
            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void DismissButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _dismissCompletion?.TrySetResult(true);
    }

    private void PositionAtTopCenter()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = GetTargetTop();
    }

    private double GetTargetTop() =>
        SystemParameters.WorkArea.Top
        + (9 + _stackIndex * StackOffset) * _interfaceScale;

    private void NotificationPopupWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var currentStyle = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        var newStyle = new IntPtr(currentStyle | WsExToolWindow | WsExNoActivate);
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
        _dismissCompletion?.TrySetResult(true);
        _dismissCompletion = null;
        _cancellation.Cancel();
        _cancellation.Dispose();
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
