using System;

namespace Mate.Services.Interfaces;

public interface IAutoStartService
{
    bool IsEnabled { get; }

    event EventHandler? EnabledChanged;

    bool SetEnabled(bool enabled);
}
