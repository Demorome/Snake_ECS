#if DEBUG

using System.Diagnostics;
using System.Runtime.CompilerServices;

///<summary>
/// Provides the full path to the source directory of the current project. <br/>
/// (Only meaningful on the machine where this code was compiled.) <br/>
/// From <a href="https://stackoverflow.com/a/66285728/773113"/> <br/>
/// NEVER change the path/name of this file without also changing myRelativePath! <br/>
/// WARNING: Will need to do a special workaround if you use `PathMap`!
///</summary>
internal static class Debug_ProjectSourcePath
{
    private const string myRelativePath = "src/ProjectSourcePath.cs";
    private static string? lazyValue;

    ///<summary>
    ///The full path to the source directory of the current project.
    ///</summary>
    public static string Value => lazyValue ??= calculate();

    private static string calculate( [CallerFilePath] string? path = null )
    {
        Debug.Assert( path!.EndsWith( myRelativePath, StringComparison.Ordinal ) );
        return path.Substring( 0, path.Length - myRelativePath.Length );
    }
}
#endif