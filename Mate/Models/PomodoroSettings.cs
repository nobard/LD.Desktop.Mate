namespace Mate.Models;

public sealed record PomodoroSettings(
    int FocusMinutes,
    int ShortBreakMinutes,
    int LongBreakMinutes,
    int SessionsBeforeLongBreak)
{
    public static PomodoroSettings Default { get; } = new(25, 5, 15, 4);
}
