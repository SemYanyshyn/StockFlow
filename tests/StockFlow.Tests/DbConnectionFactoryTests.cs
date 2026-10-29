using System.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockFlow.BusinessLogic;
using StockFlow.DataAccess;
using Xunit;

namespace StockFlow.Tests;

public sealed class DbConnectionFactoryTests
{
    // Synthetic test values: no real server or credential is used.
    private const string Valid = "Host=127.0.0.1;Port=1;Database=stockflow_test;Username=unit_test;Pooling=false";
    private const string SensitiveMarker = "test-only-sensitive-marker";

    private static IConfiguration Configuration(string? value) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:StockFlow"] = value })
        .Build();

    [Fact]
    public void NullConfigurationIsRejected() =>
        Assert.Throws<ArgumentNullException>(() => new DbConnectionFactory(null!));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingConnectionStringGivesActionableError(string? value)
    {
        var error = Assert.Throws<InvalidOperationException>(() => new DbConnectionFactory(Configuration(value)));
        Assert.Contains("ConnectionStrings:StockFlow", error.Message);
        Assert.Contains("appsettings.json", error.Message);
    }

    [Theory]
    [InlineData("Host=localhost;Database=db;Username=user;UnsupportedKey=bad")]
    [InlineData("Host=localhost;Port=not-a-number")]
    [InlineData("Host=localhost;Port=99999999999999999999999999")]
    public void MalformedConnectionStringDoesNotDiscloseValues(string value)
    {
        var input = value + ";Password=" + SensitiveMarker;
        var error = Assert.Throws<InvalidOperationException>(() => new DbConnectionFactory(Configuration(input)));
        Assert.Contains("ConnectionStrings:StockFlow", error.Message);
        Assert.DoesNotContain(SensitiveMarker, error.ToString());
        Assert.DoesNotContain(input, error.ToString());
        Assert.Null(error.InnerException);
    }

    [Theory]
    [InlineData("Database=db;Username=user")]
    [InlineData("Host=localhost;Username=user")]
    [InlineData("Host=localhost;Database=db")]
    [InlineData("Host=localhost;Database=db;Username=user;Port=0")]
    [InlineData("Host=localhost;Database=db;Username=user;Port=65536")]
    [InlineData("Host=localhost;Database=db;Username=user;Password=<set-locally>")]
    public void IncompleteOrExampleConfigurationIsRejected(string value)
    {
        var error = Assert.Throws<InvalidOperationException>(() => new DbConnectionFactory(Configuration(value)));
        Assert.Contains("Некоректне", error.Message);
        Assert.DoesNotContain(value, error.ToString());
    }

    [Fact]
    public void FactoryCreatesIndependentClosedConnectionsWithoutDatabase()
    {
        var factory = new DbConnectionFactory(Configuration(Valid));
        using var first = factory.CreateConnection();
        using var second = factory.CreateConnection();
        Assert.NotSame(first, second);
        Assert.Equal(ConnectionState.Closed, first.State);
        Assert.Equal(ConnectionState.Closed, second.State);
        Assert.Equal("stockflow_test", first.Database);
    }

    [Fact]
    public void DisposingOneConnectionDoesNotDisposeAnother()
    {
        var factory = new DbConnectionFactory(Configuration(Valid));
        var first = factory.CreateConnection();
        first.Dispose();
        using var second = factory.CreateConnection();
        Assert.Equal(ConnectionState.Closed, second.State);
        Assert.Equal("stockflow_test", second.Database);
    }

    [Fact]
    public void DependencyContainerBuildsWithoutConnectionStringOrDatabase()
    {
        var services = new ServiceCollection().AddStockFlow(Configuration(null));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
        var error = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<DbConnectionFactory>());
        Assert.Contains("ConnectionStrings:StockFlow", error.Message);
    }

    [Fact]
    public void DependencyContainerResolvesSingletonFactoryWithoutDatabase()
    {
        var services = new ServiceCollection().AddStockFlow(Configuration(Valid));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
        var first = provider.GetRequiredService<DbConnectionFactory>();
        Assert.Same(first, provider.GetRequiredService<DbConnectionFactory>());
        using var connection = first.CreateConnection();
        Assert.Equal(ConnectionState.Closed, connection.State);
    }
}
