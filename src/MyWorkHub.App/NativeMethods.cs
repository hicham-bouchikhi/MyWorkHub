using System.Runtime.InteropServices;

namespace MyWorkHub.App;

internal static partial class NativeMethods
{
    /// <summary>libc <c>setenv</c>: changes the process environment seen by native code and child processes.</summary>
    [LibraryImport("libc", EntryPoint = "setenv", StringMarshalling = StringMarshalling.Utf8)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int setenv(string name, string value, int overwrite);
}
