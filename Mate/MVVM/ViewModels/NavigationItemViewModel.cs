using System;
using Mate.MVVM.Core;

namespace Mate.MVVM.ViewModels;

public sealed class NavigationItemViewModel : ObservableObject
{
    private bool _isSelected;
    private bool _usePrivateBrowserIcon;

    public NavigationItemViewModel(string icon, string toolTip, Type targetViewModelType)
    {
        Icon = icon;
        ToolTip = toolTip;
        TargetViewModelType = targetViewModelType;
    }

    public string Icon { get; }

    public string ToolTip { get; }

    public Type TargetViewModelType { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool UsePrivateBrowserIcon
    {
        get => _usePrivateBrowserIcon;
        set => SetProperty(ref _usePrivateBrowserIcon, value);
    }
}
