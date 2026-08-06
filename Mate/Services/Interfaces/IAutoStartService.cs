namespace Mate.Services.Interfaces;

public interface IAutoStartService
{
    bool IsEnabled { get; }

    bool SetEnabled(bool enabled);
}
