using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Mate.Services.Interfaces;

namespace Mate.Services.Implementations;

public sealed class ThemeService : IThemeService
{
    private const string ThemeDictionaryMarker = "Themes/";
    private readonly string _settingsPath;

    public ThemeService()
    {
        AvailableThemes = new[]
        {
            new AppThemeOption(AppTheme.Dark, "Тёмная"),
            new AppThemeOption(AppTheme.AlmostBlack, "Чёрная")
        };

        var dataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LD.Desktop.Mate");
        Directory.CreateDirectory(dataFolder);
        _settingsPath = Path.Combine(dataFolder, "theme.txt");

        CurrentTheme = LoadTheme();
        ApplyTheme(CurrentTheme);
    }

    public IReadOnlyList<AppThemeOption> AvailableThemes { get; }

    public AppTheme CurrentTheme { get; private set; }

    public event EventHandler? ThemeChanged;

    public void SetTheme(AppTheme theme)
    {
        if (!AvailableThemes.Any(option => option.Theme == theme)) return;

        ApplyTheme(theme);
        if (CurrentTheme == theme) return;

        CurrentTheme = theme;
        SaveTheme(theme);
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void ApplyTheme(AppTheme theme)
    {
        var application = Application.Current;
        if (application is null) return;

        void Apply()
        {
            var dictionaries = application.Resources.MergedDictionaries;
            var currentThemeDictionaries = dictionaries
                .Where(dictionary => dictionary.Source?.OriginalString.Contains(
                    ThemeDictionaryMarker,
                    StringComparison.OrdinalIgnoreCase) == true)
                .ToArray();
            foreach (var dictionary in currentThemeDictionaries) dictionaries.Remove(dictionary);

            var fileName = theme == AppTheme.AlmostBlack
                ? "AlmostBlackTheme.xaml"
                : "DarkTheme.xaml";
            dictionaries.Insert(0, new ResourceDictionary
            {
                Source = new Uri($"/Mate;component/Themes/{fileName}", UriKind.Relative)
            });
        }

        if (application.Dispatcher.CheckAccess()) Apply();
        else application.Dispatcher.Invoke(Apply);
    }

    private AppTheme LoadTheme()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return AppTheme.Dark;
            return Enum.TryParse<AppTheme>(File.ReadAllText(_settingsPath), out var theme)
                ? theme
                : AppTheme.Dark;
        }
        catch
        {
            return AppTheme.Dark;
        }
    }

    private void SaveTheme(AppTheme theme)
    {
        try
        {
            File.WriteAllText(_settingsPath, theme.ToString());
        }
        catch
        {
            // The selected theme still applies for the current session.
        }
    }
}
