using Universe.Builder.Strategies.Storage;
using Xunit;

namespace Universe.Tests.Storage;

public sealed class PlatformDetectionTests
{
    [Fact]
    public void IsAzureEnvironment_WebsiteInstanceId_ReturnsTrue()
    {
        string original = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");

        try
        {
            Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", "abc123");
            Assert.True(PlatformDetection.IsAzureEnvironment());
        }
        finally
        {
            Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", original);
        }
    }

    [Fact]
    public void IsAzureEnvironment_FunctionsWorkerRuntime_NonDev_ReturnsTrue()
    {
        string original1 = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
        string original2 = Environment.GetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME");
        string original3 = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT");

        try
        {
            Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", null);
            Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated");
            Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", "Production");
            Assert.True(PlatformDetection.IsAzureEnvironment());
        }
        finally
        {
            Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", original1);
            Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", original2);
            Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", original3);
        }
    }

    [Fact]
    public void IsAzureEnvironment_FunctionsLocalDev_ReturnsFalse()
    {
        string original1 = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
        string original2 = Environment.GetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME");
        string original3 = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT");

        try
        {
            Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", null);
            Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated");
            Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", "Development");
            Assert.False(PlatformDetection.IsAzureEnvironment());
        }
        finally
        {
            Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", original1);
            Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", original2);
            Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", original3);
        }
    }

    [Fact]
    public void IsAzureEnvironment_NeitherSet_ReturnsFalse()
    {
        string original1 = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
        string original2 = Environment.GetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME");

        try
        {
            Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", null);
            Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", null);
            Assert.False(PlatformDetection.IsAzureEnvironment());
        }
        finally
        {
            Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", original1);
            Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", original2);
        }
    }

    [Fact]
    public void GetLocalTempDirectory_ReturnsNonEmptyRootedPath()
    {
        string result = PlatformDetection.GetLocalTempDirectory();
        Assert.False(string.IsNullOrEmpty(result));
        Assert.True(Path.IsPathRooted(result));
    }

    [Fact]
    public void ValidateStoragePath_ValidAbsolutePath_ReturnsNormalized()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "test.db");
        string result = PlatformDetection.ValidateStoragePath(path);
        Assert.Equal(Path.GetFullPath(path), result);
        Assert.True(Path.IsPathRooted(result));
    }

    [Fact]
    public void ValidateStoragePath_EmptyPath_Throws()
    {
        Assert.Throws<Exception.UniverseException>(
            () => PlatformDetection.ValidateStoragePath(""));
    }

    [Fact]
    public void ValidateStoragePath_NullByteInPath_Throws()
    {
        Assert.Throws<Exception.UniverseException>(
            () => PlatformDetection.ValidateStoragePath("/tmp/test\0.db"));
    }
}
