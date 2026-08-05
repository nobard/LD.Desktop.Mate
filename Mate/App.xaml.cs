using System.Windows;
using Autofac;
using Mate.MVVM.Views;
using Mate.Services.DI;
using Mate.Services.Interfaces;

namespace Mate;

public partial class App : Application
{
    private IContainer? _container;
    private ITrayService? _trayService;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _container = AutofacConfig.GetConfiguredContainer();
        _mainWindow = _container.Resolve<MainWindow>();
        MainWindow = _mainWindow;

        _trayService = _container.Resolve<ITrayService>();
        _trayService.Initialize(
            () => Dispatcher.Invoke(TogglePanel),
            () => Dispatcher.Invoke(ExitApplication));
    }

    private void TogglePanel()
    {
        if (_mainWindow is null) return;
        if (_mainWindow.IsVisible)
        {
            _mainWindow.Hide();
            return;
        }

        _mainWindow.PositionAtTopCenter();
        _mainWindow.Show();
        _mainWindow.Activate();
    }

    private void ExitApplication()
    {
        if (_mainWindow is not null)
        {
            _mainWindow.AllowClose = true;
            _mainWindow.Close();
        }

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayService?.Dispose();
        _container?.Dispose();
        base.OnExit(e);
    }
}
