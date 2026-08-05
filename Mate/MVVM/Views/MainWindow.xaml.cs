using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Mate.MVVM.ViewModels;

namespace Mate.MVVM.Views;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _folderHoverTimer;
    private NavigationItemViewModel? _folderHoverItem;
    private int _animationVersion;
    private bool _closeCanBeCancelled;

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

    public void PositionAtTopCenter()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Top + 24;
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
