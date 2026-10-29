using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockFlow.BusinessLogic;
using StockFlow.BusinessLogic.Users;
using StockFlow.Domain;
using Xunit;

namespace StockFlow.Tests;

public sealed class CurrentUserContextTests
{
    private static IConfiguration Configuration(string? role) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { [TemporaryUserContext.RoleKey] = role })
        .Build();

    [Fact]
    public void AdminContextMatchesSeedAdmin()
    {
        ICurrentUserContext context = new TemporaryUserContext(UserRole.Admin);
        Assert.True(context.IsAdmin);
        Assert.Equal(UserRole.Admin, context.Role);
        Assert.Equal(1, context.UserId);
        Assert.Equal("admin", context.CurrentUser.Username);
        Assert.True(context.CurrentUser.IsActive);
    }

    [Fact]
    public void EmployeeContextMatchesSeedEmployee()
    {
        ICurrentUserContext context = new TemporaryUserContext(UserRole.Employee);
        Assert.False(context.IsAdmin);
        Assert.Equal(UserRole.Employee, context.Role);
        Assert.Equal(2, context.UserId);
        Assert.Equal("employee", context.CurrentUser.Username);
    }

    [Fact]
    public void UnknownRoleIsRejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new TemporaryUserContext((UserRole)99));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Admin")]
    [InlineData("admin")]
    public void ConfigurationSelectsAdminByDefault(string? role)
    {
        ICurrentUserContext context = TemporaryUserContext.FromConfiguration(Configuration(role));
        Assert.True(context.IsAdmin);
    }

    [Theory]
    [InlineData("Employee")]
    [InlineData("employee")]
    [InlineData("  EMPLOYEE ")]
    public void ConfigurationCanSwitchToEmployee(string role)
    {
        ICurrentUserContext context = TemporaryUserContext.FromConfiguration(Configuration(role));
        Assert.False(context.IsAdmin);
        Assert.Equal(UserRole.Employee, context.Role);
    }

    [Theory]
    [InlineData("Owner")]
    [InlineData("1")]
    [InlineData("Admin,Employee")]
    public void InvalidConfiguredRoleGivesActionableError(string role)
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => TemporaryUserContext.FromConfiguration(Configuration(role)));
        Assert.Contains(TemporaryUserContext.RoleKey, error.Message);
        Assert.Contains("Employee", error.Message);
    }

    [Fact]
    public void ContainerResolvesSingleContextFromConfigurationWithoutDatabase()
    {
        using var provider = new ServiceCollection()
            .AddStockFlow(Configuration("Employee"))
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
        var context = provider.GetRequiredService<ICurrentUserContext>();
        Assert.IsType<TemporaryUserContext>(context);
        Assert.False(context.IsAdmin);
        Assert.Same(context, provider.GetRequiredService<ICurrentUserContext>());
    }
}
