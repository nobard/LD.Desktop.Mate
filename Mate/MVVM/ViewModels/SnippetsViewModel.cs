using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using Mate.Models;
using Mate.MVVM.Core;
using Mate.Services.Interfaces;

namespace Mate.MVVM.ViewModels;

public sealed class SnippetsViewModel : ToolViewModel
{
    private readonly ISnippetStorageService _storageService;
    private string _searchQuery = string.Empty;
    private bool _isEditorOpen;
    private SnippetType _selectedType = SnippetType.Text;
    private string _newComment = string.Empty;
    private string _newValue = string.Empty;
    private string _formError = string.Empty;
    private SnippetItemViewModel? _editingItem;

    public SnippetsViewModel(ISnippetStorageService storageService)
    {
        _storageService = storageService;
        Items = new ObservableCollection<SnippetItemViewModel>();
        foreach (var item in _storageService.GetItems())
        {
            Items.Add(new SnippetItemViewModel(item));
        }

        FilteredItems = CollectionViewSource.GetDefaultView(Items);
        FilteredItems.Filter = FilterItems;

        CopyItemCommand = new DelegateCommand(CopyItem);
        EditItemCommand = new DelegateCommand(EditItem);
        DeleteItemCommand = new DelegateCommand(DeleteItem);
        OpenEditorCommand = new DelegateCommand(_ => OpenEditor());
        ClearSearchCommand = new DelegateCommand(_ => SearchQuery = string.Empty, _ => HasSearchQuery);
        CancelEditorCommand = new DelegateCommand(_ => CloseEditor());
        AddItemCommand = new DelegateCommand(_ => AddItem(), _ => !string.IsNullOrWhiteSpace(NewValue));
        SelectTypeCommand = new DelegateCommand(SelectType);
    }

    public override string Title => "Заготовки";

    public override string Description => "Часто используемые тексты и ссылки.";

    public ObservableCollection<SnippetItemViewModel> Items { get; }

    public ICollectionView FilteredItems { get; }

    public DelegateCommand CopyItemCommand { get; }

    public DelegateCommand EditItemCommand { get; }

    public DelegateCommand DeleteItemCommand { get; }

    public DelegateCommand OpenEditorCommand { get; }

    public DelegateCommand ClearSearchCommand { get; }

    public DelegateCommand CancelEditorCommand { get; }

    public DelegateCommand AddItemCommand { get; }

