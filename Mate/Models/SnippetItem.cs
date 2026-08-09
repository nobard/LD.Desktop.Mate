using System;

namespace Mate.Models;

public enum SnippetType
{
    Link,
    Email,
    Phone,
    User,
    Text
}

public sealed record SnippetItem(
    Guid Id,
    SnippetType Type,
    string Comment,
    string Value,
    DateTimeOffset CreatedAt);
