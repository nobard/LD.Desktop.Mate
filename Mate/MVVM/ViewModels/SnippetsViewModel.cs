using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using Mate.MVVM.Core;

namespace Mate.MVVM.ViewModels;

public sealed class SnippetsViewModel : ToolViewModel
{
    private string _searchQuery = string.Empty;

    public SnippetsViewModel()
    {
        Items = new ObservableCollection<PinnedItemViewModel>
        {
            new("@", "Почта", "adilkalkbergenov@gmail.com"),
            new("☎", "Телефон", "+7 700 123 45 67"),
            new("↗", "GitHub", "https://github.com/akalkbergenov"),
            new("✈", "Telegram", "@adilkalkbergenov"),
            new("●", "Рабочая почта", "adil@cyclop.app")
        };
        FilteredItems = CollectionViewSource.GetDefaultView(Items);
        FilteredItems.Filter = FilterItems;
    }

    public override string Title => "Закреплённые";

    public override string Description => "Часто используемые тексты и ссылки.";

    public ObservableCollection<PinnedItemViewModel> Items { get; }

    public ICollectionView FilteredItems { get; }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value)) FilteredItems.Refresh();
        }
    }

    private bool FilterItems(object item)
    {
        if (item is not PinnedItemViewModel pinnedItem) return false;
        if (string.IsNullOrWhiteSpace(SearchQuery)) return true;

        return pinnedItem.Title.Contains(SearchQuery, System.StringComparison.CurrentCultureIgnoreCase)
               || pinnedItem.Value.Contains(SearchQuery, System.StringComparison.CurrentCultureIgnoreCase);
    }
}

public sealed class PinnedItemViewModel
{
    public PinnedItemViewModel(string icon, string title, string value)
    {
        Icon = icon;
        Title = title;
        Value = value;
    }

    public string Icon { get; }

    public string Title { get; }

    public string Value { get; }
}
