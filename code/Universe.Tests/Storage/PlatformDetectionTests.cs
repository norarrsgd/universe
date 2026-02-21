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
	public void IsAzureEnvironment_FunctionsWorkerRuntime_ReturnsTrue()
	{
		string original1 = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
		string original2 = Environment.GetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME");

		try
		{
			Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", null);
			Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated");
			Assert.True(PlatformDetection.IsAzureEnvironment());
		}
		finally
		{
			Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", original1);
			Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", original2);
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
	public void GetLocalTempDirectory_ReturnsNonEmptyPath()
	{
		string result = PlatformDetection.GetLocalTempDirectory();
		Assert.False(string.IsNullOrEmpty(result));
		Assert.True(Directory.Exists(result));
	}
}
