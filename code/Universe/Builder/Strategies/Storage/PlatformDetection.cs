namespace Universe.Builder.Strategies.Storage;

/// <summary>
/// Detects the runtime platform to determine safe default storage paths.
/// Azure App Service and Azure Functions mount the application directory on Azure Files (SMB),
/// which does not support the POSIX locking semantics required by SQLite WAL mode and can
/// cause unreliable file locking for JSON persistence.
/// </summary>
internal static class PlatformDetection
{
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
}
