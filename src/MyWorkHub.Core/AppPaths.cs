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

    /// <summary>Data Protection key ring (encrypts stored credentials); cross-platform.</summary>
    public static string DataProtectionKeysDir => Path.Combine(RootDir, "keys");

    /// <summary>MSAL (Microsoft 365 sign-in) token cache, encrypted per-OS by the MSAL cache helper.</summary>
    public static string MsalTokenCachePath => Path.Combine(RootDir, "msal_token_cache.bin");

    /// <summary>Built-in pull request review-agent template, seeded from the shipped copy on first run.</summary>
    public static string ReviewAgentPath => Path.Combine(RootDir, "review-agent.md");

    /// <summary>Default folder repositories are cloned into for pull request reviews (created on demand).</summary>
    public static string ReviewRepositoriesDir => Path.Combine(RootDir, "repos");

    /// <summary>Creates the runtime folder tree on first launch. Idempotent.</summary>
    public static void EnsureCreated()
    {
        Directory.CreateDirectory(RootDir);
        Directory.CreateDirectory(LogsDir);
        Directory.CreateDirectory(TempDir);
        Directory.CreateDirectory(ErrorsDir);
        Directory.CreateDirectory(DataProtectionKeysDir);
    }
}
