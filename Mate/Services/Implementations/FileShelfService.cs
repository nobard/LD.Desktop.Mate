using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Mate.Services.Interfaces;

namespace Mate.Services.Implementations;

public sealed class FileShelfService : IFileShelfService
{
    private const int WhKeyboardLowLevel = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int WmClipboardUpdate = 0x031D;
    private const int VkShift = 0x10;
    private const int VkS = 0x53;
    private const int VkSnapshot = 0x2C;
    private const int VkLeftWindows = 0x5B;
    private const int VkRightWindows = 0x5C;

    private static readonly Guid ScreenshotsFolderId = new("B7BEDE81-DF94-4682-A7D8-57A52620B86F");
    private static readonly HashSet<string> ScreenshotExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp"
    };

    private readonly ConcurrentDictionary<string, byte> _pendingScreenshots = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _storageLock = new();
    private readonly LowLevelKeyboardProcedure _keyboardProcedure;
    private readonly string _storageSettingsPath;
    private FileSystemWatcher? _screenshotWatcher;
    private FileSystemWatcher? _storageWatcher;
    private HwndSource? _clipboardSource;
    private nint _keyboardHook;
    private long _screenshotArmedAtTicks;
    private long _lastClipboardScreenshotTicks;
    private int _clipboardCaptureInProgress;
    private bool _disposed;

    public FileShelfService()
    {
        var dataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LD.Desktop.Mate");
        Directory.CreateDirectory(dataFolder);
        _storageSettingsPath = Path.Combine(dataFolder, "file-shelf-folder.txt");
        StorageFolder = LoadStorageFolder(dataFolder);
        Directory.CreateDirectory(StorageFolder);

        _keyboardProcedure = KeyboardHookCallback;
        InitializeStorageWatcher();
        InitializeFolderWatcher();
        InitializeClipboardMonitor();
    }

    public event Action? FilesChanged;

    public event Action? StorageFolderChanged;

    public string StorageFolder { get; private set; }

    public IReadOnlyList<string> GetFiles()
    {
        var storageFolder = StorageFolder;
        if (!Directory.Exists(storageFolder)) return Array.Empty<string>();

        try
        {
            return Directory
                .EnumerateFiles(storageFolder, "*", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    public bool SetStorageFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return false;

        try
        {
            var fullPath = Path.GetFullPath(folderPath.Trim());
            Directory.CreateDirectory(fullPath);

            lock (_storageLock)
            {
                if (string.Equals(StorageFolder, fullPath, StringComparison.OrdinalIgnoreCase)) return true;
                File.WriteAllText(_storageSettingsPath, fullPath);
                StorageFolder = fullPath;
            }

            InitializeStorageWatcher();
            StorageFolderChanged?.Invoke();
            FilesChanged?.Invoke();
            return true;
        }
        catch (Exception exception) when (exception is IOException
                                           or UnauthorizedAccessException
                                           or ArgumentException
                                           or NotSupportedException)
        {
            return false;
        }
    }

    public void AddFiles(IEnumerable<string> sourcePaths)
    {
        var added = false;
        try
        {
            foreach (var sourcePath in sourcePaths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!File.Exists(sourcePath)) continue;
                if (IsInsideStorage(sourcePath)) continue;

                CopyIntoStorage(sourcePath);
                added = true;
            }
        }
        finally
        {
            if (added) FilesChanged?.Invoke();
        }
    }

    public void DeleteFiles(IEnumerable<string> storedPaths)
    {
        var deleted = false;
        try
        {
            lock (_storageLock)
            {
                foreach (var path in storedPaths.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (!IsInsideStorage(path) || !File.Exists(path)) continue;

                    File.Delete(path);
                    deleted = true;
                }
            }
        }
        finally
        {
            if (deleted) FilesChanged?.Invoke();
        }
    }

    private void InitializeFolderWatcher()
    {
        try
        {
            var screenshotsFolder = GetScreenshotsFolder();
            if (string.IsNullOrWhiteSpace(screenshotsFolder)) return;

            Directory.CreateDirectory(screenshotsFolder);
            _screenshotWatcher = new FileSystemWatcher(screenshotsFolder)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.Size
            };
            _screenshotWatcher.Created += ScreenshotCreated;
            _screenshotWatcher.Renamed += ScreenshotRenamed;
            _screenshotWatcher.EnableRaisingEvents = true;
        }
        catch (IOException)
        {
            _screenshotWatcher = null;
        }
        catch (UnauthorizedAccessException)
        {
            _screenshotWatcher = null;
        }
    }

    private void InitializeStorageWatcher()
    {
        if (_storageWatcher is not null)
        {
            _storageWatcher.EnableRaisingEvents = false;
            _storageWatcher.Created -= StorageWatcherChanged;
            _storageWatcher.Deleted -= StorageWatcherChanged;
            _storageWatcher.Renamed -= StorageFolderRenamed;
            _storageWatcher.Dispose();
            _storageWatcher = null;
        }

        try
        {
            Directory.CreateDirectory(StorageFolder);
            _storageWatcher = new FileSystemWatcher(StorageFolder)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite
            };
            _storageWatcher.Created += StorageWatcherChanged;
            _storageWatcher.Deleted += StorageWatcherChanged;
            _storageWatcher.Renamed += StorageFolderRenamed;
            _storageWatcher.EnableRaisingEvents = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _storageWatcher = null;
        }
    }

    private void StorageWatcherChanged(object sender, FileSystemEventArgs e) => FilesChanged?.Invoke();

    private void StorageFolderRenamed(object sender, RenamedEventArgs e) => FilesChanged?.Invoke();

    private void InitializeClipboardMonitor()
    {
        try
        {
            var parameters = new HwndSourceParameters("LD.Desktop.Mate.ClipboardMonitor")
            {
                Width = 0,
                Height = 0,
                PositionX = -32000,
                PositionY = -32000,
                WindowStyle = 0
            };

            _clipboardSource = new HwndSource(parameters);
            _clipboardSource.AddHook(ClipboardWindowProcedure);
            AddClipboardFormatListener(_clipboardSource.Handle);

            _keyboardHook = SetWindowsHookEx(
                WhKeyboardLowLevel,
                _keyboardProcedure,
                GetModuleHandle(null),
                0);
        }
        catch
        {
            if (_clipboardSource is not null)
            {
                _clipboardSource.RemoveHook(ClipboardWindowProcedure);
                _clipboardSource.Dispose();
                _clipboardSource = null;
            }
        }
    }

    private nint KeyboardHookCallback(int code, nint message, nint data)
    {
        if (code >= 0 && (message == WmKeyDown || message == WmSysKeyDown))
        {
            var virtualKey = Marshal.ReadInt32(data);
            var isPrintScreen = virtualKey == VkSnapshot;
            var isSnippingShortcut = virtualKey == VkS &&
                                     IsKeyPressed(VkShift) &&
                                     (IsKeyPressed(VkLeftWindows) || IsKeyPressed(VkRightWindows));

            if (isPrintScreen || isSnippingShortcut)
            {
                Interlocked.Exchange(ref _screenshotArmedAtTicks, DateTime.UtcNow.Ticks);
            }
        }

        return CallNextHookEx(_keyboardHook, code, message, data);
    }

    private nint ClipboardWindowProcedure(
        nint window,
        int message,
        nint wordParameter,
        nint longParameter,
        ref bool handled)
    {
        if (message != WmClipboardUpdate) return nint.Zero;

        var armedAt = Interlocked.Read(ref _screenshotArmedAtTicks);
        if (armedAt == 0 || DateTime.UtcNow - new DateTime(armedAt, DateTimeKind.Utc) > TimeSpan.FromMinutes(3))
        {
            return nint.Zero;
        }

        if (Interlocked.CompareExchange(ref _clipboardCaptureInProgress, 1, 0) == 0)
        {
            _ = CaptureClipboardScreenshotAsync(armedAt);
        }

        return nint.Zero;
    }

    private async Task CaptureClipboardScreenshotAsync(long armedAt)
    {
        try
        {
            byte[]? imageBytes = null;
            for (var attempt = 0; attempt < 10 && imageBytes is null; attempt++)
            {
                if (_disposed) return;

                imageBytes = await Application.Current.Dispatcher.InvokeAsync(TryReadClipboardImage);
                if (imageBytes is null) await Task.Delay(100);
            }

            if (imageBytes is null) return;
            if (Interlocked.CompareExchange(ref _screenshotArmedAtTicks, 0, armedAt) != armedAt) return;

            SaveClipboardScreenshot(imageBytes);
            Interlocked.Exchange(ref _lastClipboardScreenshotTicks, DateTime.UtcNow.Ticks);
            FilesChanged?.Invoke();
        }
        finally
        {
            Interlocked.Exchange(ref _clipboardCaptureInProgress, 0);
        }
    }

    private static byte[]? TryReadClipboardImage()
    {
        try
        {
            if (!Clipboard.ContainsImage()) return null;
            var image = Clipboard.GetImage();
            if (image is null) return null;

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }
        catch (ExternalException)
        {
            return null;
        }
    }

    private void SaveClipboardScreenshot(byte[] imageBytes)
    {
        lock (_storageLock)
        {
            var fileName = $"Снимок {DateTime.Now:yyyy-MM-dd HH-mm-ss-fff}.png";
            var destinationPath = GetUniqueDestinationPath(fileName);
            File.WriteAllBytes(destinationPath, imageBytes);
        }
    }

    private void ScreenshotCreated(object sender, FileSystemEventArgs e) => QueueScreenshot(e.FullPath);

    private void ScreenshotRenamed(object sender, RenamedEventArgs e) => QueueScreenshot(e.FullPath);

    private void QueueScreenshot(string path)
    {
        if (!ScreenshotExtensions.Contains(Path.GetExtension(path))) return;
        if (!_pendingScreenshots.TryAdd(path, 0)) return;

        _ = ImportScreenshotAsync(path);
    }

    private async Task ImportScreenshotAsync(string path)
    {
        try
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                if (_disposed) return;

                try
                {
                    if (File.Exists(path))
                    {
                        await Task.Delay(600);
                        if (IsInsideStorage(path))
                        {
                            FilesChanged?.Invoke();
                            return;
                        }

                        var lastClipboardScreenshot = Interlocked.Read(ref _lastClipboardScreenshotTicks);
                        if (lastClipboardScreenshot != 0 &&
                            DateTime.UtcNow - new DateTime(lastClipboardScreenshot, DateTimeKind.Utc) < TimeSpan.FromSeconds(2))
                        {
                            return;
                        }

                        CopyIntoStorage(path);
                        FilesChanged?.Invoke();
                        return;
                    }
                }
                catch (IOException)
                {
                    // The screenshot application can keep the new file locked briefly.
                }
                catch (UnauthorizedAccessException)
                {
                    // Retry while Windows finishes writing the screenshot.
                }

                await Task.Delay(150);
            }
        }
        finally
        {
            _pendingScreenshots.TryRemove(path, out _);
        }
    }

    private string CopyIntoStorage(string sourcePath)
    {
        lock (_storageLock)
        {
            var sourceFullPath = Path.GetFullPath(sourcePath);
            var destinationPath = GetUniqueDestinationPath(Path.GetFileName(sourceFullPath));
            File.Copy(sourceFullPath, destinationPath, false);
            return destinationPath;
        }
    }

    private string GetUniqueDestinationPath(string fileName)
    {
        var destination = Path.Combine(StorageFolder, fileName);
        if (!File.Exists(destination)) return destination;

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var index = 2; ; index++)
        {
            destination = Path.Combine(StorageFolder, $"{baseName} ({index}){extension}");
            if (!File.Exists(destination)) return destination;
        }
    }

    private bool IsInsideStorage(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var storagePath = Path.GetFullPath(StorageFolder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(storagePath, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsKeyPressed(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private string LoadStorageFolder(string dataFolder)
    {
        var defaultFolder = Path.Combine(dataFolder, "Files");
        try
        {
            if (!File.Exists(_storageSettingsPath)) return defaultFolder;
            var configuredFolder = File.ReadAllText(_storageSettingsPath).Trim();
            if (string.IsNullOrWhiteSpace(configuredFolder)) return defaultFolder;

            var fullPath = Path.GetFullPath(configuredFolder);
            Directory.CreateDirectory(fullPath);
            return fullPath;
        }
        catch
        {
            return defaultFolder;
        }
    }

    private static string? GetScreenshotsFolder()
    {
        if (SHGetKnownFolderPath(ScreenshotsFolderId, 0, nint.Zero, out var pathPointer) == 0)
        {
            try
            {
                return Marshal.PtrToStringUni(pathPointer);
            }
            finally
            {
                Marshal.FreeCoTaskMem(pathPointer);
            }
        }

        var picturesFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return string.IsNullOrWhiteSpace(picturesFolder)
            ? null
            : Path.Combine(picturesFolder, "Screenshots");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_screenshotWatcher is not null)
        {
            _screenshotWatcher.EnableRaisingEvents = false;
            _screenshotWatcher.Created -= ScreenshotCreated;
            _screenshotWatcher.Renamed -= ScreenshotRenamed;
            _screenshotWatcher.Dispose();
        }

        if (_storageWatcher is not null)
        {
            _storageWatcher.EnableRaisingEvents = false;
            _storageWatcher.Created -= StorageWatcherChanged;
            _storageWatcher.Deleted -= StorageWatcherChanged;
            _storageWatcher.Renamed -= StorageFolderRenamed;
            _storageWatcher.Dispose();
            _storageWatcher = null;
        }

        if (_keyboardHook != nint.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = nint.Zero;
        }

        if (_clipboardSource is not null)
        {
            RemoveClipboardFormatListener(_clipboardSource.Handle);
            _clipboardSource.RemoveHook(ClipboardWindowProcedure);
            _clipboardSource.Dispose();
            _clipboardSource = null;
        }
    }

    private delegate nint LowLevelKeyboardProcedure(int code, nint wordParameter, nint longParameter);

    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid knownFolderId,
        uint flags,
        nint token,
        out nint path);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AddClipboardFormatListener(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveClipboardFormatListener(nint window);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(
        int hookId,
        LowLevelKeyboardProcedure procedure,
        nint module,
        uint threadId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nint wordParameter, nint longParameter);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);
}
