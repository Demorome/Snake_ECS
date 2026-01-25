#if DEBUG

using System;
using System.Diagnostics;
using MoonWorks;
using MoonWorks.Graphics;
using MoonWorks.Storage;

// TODO: Make this Dispose-able
internal static class AssetHotReloadManager
{
    private static FileSystemWatcher? Watcher;

    private class AssetUpdateHandler
    {
        /// <summary>
        /// Arg is the path of the changed asset file, 
        /// relative to base app path. <br/>
        /// Returns true if the file was successfully updated. <br/>
        /// If it returns false, then we'll retry with a delay.
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
        Watcher.IncludeSubdirectories = true;
        Watcher.EnableRaisingEvents = true;
        Watcher.Changed += OnChanged;
        Watcher.Error += OnError;

        // Try to avoid missing events by not exceeding event buffer size.
        // Credits to Roger Sanders:
        // https://stackoverflow.com/a/35432077/32021917
        Watcher.InternalBufferSize = 64 * 1024;

        Timer.Start();

        Logger.LogInfo("Hot-reloading of assets in the Content directory is supported.");
    }

    public static void Dispose()
    {
        Watcher?.Dispose();
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
    static int MinWaitTimeBeforeProcessingUpdatesMs = 200;

    public static void ProcessChangesOnMainThread()
    {
        var storage = new TitleStorage(SDL3.SDL.SDL_GetBasePath());

        lock (ToProcess)
        {
            foreach ((string relativePath, var handler) in ToProcess)
            {
                TryReloadFile(
                    handler,
                    SDL3.SDL.SDL_GetBasePath() + relativePath,
                    relativePath,
                    storage
                );
            }
            ToProcess.Clear();
        }

        storage.Dispose();
    }

    /// <summary>
    /// WARNING: This can run outside the main thread, 
    /// and before the file is fully updated! <br/>
    /// Can also run multiple times for the same file change!
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
                    TargetWaitTimeMs = (int)Timer.Elapsed.TotalMilliseconds 
                        + MinWaitTimeBeforeProcessingUpdatesMs;
                }

                return;
            }
        }
        
        Logger.LogWarn($"Asset hot-reloading: Currently not supporting {relativePath}");
    }

    private static void OnError(object sender, ErrorEventArgs e)
    {
        Logger.LogError("Asset hot-reloading: The FileSystemWatcher has detected an error");

        //  Give more information if the error is due to an internal buffer overflow.
        if (e.GetException().GetType() == typeof(InternalBufferOverflowException))
        {
            //  This can happen if Windows is reporting many file system events quickly
            //  and internal buffer of the  FileSystemWatcher is not large enough to handle this
            //  rate of events. The InternalBufferOverflowException error informs the application
            //  that some of the file system events are being lost.
            Console.WriteLine(
                "The FileSystemWatcher experienced an internal buffer overflow: " 
                + e.GetException().Message
            );
        }
    }


    // Credits to Chris Schiffhauer: 
    // https://stackoverflow.com/a/21053032/32021917
    // Slightly modified.
    private static bool TryReloadFile(
        AssetUpdateHandler handler,
        string fullPath,
        string relativePath,
        TitleStorage titleStorage
    )
    {
        var success = false;
        const int MaximumAttemptsAllowed = 20;
        var attemptsMade = 0;

        while (true)
        {
            // Wait a minimum amount, to avoid processing duplicate events.
            while (TargetWaitTimeMs > Timer.Elapsed.TotalMilliseconds)
            {
                var sleep = TargetWaitTimeMs - (int)Timer.Elapsed.TotalMilliseconds;
                Logger.LogInfo($"Asset hot-reload: Waiting for {sleep}ms due to new change");
                Thread.Sleep(sleep);
            }

            // For some reason, the below File.Open trick 
            // doesn't fully work for me, on CachyOS linux, 
            // when saving a large atlas .png in Aseprite.
            // Trying to read the file's png contents fails, but
            // no IOException is thrown, so checking for failure to
            // actually load+read the file seems necessary.
            try
            {
                using (FileStream stream = File.Open(
                    fullPath, 
                    FileMode.Open, 
                    FileAccess.ReadWrite, 
                    FileShare.None))
                {
                    // FIXME: Having the stream open at the same time as
                    // trying to use TitleStorage could maybe cause an error?
                    if (!handler.OnUpdateAsset(relativePath, titleStorage))
                    {
                        Logger.LogInfo("Asset hot-reload: Failed to load file, will retry...");
                    }
                    else
                    {
                        success = true;
                    }
                }
            }
            catch (IOException)
            {
                Logger.LogInfo("Asset hot-reload: Can't access file yet, sleeping...");
            }

            if (success || attemptsMade > MaximumAttemptsAllowed)
            {
                break;
            }
            
            ++attemptsMade;
            Thread.Sleep(100);
        }

        if (success)
        {
            Logger.LogInfo($"Asset hot-reload: Hot-reloaded {relativePath}");
        }
        else 
        {
            Logger.LogError($"Asset hot-reload: Failed to read file at {relativePath}");
        }

        return success;
    }

    /// <summary>
    /// Assumes the Texture
    /// </summary>
	public static Texture? Debug_HotReloadImage(
        Texture? Texture,
		GraphicsDevice graphicsDevice, 
		string compressedImagePartialPath,
		TitleStorage storage)
	{
		var resourceUploader = new ResourceUploader(graphicsDevice);

		// FIXME: Shouldn't need to create a new texture if new JSON 
		// was processed before this!
        // I don't think this can be fixed, unless I can somehow guarantee
        // that the JSON file-changed event will always be processed 
        // before this.
		Texture?.Dispose();
		Texture = resourceUploader.CreateTexture2DFromCompressed(
			storage,
			compressedImagePartialPath,
			TextureFormat.R8G8B8A8Unorm,
			TextureUsageFlags.Sampler
		);

		if (Texture != null)
		{
			resourceUploader.Upload();
		}
		resourceUploader.Dispose();

		return Texture;
	}
}

#endif