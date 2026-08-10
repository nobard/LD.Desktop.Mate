using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Mate.MVVM.ViewModels;
using Mate.Services.Interfaces;
using Microsoft.Win32;

namespace Mate.MVVM.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private Point _featureDragStart;
    private AppFeature? _pendingDraggedFeature;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.CloseRequested += ViewModel_CloseRequested;
        _viewModel.ChooseStorageFolderRequested += ViewModel_ChooseStorageFolderRequested;
    }

    public bool AllowClose { get; set; }

    public void ShowSettings()
    {
        _viewModel.Refresh();
        if (!IsVisible) Show();
        Activate();
    }

    private void ViewModel_CloseRequested(object? sender, System.EventArgs e) => Hide();

    private void FeatureDragHandle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: FeatureOptionViewModel option } handle) return;

        _pendingDraggedFeature = option.Feature;
        _featureDragStart = e.GetPosition(this);
        handle.CaptureMouse();
        e.Handled = true;
    }

    private void FeatureDragHandle_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_pendingDraggedFeature is null || sender is not FrameworkElement handle) return;

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            handle.ReleaseMouseCapture();
            _pendingDraggedFeature = null;
            return;
        }

        var currentPosition = e.GetPosition(this);
        if (System.Math.Abs(currentPosition.X - _featureDragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && System.Math.Abs(currentPosition.Y - _featureDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var draggedFeature = _pendingDraggedFeature.Value;
        _pendingDraggedFeature = null;
        handle.ReleaseMouseCapture();

        var data = new DataObject(typeof(AppFeature), draggedFeature);
        DragDrop.DoDragDrop(handle, data, DragDropEffects.Move);
        e.Handled = true;
    }

    private void FeatureDragHandle_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement handle && handle.IsMouseCaptured)
        {
            handle.ReleaseMouseCapture();
        }

        _pendingDraggedFeature = null;
        e.Handled = true;
    }

    private void FeaturesList_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(AppFeature))
            || e.Data.GetData(typeof(AppFeature)) is not AppFeature draggedFeature)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var source = e.OriginalSource as DependencyObject;
        var container = source is null
            ? null
            : ItemsControl.ContainerFromElement(FeaturesList, source) as FrameworkElement;
        if (container?.DataContext is FeatureOptionViewModel target)
        {
            _viewModel.MoveFeature(draggedFeature, target.Feature);
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void FeaturesList_Drop(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(AppFeature))
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void ViewModel_ChooseStorageFolderRequested(object? sender, System.EventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Выберите папку для файлов Mate",
            InitialDirectory = _viewModel.StorageFolder,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.SetStorageFolder(dialog.FolderName);
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        Hide();
        e.Handled = true;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (AllowClose)
        {
            _viewModel.CloseRequested -= ViewModel_CloseRequested;
            _viewModel.ChooseStorageFolderRequested -= ViewModel_ChooseStorageFolderRequested;
            return;
        }

        e.Cancel = true;
        Hide();
    }
}
