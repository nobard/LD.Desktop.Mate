using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Mate.MVVM.ViewModels;

namespace Mate.MVVM.Views;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _folderHoverTimer;
    private NavigationItemViewModel? _folderHoverItem;

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

    public void PositionAtTopCenter()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Top + 24;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        Hide();
        e.Handled = true;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (AllowClose) return;
        e.Cancel = true;
        Hide();
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
