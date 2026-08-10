using System;
using Mate.MVVM.Core;
using Mate.Services.Interfaces;

namespace Mate.MVVM.ViewModels;

public sealed class TrayMenuViewModel : ObservableObject, IDisposable
{
    private readonly IHoverActivationService _hoverActivationService;
    private readonly Action _togglePanel;
    private readonly Action _openSettings;
    private readonly Action _exitApplication;
    private bool _isHoverActivationDisabled;

    public TrayMenuViewModel(
        IHoverActivationService hoverActivationService,
        Action togglePanel,
        Action openSettings,
        Action exitApplication)
    {
        _hoverActivationService = hoverActivationService;
        _togglePanel = togglePanel;
        _openSettings = openSettings;
        _exitApplication = exitApplication;
        _isHoverActivationDisabled = !_hoverActivationService.IsEnabled;

        OpenMateCommand = new DelegateCommand(_ => ExecuteAndClose(_togglePanel));
        OpenSettingsCommand = new DelegateCommand(_ => ExecuteAndClose(_openSettings));
        ToggleHoverActivationCommand = new DelegateCommand(_ => ToggleHoverActivation());
        ExitCommand = new DelegateCommand(_ => ExecuteAndClose(_exitApplication));

        _hoverActivationService.EnabledChanged += HoverActivationService_EnabledChanged;
    }

    public event EventHandler? CloseRequested;

    public DelegateCommand OpenMateCommand { get; }

    public DelegateCommand OpenSettingsCommand { get; }

    public DelegateCommand ToggleHoverActivationCommand { get; }

    public DelegateCommand ExitCommand { get; }

    public bool IsHoverActivationDisabled
    {
        get => _isHoverActivationDisabled;
        private set => SetProperty(ref _isHoverActivationDisabled, value);
    }

    public void Refresh() => IsHoverActivationDisabled = !_hoverActivationService.IsEnabled;

    private void ToggleHoverActivation()
    {
        _hoverActivationService.SetEnabled(!_hoverActivationService.IsEnabled);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ExecuteAndClose(Action action)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
        action();
    }

    private void HoverActivationService_EnabledChanged(object? sender, EventArgs e) => Refresh();

    public void Dispose() =>
        _hoverActivationService.EnabledChanged -= HoverActivationService_EnabledChanged;
}
