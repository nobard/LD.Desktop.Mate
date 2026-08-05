using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Mate.MVVM.Core;

namespace Mate.MVVM.ViewModels;

public sealed class FolderViewModel : ToolViewModel
{
    public FolderViewModel()
    {
        Files = new ObservableCollection<FileCardViewModel>
        {
            new("Снимок", "2026-08-05 12:42", true),
            new("Снимок", "2026-08-05 12:37", true)
        };

        foreach (var file in Files)
        {
            file.PropertyChanged += File_PropertyChanged;
        }
        Files.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SelectionSummary));

        ToggleSelectionCommand = new DelegateCommand(ToggleSelection);
        ClearSelectionCommand = new DelegateCommand(_ =>
        {
            foreach (var file in Files) file.IsSelected = false;
        });
        ClearFilesCommand = new DelegateCommand(_ => Files.Clear());
    }

    public override string Title => "Папка";

    public override string Description => "Быстрый доступ к файлам и скриншотам.";

    public ObservableCollection<FileCardViewModel> Files { get; }

    public DelegateCommand ToggleSelectionCommand { get; }

    public DelegateCommand ClearSelectionCommand { get; }

    public DelegateCommand ClearFilesCommand { get; }

    public string SelectionSummary => $"Выбрано: {Files.Count(file => file.IsSelected)}";

    private void ToggleSelection(object? parameter)
    {
        if (parameter is FileCardViewModel file) file.IsSelected = !file.IsSelected;
    }

    private void File_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FileCardViewModel.IsSelected)) OnPropertyChanged(nameof(SelectionSummary));
    }
}

public sealed class FileCardViewModel : ObservableObject
{
    private bool _isSelected;

    public FileCardViewModel(string name, string capturedAt, bool isSelected)
    {
        Name = name;
        CapturedAt = capturedAt;
        _isSelected = isSelected;
    }

    public string Name { get; }

    public string CapturedAt { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
