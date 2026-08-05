namespace Mate.MVVM.ViewModels;

public sealed class ClipboardViewModel : ToolViewModel
{
    public override string Title => "Буфер обмена";

    public override string Description => "Текущее содержимое системного буфера.";
}
