using System.Runtime.InteropServices;

namespace Universe.Builder.Strategies.Storage;

/// <summary>
/// Detects the runtime platform to determine safe default storage paths.
/// Azure App Service and Azure Functions mount the application directory on Azure Files (SMB),
/// which does not support the POSIX locking semantics required by SQLite WAL mode and can
/// cause unreliable file locking for JSON persistence.
/// </summary>
internal static class PlatformDetection
{
    private const UnixFileMode OwnerOnlyFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const uint PreserveGroup = uint.MaxValue;

    /// <summary>
    /// Detects Azure App Service or Azure Functions by checking well-known environment variables.
    /// Returns <c>true</c> when <c>WEBSITE_INSTANCE_ID</c> is set (App Service / Functions on Azure),
    /// or when <c>FUNCTIONS_WORKER_RUNTIME</c> is set and <c>AZURE_FUNCTIONS_ENVIRONMENT</c> is not
    /// <c>"Development"</c> (to avoid treating local Functions development as Azure).
    /// </summary>
    internal static bool IsAzureEnvironment()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID")))
            return true;

        // FUNCTIONS_WORKER_RUNTIME is set in local.settings.json during local dev.
        // Exclude local development by checking AZURE_FUNCTIONS_ENVIRONMENT.
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME")))
        {
            string env = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT");
            return !string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    /// <summary>
    /// Resolves a platform-aware local temp directory.
    /// Prefers <c>TMP</c> / <c>TEMP</c> environment variables (which on Azure point to
    /// SSD-backed local storage), falling back to <see cref="Path.GetTempPath"/>.
    /// </summary>
    internal static string GetLocalTempDirectory() =>
        Environment.GetEnvironmentVariable("TMP")
        ?? Environment.GetEnvironmentVariable("TEMP")
        ?? Path.GetTempPath();

    /// <summary>
    /// Validates and normalizes a storage file path.
    /// Resolves the path via <see cref="Path.GetFullPath(string)"/>, ensures the result
    /// is rooted (absolute), and rejects paths containing null bytes.
    /// </summary>
    /// <param name="path">The raw file path to validate.</param>
    /// <returns>The fully-qualified, validated path.</returns>
    /// <exception cref="UniverseException">
    /// Thrown when the path is empty, contains null bytes, or cannot be resolved to a rooted path.
    /// </exception>
    internal static string ValidateStoragePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new UniverseException("Storage path must not be empty.");

        if (path.Contains('\0'))
            throw new UniverseException("Storage path contains invalid characters.");

        string fullPath = Path.GetFullPath(path);

        if (!Path.IsPathRooted(fullPath))
            throw new UniverseException($"Storage path must resolve to an absolute path. Got: '{fullPath}'");

        return fullPath;
    }

    /// <summary>
    /// Sets restrictive permissions (owner-only) on Unix-like systems to prevent other users from reading statistics.
    /// No-op on Windows where NTFS ACLs are used instead.
    /// </summary>
    internal static void SetRestrictivePermissions(string path)
    {
        if (!UsesUnixFilePermissions())
            return;

        try
        {
            if (Directory.Exists(path))
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            else if (File.Exists(path))
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (System.Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            System.Diagnostics.Trace.TraceWarning($"[UniverseQuery] Failed to set file permissions: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a regular file with owner-only permissions when it does not already exist, then
    /// verifies that the current process owns it and that no group or other permissions remain.
    /// No-op on non-Unix platforms, where filesystem ACL inheritance remains authoritative.
    /// </summary>
    internal static void CreateOrSecureOwnerOnlyFile(string path)
    {
        if (!UsesUnixFilePermissions())
            return;

        FileInfo file = new(path);
        file.Refresh();
        RejectNonRegularFile(file, path, allowMissing: true);

        if (!file.Exists)
        {
            try
            {
                using FileStream _ = new(path, new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.ReadWrite,
                    Share = FileShare.ReadWrite | FileShare.Delete,
                    UnixCreateMode = OwnerOnlyFileMode
                });
            }
            catch (IOException)
            {
                // A competing creator or non-regular path may now occupy the name. Validate it
                // below so the caller either secures a regular file or fails closed.
                file.Refresh();
                RejectNonRegularFile(file, path, allowMissing: false);
            }
        }

        EnsureOwnerOnlyFile(path);
    }

    /// <summary>
    /// Fails unless <paramref name="path"/> is a process-owned regular file with mode 0600.
    /// </summary>
    internal static void EnsureOwnerOnlyFile(string path)
    {
        if (!UsesUnixFilePermissions())
            return;

        try
        {
            FileInfo file = new(path);
            file.Refresh();
            RejectNonRegularFile(file, path, allowMissing: false);

            uint effectiveUserId = GetEffectiveUserId();
            if (ChangeOwner(path, effectiveUserId, PreserveGroup) != 0)
            {
                int error = Marshal.GetLastPInvokeError();
                throw new UnauthorizedAccessException(
                    $"Unable to assign SQLite statistics file ownership to the current process (errno {error}).");
            }

            File.SetUnixFileMode(path, OwnerOnlyFileMode);

            file.Refresh();
            RejectNonRegularFile(file, path, allowMissing: false);
            UnixFileMode effectiveMode = File.GetUnixFileMode(path);
            if (effectiveMode != OwnerOnlyFileMode)
            {
                throw new UnauthorizedAccessException(
                    $"SQLite statistics file permissions must be owner read/write only, but were '{effectiveMode}'.");
            }
        }
        catch (UniverseException)
        {
            throw;
        }
        catch (System.Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or PlatformNotSupportedException
            or DllNotFoundException
            or EntryPointNotFoundException)
        {
            throw new UniverseException(
                $"Unable to secure SQLite statistics file '{path}'.",
                ex);
        }
    }

    private static bool UsesUnixFilePermissions() =>
        OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();

    private static void RejectNonRegularFile(FileInfo file, string path, bool allowMissing)
    {
        if (!file.Exists)
        {
            if (allowMissing && file.LinkTarget is null && !Directory.Exists(path))
                return;

            throw new UniverseException($"SQLite statistics path must identify a regular file: '{path}'.");
        }

        FileAttributes attributes = file.Attributes;
        if (file.LinkTarget is not null
            || (attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.Device)) != 0)
        {
            throw new UniverseException($"SQLite statistics path must identify a regular file: '{path}'.");
        }
    }

    private static uint GetEffectiveUserId() => OperatingSystem.IsLinux()
        ? LinuxNative.GetEffectiveUserId()
        : MacOsNative.GetEffectiveUserId();

    private static int ChangeOwner(string path, uint owner, uint group) => OperatingSystem.IsLinux()
        ? LinuxNative.ChangeOwner(path, owner, group)
        : MacOsNative.ChangeOwner(path, owner, group);

    private static class LinuxNative
    {
        [DllImport("libc", EntryPoint = "geteuid")]
        internal static extern uint GetEffectiveUserId();

        [DllImport("libc", EntryPoint = "chown", SetLastError = true)]
        internal static extern int ChangeOwner(string path, uint owner, uint group);
    }

    private static class MacOsNative
    {
        [DllImport("libSystem.B.dylib", EntryPoint = "geteuid")]
        internal static extern uint GetEffectiveUserId();

        [DllImport("libSystem.B.dylib", EntryPoint = "chown", SetLastError = true)]
        internal static extern int ChangeOwner(string path, uint owner, uint group);
    }
}
