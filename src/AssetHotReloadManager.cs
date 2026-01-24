#if DEBUG

using System;
using System.Diagnostics;
using MoonWorks;
using MoonWorks.Storage;

internal static class AssetHotReloadManager
{
    private static FileSystemWatcher? Watcher;

    private class AssetUpdateHandler
    {
        /// <summary>
        /// Arg is the path of the changed asset file, 
        /// relative to base app path. <br/>
        /// Returns true if the file was updated.
        /// </summary>
        public required Func<string, TitleStorage, bool> OnUpdateAsset;

        /// <summary>
        /// The filter required to match with 
        /// the relative path of a changed asset file 
        /// before UpdateAsset can be called.
        /// </summary>
        public required string FolderFilter;
    }
    private static List<AssetUpdateHandler> Handlers = new();

    public static void Init()
    {
        Watcher = new FileSystemWatcher(
            Path.Combine(
                SDL3.SDL.SDL_GetBasePath(), 
                "Content"
            )
        );
        Watcher.NotifyFilter = NotifyFilters.LastWrite;
        Watcher.EnableRaisingEvents = true;
        Watcher.IncludeSubdirectories = true;
        Watcher.Changed += OnChanged;

        Timer.Start();

        Logger.LogInfo("Hot-reloading of assets in the Content directory is supported.");
    }

    public static void RegisterHandler(
        string relativePathFilter, 
        Func<string, TitleStorage, bool> onUpdateAsset)
    {
        Handlers.Add(new AssetUpdateHandler
        {
            OnUpdateAsset = onUpdateAsset,
            FolderFilter = relativePathFilter
        });
    }

    static Dictionary<string, AssetUpdateHandler> 
        ToProcess = new();

    static readonly Stopwatch Timer = new Stopwatch();
    static int TargetWaitTimeMs;

    public static void ProcessChangesOnMainThread()
    {
        var storage = new TitleStorage(SDL3.SDL.SDL_GetBasePath());

        lock (ToProcess)
        {
            foreach ((string relativePath, var handler) in ToProcess)
            {
                if (TryWaitForFile(SDL3.SDL.SDL_GetBasePath() + relativePath))
                {
                    if (handler.OnUpdateAsset(relativePath, storage))
                    {
                        Logger.LogInfo($"Hot-reloaded asset: {relativePath}");
                    }
                    else
                    {
                        Logger.LogError($"Unable to handle asset hot-reloading for {relativePath}");
                    }
                }
                else
                {
                    Logger.LogError($"Asset hot-reloading: Couldn't get file access at {relativePath}");
                }
            }
            ToProcess.Clear();
        }

        storage.Dispose();
    }

    /// <summary>
    /// WARNING: This can run outside the main thread, 
    /// and before the file is fully updated!
    /// </summary>
    private static void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Changed)
        {
            return;
        }

        var relativePath = Path.GetRelativePath(
            SDL3.SDL.SDL_GetBasePath(),
            e.FullPath
        );

        foreach (var handler in Handlers)
        {
            // FIXME: Might need to use this for cross-platform support:
            // https://stackoverflow.com/a/66877016/32021917
            if (relativePath.StartsWith(handler.FolderFilter))
            {
                lock (ToProcess)
                {
                    ToProcess.TryAdd(relativePath, handler);
                    TargetWaitTimeMs = (int)Timer.Elapsed.TotalMilliseconds + 3000;
                }

                return;
            }
        }
        
        Logger.LogWarn($"Currently not supporting hot-reloadable assets for {relativePath}");
    }

    // Credits to Chris Schiffhauer: 
    // https://stackoverflow.com/a/21053032/32021917
    /// <summary>
    /// Use this to verify if the file can be safely accessed. <br/>
    /// Will stall the thread to try to wait until it can be safely accessed. <br/>
    /// Returns true if we succeeded in accessing the file. <br/>
    /// WARNING: The file might soon after be claimed by something else,
    /// but this should be robust enough for our cases.
    /// </summary>
    private static bool TryWaitForFile(string path)
    {
        var canAccessFile = false;
        const int MaximumAttemptsAllowed = 20;
        var attemptsMade = 0;

        while (!canAccessFile && attemptsMade <= MaximumAttemptsAllowed)
        {
            // Sleep a minimum amount.
            // For some reason, the below File.Open trick isn't working for me,
            // on CachyOS linux, when saving a large atlas .png in Aseprite.
            // No exceptions are thrown, so I must rely on a bit of manual waiting.
            // FIXME: Find a better solution! 
            // This will probably break for huge large file changes!
            while (TargetWaitTimeMs > Timer.Elapsed.TotalMilliseconds)
            {
                var sleep = TargetWaitTimeMs - (int)Timer.Elapsed.TotalMilliseconds;
                Logger.LogInfo($"Asset hot-reloading: Waiting for {sleep}ms due to new change");
                Thread.Sleep(sleep);
            }

            try
            {
                using (FileStream stream = File.Open(
                    path, 
                    FileMode.Open, 
                    FileAccess.ReadWrite, 
                    FileShare.None))
                {
                    canAccessFile = true;
                }
            }
            catch (IOException)
            {
                Logger.LogInfo("Asset hot-reloading: Couldn't yet access file, sleeping...");
                attemptsMade++;
                Thread.Sleep(100);
            }
        }

        return canAccessFile;
    }
}

#endif