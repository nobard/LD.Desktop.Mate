using System;
using System.Collections.Generic;

namespace Mate.Services.Interfaces;

public interface IFileShelfService : IDisposable
{
    event Action? FilesChanged;

    string StorageFolder { get; }

    IReadOnlyList<string> GetFiles();

    void AddFiles(IEnumerable<string> sourcePaths);

    void DeleteFiles(IEnumerable<string> storedPaths);
}