    public DelegateCommand SelectTypeCommand { get; }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (!SetProperty(ref _searchQuery, value)) return;
            FilteredItems.Refresh();
            ClearSearchCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(HasSearchQuery));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(EmptyMessage));
        }
    }

    public bool IsEditorOpen
    {
        get => _isEditorOpen;
        private set => SetProperty(ref _isEditorOpen, value);
    }

    public SnippetType SelectedType
    {
        get => _selectedType;
        private set
        {
            if (!SetProperty(ref _selectedType, value)) return;
            OnPropertyChanged(nameof(IsTextTypeSelected));
            OnPropertyChanged(nameof(IsLinkTypeSelected));
            OnPropertyChanged(nameof(IsEmailTypeSelected));
            OnPropertyChanged(nameof(IsPhoneTypeSelected));
        }
    }

    public bool IsTextTypeSelected => SelectedType == SnippetType.Text;

    public bool IsLinkTypeSelected => SelectedType == SnippetType.Link;

    public bool IsEmailTypeSelected => SelectedType is SnippetType.Email or SnippetType.User;

    public bool IsPhoneTypeSelected => SelectedType == SnippetType.Phone;

    public string NewComment
    {
        get => _newComment;
        set => SetProperty(ref _newComment, value);
    }

    public string NewValue
    {
        get => _newValue;
        set
        {
            if (!SetProperty(ref _newValue, value)) return;
            AddItemCommand.RaiseCanExecuteChanged();
            FormError = string.Empty;
        }
    }

    public string FormError
    {
        get => _formError;
        private set => SetProperty(ref _formError, value);
    }

    public bool IsEmpty => FilteredItems.IsEmpty;

    public bool HasSearchQuery => !string.IsNullOrEmpty(SearchQuery);

    public string EmptyMessage => string.IsNullOrWhiteSpace(SearchQuery)
        ? "Добавьте первую заготовку"
        : "Ничего не найдено";

    private void OpenEditor()
    {
        SetEditingItem(null);
        SelectedType = SnippetType.Text;
        NewComment = string.Empty;
        NewValue = string.Empty;
        FormError = string.Empty;
        IsEditorOpen = true;
    }

    private void CloseEditor()
    {
        IsEditorOpen = false;
        FormError = string.Empty;
        SetEditingItem(null);
    }

    private void AddItem()
    {
        try
        {
            if (_editingItem is null)
            {
                var item = _storageService.Add(SelectedType, NewComment, NewValue);
                Items.Insert(0, new SnippetItemViewModel(item));
            }
            else
            {
                var itemIndex = Items.IndexOf(_editingItem);
                var updatedItem = _storageService.Update(
                    _editingItem.Id,
                    SelectedType,
                    NewComment,
                    NewValue);
                if (itemIndex >= 0)
                {
                    Items[itemIndex] = new SnippetItemViewModel(updatedItem);
                }
            }

            FilteredItems.Refresh();
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(EmptyMessage));
            CloseEditor();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            FormError = "Не удалось сохранить заготовку";
        }
    }

    private void EditItem(object? parameter)
    {
        if (parameter is not SnippetItemViewModel item) return;

        SetEditingItem(item);
        SelectedType = item.Type;
        NewComment = item.Comment;
        NewValue = item.Value;
        FormError = string.Empty;
        IsEditorOpen = true;
    }

    private void SetEditingItem(SnippetItemViewModel? item)
    {
        _editingItem = item;
    }

    private async void CopyItem(object? parameter)
    {
        if (parameter is not SnippetItemViewModel item) return;

        try
        {
            Clipboard.SetText(item.Value);
            foreach (var snippet in Items) snippet.IsCopied = false;
            item.IsCopied = true;
            await Task.Delay(1200);
            item.IsCopied = false;
        }
        catch
        {
            // Windows can temporarily lock the clipboard; the next click will retry.
        }
    }

    private void DeleteItem(object? parameter)
    {
        if (parameter is not SnippetItemViewModel item) return;

        try
        {
            _storageService.Delete(item.Id);
            Items.Remove(item);
            FilteredItems.Refresh();
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(EmptyMessage));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Leave the row intact when persistence fails.
        }
    }

    private void SelectType(object? parameter)
    {
        if (parameter is SnippetType type) SelectedType = type;
    }

    private bool FilterItems(object item)
    {
        if (item is not SnippetItemViewModel snippet) return false;
        if (string.IsNullOrWhiteSpace(SearchQuery)) return true;

        return snippet.CommentDisplay.Contains(SearchQuery, StringComparison.CurrentCultureIgnoreCase)
               || snippet.Value.Contains(SearchQuery, StringComparison.CurrentCultureIgnoreCase)
               || snippet.TypeDisplay.Contains(SearchQuery, StringComparison.CurrentCultureIgnoreCase);
    }
}

public sealed class SnippetItemViewModel : ObservableObject
{
    private bool _isCopied;

    public SnippetItemViewModel(SnippetItem item)
    {
        Id = item.Id;
        Type = item.Type;
        Comment = item.Comment;
        Value = item.Value;
    }

    public Guid Id { get; }

    public SnippetType Type { get; }

    public string Comment { get; }

    public string Value { get; }

    public string TypeDisplay => Type switch
    {
        SnippetType.Text => "Текст",
        SnippetType.Link => "Ссылка",
        SnippetType.Email => "Почта",
        SnippetType.Phone => "Телефон",
        SnippetType.User => "Почта",
        _ => "Текст"
    };

    public string CommentDisplay => string.IsNullOrWhiteSpace(Comment) ? TypeDisplay : Comment;

    public string Icon => Type switch
    {
        SnippetType.Text => "T",
        SnippetType.Link => "\uE71B",
        SnippetType.Email => "@",
        SnippetType.Phone => "\uE717",
        SnippetType.User => "@",
        _ => "•"
    };

    public string IconFontFamily => Type is SnippetType.Link or SnippetType.Phone
        ? "Segoe MDL2 Assets"
        : "Segoe UI";

    public bool IsCopied
    {
        get => _isCopied;
        set => SetProperty(ref _isCopied, value);
    }
}
