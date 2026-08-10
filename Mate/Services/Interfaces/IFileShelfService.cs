using System;
using System.Collections.Generic;

namespace Mate.Services.Interfaces;

public interface IFileShelfService : IDisposable
{
    event Action? FilesChanged;

    event Action? StorageFolderChanged;

    string StorageFolder { get; }

    IReadOnlyList<string> GetFiles();

    bool SetStorageFolder(string folderPath);

    void AddFiles(IEnumerable<string> sourcePaths);

    void DeleteFiles(IEnumerable<string> storedPaths);
}
