using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace Mate.MVVM.Views;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

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
}
