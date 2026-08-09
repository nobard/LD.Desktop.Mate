using System;

namespace Mate.Services.Interfaces;

public interface ITrayService : IDisposable
{
    void Initialize(Action togglePanel, Action checkForUpdates, Action exitApplication);

    void SetUpdateCheckInProgress(bool isInProgress);

    void ShowUpdateAvailable(string version, Action installUpdate);

    void SetUpdateInstallationInProgress();

    void ShowUpdateCheckMessage(string message);
}
