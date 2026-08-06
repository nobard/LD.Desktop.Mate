using Mate.MVVM.Core;

namespace Mate.MVVM.ViewModels;

public abstract class ToolViewModel : BaseViewModel
{
    public abstract string Title { get; }

    public abstract string Description { get; }

    public virtual object? HeaderContent => null;
}
