using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Mate.MVVM.ViewModels;

namespace Mate.MVVM.Views;

public partial class MainWindow : Window
{
    private const double DesignWidth = 840;
    private const double DesignHeight = 340;
    private const double ReferenceScreenWidth = 3072;
    private const double ReferenceScreenHeight = 1728;
    private const double MinimumInterfaceScale = 0.5;
    private const double InterfaceScaleMultiplier = 1.2;
    private const double NavigationScrollStep = 42;

    private static readonly DependencyProperty NavigationScrollOffsetProperty =
        DependencyProperty.Register(
            nameof(NavigationScrollOffset),
            typeof(double),
            typeof(MainWindow),
            new PropertyMetadata(0d, NavigationScrollOffsetChanged));

    private readonly DispatcherTimer _folderHoverTimer;
    private NavigationItemViewModel? _folderHoverItem;
    private int _animationVersion;
    private int _navigationScrollAnimationVersion;
    private double _navigationTargetOffset;
    private bool _isNavigationScrollAnimating;
    private bool _closeCanBeCancelled;
    private bool? _navigationFadeTop;
    private bool? _navigationFadeBottom;

    public MainWindow()
    {
        InitializeComponent();
        _folderHoverTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = System.TimeSpan.FromMilliseconds(400)
        };
        _folderHoverTimer.Tick += FolderHoverTimer_Tick;
    }

    public bool AllowClose { get; set; }

    public bool IsClosingAnimation { get; private set; }

    public double InterfaceScale { get; private set; } = 1;

    private double NavigationScrollOffset
    {
        get => (double)GetValue(NavigationScrollOffsetProperty);
        set => SetValue(NavigationScrollOffsetProperty, value);
    }

    public void ApplyScreenScale()
    {
        var widthScale = SystemParameters.PrimaryScreenWidth / ReferenceScreenWidth;
        var heightScale = SystemParameters.PrimaryScreenHeight / ReferenceScreenHeight;
        var screenScale = System.Math.Max(
            MinimumInterfaceScale,
            System.Math.Min(widthScale, heightScale));
        InterfaceScale = screenScale * InterfaceScaleMultiplier;

        Width = DesignWidth * InterfaceScale;
        Height = DesignHeight * InterfaceScale;
    }

    public void PositionAtTopCenter()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Top;
    }

    public void PrepareOpenAnimation()
    {
        _animationVersion++;
        IsClosingAnimation = false;
        SetSurfaceState(0, 0.38, 0.35);
    }

    public void PlayOpenAnimation()
    {
        var startOpacity = Opacity;
        var startScaleX = OpenScaleTransform.ScaleX;
        var startScaleY = OpenScaleTransform.ScaleY;
        SetSurfaceState(startOpacity, startScaleX, startScaleY);

        IsClosingAnimation = false;
        _closeCanBeCancelled = false;
        var animationVersion = ++_animationVersion;
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var opacityAnimation = CreateAnimation(1, 360, easing);
        var scaleXAnimation = CreateAnimation(1, 420, easing);
        var scaleYAnimation = CreateAnimation(1, 420, easing);
        scaleYAnimation.Completed += (_, _) =>
        {
            if (animationVersion != _animationVersion) return;
            SetSurfaceState(1, 1, 1);
        };

        BeginAnimation(OpacityProperty, opacityAnimation);
        OpenScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleXAnimation);
        OpenScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleYAnimation);
    }

    public void HideAnimated(bool cancelWhenPointerReturns = false)
    {
        if (!IsVisible || IsClosingAnimation) return;

        var startOpacity = Opacity;
        var startScaleX = OpenScaleTransform.ScaleX;
        var startScaleY = OpenScaleTransform.ScaleY;
        SetSurfaceState(startOpacity, startScaleX, startScaleY);

        IsClosingAnimation = true;
        _closeCanBeCancelled = cancelWhenPointerReturns;
        var animationVersion = ++_animationVersion;
        var easing = new CubicEase { EasingMode = EasingMode.EaseIn };
        var opacityAnimation = CreateAnimation(0, 300, easing);
        var scaleXAnimation = CreateAnimation(0.38, 350, easing);
        var scaleYAnimation = CreateAnimation(0.35, 350, easing);
        scaleYAnimation.Completed += (_, _) =>
        {
            if (animationVersion != _animationVersion) return;
            IsClosingAnimation = false;
            _closeCanBeCancelled = false;
            Hide();
            SetSurfaceState(1, 1, 1);
        };

        BeginAnimation(OpacityProperty, opacityAnimation);
        OpenScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleXAnimation);
        OpenScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleYAnimation);
    }

    public void CancelCloseAnimation(bool force = false)
    {
        if (IsClosingAnimation && (_closeCanBeCancelled || force)) PlayOpenAnimation();
    }

    private static DoubleAnimation CreateAnimation(
        double targetValue,
        double durationMilliseconds,
        IEasingFunction easing) => new(targetValue, System.TimeSpan.FromMilliseconds(durationMilliseconds))
    {
        EasingFunction = easing,
        FillBehavior = FillBehavior.HoldEnd
    };

    private void SetSurfaceState(double opacity, double scaleX, double scaleY)
    {
        BeginAnimation(OpacityProperty, null);
        OpenScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, null);
        OpenScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, null);

        Opacity = opacity;
        OpenScaleTransform.ScaleX = scaleX;
        OpenScaleTransform.ScaleY = scaleY;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        HideAnimated();
        e.Handled = true;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (AllowClose) return;
        e.Cancel = true;
        HideAnimated();
    }

    private static void NavigationScrollOffsetChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is not MainWindow window) return;
        window.NavigationScrollViewer?.ScrollToVerticalOffset((double)eventArgs.NewValue);
    }

    private void NavigationScrollUp_Click(object sender, RoutedEventArgs e) =>
        ScrollNavigation(-1);

    private void NavigationScrollDown_Click(object sender, RoutedEventArgs e) =>
        ScrollNavigation(1);

    private void NavigationScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta == 0) return;

        var direction = e.Delta < 0 ? 1 : -1;
        if (direction < 0 && !NavigationUpButton.IsEnabled) return;
        if (direction > 0 && !NavigationDownButton.IsEnabled) return;

        ScrollNavigation(direction);
        e.Handled = true;
    }

    private void NavigationScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e) =>
        UpdateNavigationScrollState();

    private void ScrollNavigation(int direction)
    {
        var currentOffset = NavigationScrollViewer.VerticalOffset;
        var maximumOffset = NavigationScrollViewer.ScrollableHeight;
        if (maximumOffset <= 0) return;

        var startingTarget = _isNavigationScrollAnimating
            ? _navigationTargetOffset
            : currentOffset;
        var targetOffset = System.Math.Clamp(
            startingTarget + direction * NavigationScrollStep,
            0,
            maximumOffset);
        if (direction > 0 && maximumOffset - targetOffset < NavigationScrollStep / 2)
        {
            targetOffset = maximumOffset;
        }
        else if (direction < 0 && targetOffset < NavigationScrollStep / 2)
        {
            targetOffset = 0;
        }

        if (System.Math.Abs(targetOffset - _navigationTargetOffset) < 0.1
            && _isNavigationScrollAnimating)
        {
            return;
        }

        var animationVersion = ++_navigationScrollAnimationVersion;
        _navigationTargetOffset = targetOffset;
        _isNavigationScrollAnimating = true;

        // Capture the actual visual position before replacing an unfinished animation.
        currentOffset = NavigationScrollViewer.VerticalOffset;
        BeginAnimation(NavigationScrollOffsetProperty, null);
        NavigationScrollOffset = currentOffset;

        var animation = new DoubleAnimation(
            currentOffset,
            targetOffset,
            System.TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd
        };
        animation.Completed += (_, _) =>
        {
            if (animationVersion != _navigationScrollAnimationVersion) return;

            var finalOffset = System.Math.Clamp(
                targetOffset,
                0,
                NavigationScrollViewer.ScrollableHeight);
            BeginAnimation(NavigationScrollOffsetProperty, null);
            NavigationScrollOffset = finalOffset;
            _navigationTargetOffset = finalOffset;
            _isNavigationScrollAnimating = false;
        };

        BeginAnimation(
            NavigationScrollOffsetProperty,
            animation,
            HandoffBehavior.SnapshotAndReplace);
    }

    private void UpdateNavigationScrollState()
    {
        var canScrollUp = NavigationScrollViewer.VerticalOffset > 0.5;
        var canScrollDown = NavigationScrollViewer.VerticalOffset
                            < NavigationScrollViewer.ScrollableHeight - 0.5;

        NavigationUpButton.IsEnabled = canScrollUp;
        NavigationDownButton.IsEnabled = canScrollDown;

        if (_navigationFadeTop == canScrollUp
            && _navigationFadeBottom == canScrollDown)
        {
            return;
        }

        _navigationFadeTop = canScrollUp;
        _navigationFadeBottom = canScrollDown;

        var transparent = Color.FromArgb(0, 0, 0, 0);
        var mask = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops = new GradientStopCollection
            {
                new(canScrollUp ? transparent : Colors.Black, 0),
                new(Colors.Black, 0.055),
                new(Colors.Black, 0.945),
                new(canScrollDown ? transparent : Colors.Black, 1)
            }
        };
        mask.Freeze();
        NavigationViewport.OpacityMask = mask;
    }

    private void NavigationButton_DragEnter(object sender, DragEventArgs e)
    {
        if (!TryGetFolderDrag(sender, e, out var item)) return;
        BeginFolderHover(item);
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void NavigationButton_DragOver(object sender, DragEventArgs e)
    {
        if (!TryGetFolderDrag(sender, e, out var item))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        if (!ReferenceEquals(_folderHoverItem, item)) BeginFolderHover(item);
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void NavigationButton_DragLeave(object sender, DragEventArgs e) => CancelFolderHover();

    private void NavigationButton_Drop(object sender, DragEventArgs e)
    {
        CancelFolderHover();
        if (!TryGetFolderDrag(sender, e, out var item)) return;
        if (DataContext is not MainWindowViewModel mainViewModel) return;

        mainViewModel.NavigateCommand.Execute(item);
        if (mainViewModel.NavigationService.CurrentView is FolderViewModel folderViewModel
            && e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            folderViewModel.AddFiles(paths.Where(File.Exists));
        }

        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void BeginFolderHover(NavigationItemViewModel item)
    {
        if (item.IsSelected) return;
        _folderHoverItem = item;
        _folderHoverTimer.Stop();
        _folderHoverTimer.Start();
    }

    private void CancelFolderHover()
    {
        _folderHoverTimer.Stop();
        _folderHoverItem = null;
    }

    private void FolderHoverTimer_Tick(object? sender, System.EventArgs e)
    {
        _folderHoverTimer.Stop();
        if (_folderHoverItem is null || DataContext is not MainWindowViewModel mainViewModel) return;

        var item = _folderHoverItem;
        _folderHoverItem = null;
        mainViewModel.NavigateCommand.Execute(item);
    }

    private static bool TryGetFolderDrag(object sender, DragEventArgs e, out NavigationItemViewModel item)
    {
        item = null!;
        if (sender is not Button { DataContext: NavigationItemViewModel navigationItem }) return false;
        if (navigationItem.TargetViewModelType != typeof(FolderViewModel)) return false;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return false;

        var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (paths is null || !paths.Any(File.Exists)) return false;

        item = navigationItem;
        return true;
    }
}
