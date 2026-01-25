using System.Diagnostics;
using MoonWorks;

// TODO: Make this Dispose-able
internal static class ProjectContentPorting
{
    /// <summary>
    /// Watches for changes in the project's directory 
    /// (i.e. where the .csproj is).
    /// </summary>
    private static FileSystemWatcher? ProjectContentWatcher;

    private static readonly string ProjectContentFullPath = Path.Combine(
        Debug_ProjectSourcePath.Value,
        "Content",
        "ProcessedContent"
    );
    
    public static void Init()
    {
        ProjectContentWatcher = new FileSystemWatcher(ProjectContentFullPath);
        ProjectContentWatcher.NotifyFilter 
            = NotifyFilters.LastWrite
            | NotifyFilters.CreationTime
            | NotifyFilters.Security
            | NotifyFilters.Size
            | NotifyFilters.FileName;
        ProjectContentWatcher.IncludeSubdirectories = true;
        ProjectContentWatcher.EnableRaisingEvents = true;
        ProjectContentWatcher.Changed += OnProjectContentChanged;
        ProjectContentWatcher.Created += OnProjectContentCreated;
        ProjectContentWatcher.Error += OnError;

        // Try to avoid missing events by not exceeding event buffer size.
        // Credits to Roger Sanders:
        // https://stackoverflow.com/a/35432077/32021917
        ProjectContentWatcher.InternalBufferSize = 64 * 1024;

        Timer.Start();
    }

    private static Stack<string> ToProcess = new();

    static readonly Stopwatch Timer = new Stopwatch();
    static int TargetWaitTimeMs;
    static int MinWaitTimeBeforeProcessingUpdatesMs = 200;

    public static void ProcessChangesOnMainThread()
    {
        lock (ToProcess)
        {
            if (ToProcess.Count != 0)
            {
                Logger.LogInfo("ProjectContentPorting: Auto-porting changed files to exe location.");
            }

            foreach (string fullPath in ToProcess)
            {
                ProcessChange(fullPath);
            }
            ToProcess.Clear();
        }
    }

    private static void ProcessChange(string fullPath)
    {
         // Wait a minimum amount, to avoid processing duplicate events.
        while (TargetWaitTimeMs > Timer.Elapsed.TotalMilliseconds)
        {
            var sleep = TargetWaitTimeMs - (int)Timer.Elapsed.TotalMilliseconds;
            Logger.LogInfo($"ProjectContentPorting: Waiting for {sleep}ms due to new change");
            Thread.Sleep(sleep);
        }

        string dest;
        {
            var relativePath = Path.GetRelativePath(
                ProjectContentFullPath,
                fullPath
            );
            dest = SDL3.SDL.SDL_GetBasePath()
                + "Content/"
                + relativePath;
        }

        var fileInfo = new FileInfo(fullPath);
        try 
        {
            fileInfo.CopyTo(dest, true);
        }
        catch (Exception e)
        {
            Logger.LogError("ProjectContentPorting: Exception occured: ");
            Logger.LogError(e.Message);
        }
    }

    /// <summary>
    /// WARNING: This can run outside the main thread, 
    /// and before the file is fully updated! <br/>
    /// Can also run multiple times for the same file change!
    /// </summary>
    private static void OnProjectContentChanged(
        object sender, 
        FileSystemEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Changed)
        {
            return;
        }
        
        lock (ToProcess)
        {
            TargetWaitTimeMs = (int)Timer.Elapsed.TotalMilliseconds 
                + MinWaitTimeBeforeProcessingUpdatesMs;

            ToProcess.Push(e.FullPath);
        }
    }

    private static void OnProjectContentCreated(
        object sender, 
        FileSystemEventArgs e)
    {
        var fullPath = e.FullPath;
        AssetHotReloadManager.MaybeChangeExtension(ref fullPath);

        lock (ToProcess)
        {
            TargetWaitTimeMs = (int)Timer.Elapsed.TotalMilliseconds 
                + MinWaitTimeBeforeProcessingUpdatesMs;
                
            ToProcess.Push(fullPath);
        }
    }

    private static void OnError(object sender, ErrorEventArgs e)
    {
        Logger.LogError("ProjectContentPorting: The FileSystemWatcher has detected an error");

        //  Give more information if the error is due to an internal buffer overflow.
        if (e.GetException().GetType() == typeof(InternalBufferOverflowException))
        {
            //  This can happen if Windows is reporting many file system events quickly
            //  and internal buffer of the  FileSystemWatcher is not large enough to handle this
            //  rate of events. The InternalBufferOverflowException error informs the application
            //  that some of the file system events are being lost.
            Logger.LogError(
                "The FileSystemWatcher experienced an internal buffer overflow: " 
                + e.GetException().Message
            );
        }
    }
}