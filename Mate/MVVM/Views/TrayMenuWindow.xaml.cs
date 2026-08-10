using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Mate.MVVM.ViewModels;
using Forms = System.Windows.Forms;

namespace Mate.MVVM.Views;

public partial class TrayMenuWindow : Window
{
    private readonly TrayMenuViewModel _viewModel;

    public TrayMenuWindow(TrayMenuViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.CloseRequested += ViewModel_CloseRequested;
        Closed += Window_Closed;
    }

    public void ShowMenu()
    {
        _viewModel.Refresh();
        Opacity = 0;
        if (!IsVisible) Show();
        UpdateLayout();

        var cursor = Forms.Cursor.Position;
        var screen = Forms.Screen.FromPoint(cursor);
        var dpi = VisualTreeHelper.GetDpi(this);
        var cursorX = cursor.X / dpi.DpiScaleX;
        var cursorY = cursor.Y / dpi.DpiScaleY;
        var workArea = new Rect(
            screen.WorkingArea.Left / dpi.DpiScaleX,
            screen.WorkingArea.Top / dpi.DpiScaleY,
            screen.WorkingArea.Width / dpi.DpiScaleX,
            screen.WorkingArea.Height / dpi.DpiScaleY);

        Left = Math.Clamp(cursorX - ActualWidth + 12, workArea.Left, workArea.Right - ActualWidth);
        Top = Math.Clamp(cursorY - ActualHeight - 8, workArea.Top, workArea.Bottom - ActualHeight);
        Opacity = 1;
        Activate();
    }

    private void ViewModel_CloseRequested(object? sender, EventArgs e) => Hide();

    private void Window_Deactivated(object? sender, EventArgs e) => Hide();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        Hide();
        e.Handled = true;
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _viewModel.CloseRequested -= ViewModel_CloseRequested;
        Closed -= Window_Closed;
    }
}
