using System;
using System.Collections.Generic;
using Mate.Models;

namespace Mate.Services.Interfaces;

public interface ISnippetStorageService
{
    IReadOnlyList<SnippetItem> GetItems();

    SnippetItem Add(SnippetType type, string comment, string value);

    SnippetItem Update(Guid id, SnippetType type, string comment, string value);

    void Delete(Guid id);
}
