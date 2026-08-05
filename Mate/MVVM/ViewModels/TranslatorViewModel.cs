using Mate.MVVM.Core;

namespace Mate.MVVM.ViewModels;

public sealed class TranslatorViewModel : ToolViewModel
{
    private string _sourceText = "Привет";
    private string _translatedText = "Hello";

    public TranslatorViewModel()
    {
        ClearCommand = new DelegateCommand(_ => SourceText = string.Empty);
    }

    public override string Title => "Перевод";

    public override string Description => "Быстрый перевод введённого текста.";

    public string SourceLanguage => "РУССКИЙ";

    public string TargetLanguage => "АНГЛИЙСКИЙ";

    public string SourceText
    {
        get => _sourceText;
        set
        {
            if (!SetProperty(ref _sourceText, value)) return;
            TranslatedText = string.IsNullOrWhiteSpace(value) ? string.Empty : "Hello";
        }
    }

    public string TranslatedText
    {
        get => _translatedText;
        private set => SetProperty(ref _translatedText, value);
    }

    public DelegateCommand ClearCommand { get; }
}
