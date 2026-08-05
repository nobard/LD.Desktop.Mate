using System;
using System.Collections.Generic;

namespace Mate.Services.Interfaces;

public enum AppTheme
{
    Dark,
    AlmostBlack
}

public sealed record AppThemeOption(AppTheme Theme, string DisplayName);

public interface IThemeService
{
    IReadOnlyList<AppThemeOption> AvailableThemes { get; }

    AppTheme CurrentTheme { get; }

    event EventHandler? ThemeChanged;

    void SetTheme(AppTheme theme);
}
