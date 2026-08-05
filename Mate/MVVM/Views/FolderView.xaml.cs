using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Mate.MVVM.ViewModels;

namespace Mate.MVVM.Views;

public partial class FolderView : UserControl
{
    private Point _dragStart;
    private FileCardViewModel? _draggedFile;

    public FolderView() => InitializeComponent();

    private void FileCardMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _draggedFile = (sender as FrameworkElement)?.DataContext as FileCardViewModel;
    }

    private void FileCardMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedFile is null) return;

        var currentPosition = e.GetPosition(this);
        if (Math.Abs(currentPosition.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(currentPosition.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var file = _draggedFile;
        _draggedFile = null;

        var draggedFiles = file.IsSelected && DataContext is FolderViewModel viewModel
            ? viewModel.Files.Where(item => item.IsSelected)
            : new[] { file }.AsEnumerable();
        var paths = draggedFiles
            .Select(item => item.FilePath)
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (paths.Length == 0) return;

        var data = new DataObject(DataFormats.FileDrop, paths);
        DragDrop.DoDragDrop(this, data, DragDropEffects.Copy);
        e.Handled = true;
    }

    private void FileCardMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not FileCardViewModel file) return;
        if (DataContext is FolderViewModel viewModel) viewModel.OpenFile(file);
        e.Handled = true;
    }

    private void FilesDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
        var hasFiles = paths is not null && Array.Exists(paths, File.Exists);
        e.Effects = hasFiles ? DragDropEffects.Copy : DragDropEffects.None;
        DropSurface.BorderBrush = hasFiles ? new SolidColorBrush(Color.FromRgb(105, 105, 105)) : Brushes.Transparent;
        DropSurface.Background = hasFiles ? new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)) : Brushes.Transparent;
        e.Handled = true;
    }

    private void FilesDragLeave(object sender, DragEventArgs e) => ResetDropSurface();

    private void FilesDrop(object sender, DragEventArgs e)
    {
        ResetDropSurface();
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        if (DataContext is FolderViewModel viewModel) viewModel.AddFiles(paths);
        e.Handled = true;
    }

    private void ResetDropSurface()
    {
        DropSurface.BorderBrush = Brushes.Transparent;
        DropSurface.Background = Brushes.Transparent;
    }
}
