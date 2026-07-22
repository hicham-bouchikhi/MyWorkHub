namespace MyWorkHub.Core;

/// <summary>
/// Centralized runtime paths under <c>%USERPROFILE%\.MyWorkHub\</c>
/// (see ARCHITECTURE.md §13).
/// </summary>
public static class AppPaths
{
    public static string RootDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".MyWorkHub");

    public static string LogsDir => Path.Combine(RootDir, "logs");
    public static string TempDir => Path.Combine(RootDir, "temp");
    public static string ErrorsDir => Path.Combine(RootDir, "errors");

    public static string DbPath => Path.Combine(RootDir, "MyWorkHub.db");
    public static string UserAppSettingsPath => Path.Combine(RootDir, "appsettings.json");
    public static string MsalTokenCachePath => Path.Combine(RootDir, "msal_token_cache.bin");

    /// <summary>Seed template for the Claude Code review agent, copied here on first launch.</summary>
    public static string ReviewAgentPath => Path.Combine(RootDir, "review-agent.md");

    /// <summary>Folder where generated HTML review reports are written.</summary>
    public static string ReviewsDir => Path.Combine(RootDir, "reviews");

    /// <summary>Data Protection key ring (encrypts stored credentials); cross-platform.</summary>
    public static string DataProtectionKeysDir => Path.Combine(RootDir, "keys");

    /// <summary>Creates the runtime folder tree on first launch. Idempotent.</summary>
    public static void EnsureCreated()
    {
        Directory.CreateDirectory(RootDir);
        Directory.CreateDirectory(LogsDir);
        Directory.CreateDirectory(TempDir);
        Directory.CreateDirectory(ErrorsDir);
        Directory.CreateDirectory(ReviewsDir);
        Directory.CreateDirectory(DataProtectionKeysDir);
    }
}
