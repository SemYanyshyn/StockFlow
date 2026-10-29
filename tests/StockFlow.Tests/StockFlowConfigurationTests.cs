using System.Text.Json;
using Microsoft.Extensions.Configuration;
using StockFlow.BusinessLogic;
using Xunit;

namespace StockFlow.Tests;

// These tests restore every process-wide setting in finally/Dispose and cannot run concurrently.
[CollectionDefinition("Process configuration", DisableParallelization = true)]
public sealed class ProcessConfigurationCollection
{
}

[Collection("Process configuration")]
public sealed class StockFlowConfigurationTests : IDisposable
{
    private const string Variable = "STOCKFLOW_ConnectionStrings__StockFlow";
    private readonly string? _previousVariable = Environment.GetEnvironmentVariable(Variable);
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "stockflow-config-" + Guid.NewGuid());

    public StockFlowConfigurationTests()
    {
        Directory.CreateDirectory(_directory);
        Environment.SetEnvironmentVariable(Variable, null);
    }

    [Fact]
    public void MissingLocalFileIsOptionalAndExampleIsNeverLoaded()
    {
        File.WriteAllText(Path.Combine(_directory, "appsettings.example.json"), Json("example-must-not-be-used"));
        var config = StockFlowConfiguration.Load(_directory);
        using var lifetime = config as IDisposable;
        Assert.Null(config.GetConnectionString("StockFlow"));
    }

    [Fact]
    public void JsonLoadsFromExplicitBaseDirectory()
    {
        File.WriteAllText(Path.Combine(_directory, "appsettings.json"), Json("json-test-value"));
        var config = StockFlowConfiguration.Load(_directory);
        using var lifetime = config as IDisposable;
        Assert.Equal("json-test-value", config.GetConnectionString("StockFlow"));
    }

    [Fact]
    public void DefaultBaseDirectoryDoesNotFollowTerminalWorkingDirectory()
    {
        File.WriteAllText(Path.Combine(_directory, "appsettings.json"), Json("wrong-working-directory"));
        var previous = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = _directory;
            var config = StockFlowConfiguration.Load();
            using var lifetime = config as IDisposable;
            Assert.Null(config.GetConnectionString("StockFlow"));
        }
        finally { Environment.CurrentDirectory = previous; }
    }

    [Fact]
    public void EnvironmentVariableOverridesJson()
    {
        File.WriteAllText(Path.Combine(_directory, "appsettings.json"), Json("json-test-value"));
        Environment.SetEnvironmentVariable(Variable, "environment-test-value");
        var config = StockFlowConfiguration.Load(_directory);
        using var lifetime = config as IDisposable;
        Assert.Equal("environment-test-value", config.GetConnectionString("StockFlow"));
    }

    [Fact]
    public void InvalidJsonDoesNotDiscloseFileContents()
    {
        const string marker = "test-only-sensitive-json-marker";
        File.WriteAllText(Path.Combine(_directory, "appsettings.json"), "{ invalid-json-" + marker);
        var error = Assert.Throws<InvalidOperationException>(() => StockFlowConfiguration.Load(_directory));
        Assert.Contains("appsettings.json", error.Message);
        Assert.DoesNotContain(marker, error.ToString());
        Assert.Null(error.InnerException);
    }

    private static string Json(string value) => JsonSerializer.Serialize(new
    {
        ConnectionStrings = new { StockFlow = value }
    });

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(Variable, _previousVariable);
        Directory.Delete(_directory, recursive: true);
    }
}
