namespace Mate.MVVM.ViewModels;

public sealed class MusicViewModel : ToolViewModel
{
    public override string Title => "Музыка";

    public override string Description => "Текущий трек и управление воспроизведением.";

    public string TrackTitle => "Мотылёк";

    public string Artist => "M'Dee — Городской FM";

    public string ElapsedTime => "0:12";

    public string Source => "Google Chrome";
}
