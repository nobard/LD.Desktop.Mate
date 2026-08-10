using System;
using System.Collections.Generic;

namespace Mate.Services.Interfaces;

public enum AppFeature
{
    Player,
    Folder,
    Clipboard,
    Snippets,
    Browser,
    Translator,
    Notifications,
    Pomodoro
}

public sealed record FeatureLayoutItem(AppFeature Feature, bool IsVisible);

public interface IFeatureLayoutService
{
    IReadOnlyList<FeatureLayoutItem> Items { get; }

    event EventHandler? LayoutChanged;

    bool SetVisible(AppFeature feature, bool isVisible);

    bool Move(AppFeature feature, int targetIndex);
}
