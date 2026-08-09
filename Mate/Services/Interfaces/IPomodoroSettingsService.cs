using Mate.Models;

namespace Mate.Services.Interfaces;

public interface IPomodoroSettingsService
{
    PomodoroSettings Load();

    void Save(PomodoroSettings settings);
}
