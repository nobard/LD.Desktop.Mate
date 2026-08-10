using System;

namespace Mate.Services.Interfaces;

public interface IHoverActivationService
{
    bool IsEnabled { get; }

    event EventHandler? EnabledChanged;

    void SetEnabled(bool isEnabled);
}
