#if DEBUG

using System;
using MoonWorks;

[assembly: System.Reflection.Metadata.MetadataUpdateHandler(
    typeof(CodeHotReloadManager)
    )
]

internal static class CodeHotReloadManager
{
    /// <summary>
    /// Gives update handlers an opportunity 
    /// to clear any caches that are inferred based 
    /// on the application's metadata. <br/>
    /// https://learn.microsoft.com/en-us/visualstudio/debugger/hot-reload-metadataupdatehandler?view=visualstudio
    /// </summary>
    public static void ClearCache(Type[]? types)
    {
        Logger.LogInfo("HOT RELOAD: Clearing cache");
    }

    /// <summary>
    /// After all <see cref="ClearCache"/> methods have been invoked, 
    /// this is invoked for every handler that specifies one. <br/>
    /// You might use this to refresh the UI. <br/>
    /// https://learn.microsoft.com/en-us/visualstudio/debugger/hot-reload-metadataupdatehandler?view=visualstudio
    /// </summary>
    public static void UpdateApplication(Type[]? types)
    {
        Logger.LogInfo("HOT RELOAD: Updating app");

        RollAndCash.Editor.DrawComponents.ReInitComponentTypesList();
    }
}

#endif