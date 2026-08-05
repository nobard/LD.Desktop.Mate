using System;

namespace Mate.Services.Interfaces;

public interface ITrayService : IDisposable
{
    void Initialize(Action togglePanel, Action exitApplication);
}
