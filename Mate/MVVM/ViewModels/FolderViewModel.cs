using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Mate.MVVM.Core;
using Mate.Services.Interfaces;

namespace Mate.MVVM.ViewModels;

public sealed class FolderViewModel : ToolViewModel, IDisposable
{
    private readonly IFileShelfService _fileShelfService;
    private string _statusMessage = "Перетащите файлы в это окно";

    public FolderViewModel(IFileShelfService fileShelfService)
    {
        _fileShelfService = fileShelfService;
        Files = new ObservableCollection<FileCardViewModel>();

        ToggleSelectionCommand = new DelegateCommand(ToggleSelection);
        ClearSelectionCommand = new DelegateCommand(_ => ClearSelection(), _ => HasSelection);
        DeleteSelectedCommand = new DelegateCommand(_ => DeleteSelected(), _ => HasSelection);
        ClearFilesCommand = new DelegateCommand(_ => ClearFiles(), _ => !IsEmpty);

        _fileShelfService.FilesChanged += FileShelfService_FilesChanged;
        ReloadFiles();
    }

    public override string Title => "Папка";

    public override string Description => "Быстрый доступ к файлам и скриншотам.";

    public ObservableCollection<FileCardViewModel> Files { get; }

    public DelegateCommand ToggleSelectionCommand { get; }

    public DelegateCommand ClearSelectionCommand { get; }

    public DelegateCommand DeleteSelectedCommand { get; }

    public DelegateCommand ClearFilesCommand { get; }

    public string SelectionSummary => HasSelection
        ? $"Выбрано: {Files.Count(file => file.IsSelected)}"
        : $"Файлов: {Files.Count}";

    public bool IsEmpty => Files.Count == 0;

    public bool HasSelection => Files.Any(file => file.IsSelected);

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public void AddFiles(IEnumerable<string> paths)
    {
        try
        {
            _fileShelfService.AddFiles(paths);
            StatusMessage = "Файлы добавлены";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusMessage = "Не удалось добавить один из файлов";
        }
    }

    public void OpenFile(FileCardViewModel file) => OpenPath(file.FilePath);

    private static void OpenPath(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return;

        Process.Start(new ProcessStartInfo(path)
        {
            UseShellExecute = true
        });
    }

    private void ReloadFiles()
    {
        foreach (var file in Files)
        {
            file.PropertyChanged -= File_PropertyChanged;
        }

        Files.Clear();
        foreach (var path in _fileShelfService.GetFiles())
        {
            var file = new FileCardViewModel(path);
            file.PropertyChanged += File_PropertyChanged;
            Files.Add(file);
        }

        StatusMessage = IsEmpty ? "Перетащите файлы в это окно" : "Можно перетащить файлы сюда или из окна наружу";
        NotifyCollectionStateChanged();
    }

    private void ToggleSelection(object? parameter)
    {
        if (parameter is FileCardViewModel file) file.IsSelected = !file.IsSelected;
    }

    private void ClearSelection()
    {
        foreach (var file in Files) file.IsSelected = false;
    }

    private void DeleteSelected()
    {
        var selectedPaths = Files
            .Where(file => file.IsSelected)
            .Select(file => file.FilePath)
            .ToArray();

        try
        {
            _fileShelfService.DeleteFiles(selectedPaths);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusMessage = "Не удалось удалить один из файлов";
        }
    }

    private void ClearFiles()
    {
        var paths = Files.Select(file => file.FilePath).ToArray();
        try
        {
            _fileShelfService.DeleteFiles(paths);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusMessage = "Не удалось очистить хранилище";
        }
    }

    private void File_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FileCardViewModel.IsSelected)) NotifyCollectionStateChanged();
    }

    private void FileShelfService_FilesChanged()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            ReloadFiles();
            return;
        }

        dispatcher.Invoke(ReloadFiles);
    }

    private void NotifyCollectionStateChanged()
    {
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasSelection));
        ClearSelectionCommand.RaiseCanExecuteChanged();
        DeleteSelectedCommand.RaiseCanExecuteChanged();
        ClearFilesCommand.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _fileShelfService.FilesChanged -= FileShelfService_FilesChanged;
        foreach (var file in Files)
        {
            file.PropertyChanged -= File_PropertyChanged;
        }
    }
}

public sealed class FileCardViewModel : ObservableObject
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tif", ".tiff"
    };

    private bool _isSelected;

    public FileCardViewModel(string filePath)
    {
        FilePath = filePath;
        Name = Path.GetFileName(filePath);
        CapturedAt = File.GetLastWriteTime(filePath).ToString("dd.MM.yyyy  HH:mm");
        Extension = GetExtensionLabel(filePath);
        Thumbnail = TryLoadThumbnail(filePath);
    }

    public string FilePath { get; }

    public string Name { get; }

    public string CapturedAt { get; }

    public string Extension { get; }

    public ImageSource? Thumbnail { get; }

    public bool HasThumbnail => Thumbnail is not null;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private static string GetExtensionLabel(string path)
    {
        var extension = Path.GetExtension(path).TrimStart('.');
        return string.IsNullOrWhiteSpace(extension) ? "ФАЙЛ" : extension.ToUpperInvariant();
    }

    private static ImageSource? TryLoadThumbnail(string path)
    {
        if (!ImageExtensions.Contains(Path.GetExtension(path))) return null;

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 240;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}
