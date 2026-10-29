using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockFlow.BusinessLogic;
using StockFlow.BusinessLogic.Users;
using StockFlow.Domain;
using Xunit;

namespace StockFlow.Tests;

public sealed class CurrentUserContextTests
{
    private sealed class FakeContext : ICurrentUserContext
    {
        public User CurrentUser { get; init; } = new();
    }

    [Fact]
    public void TemporaryContextIsActiveAdmin()
    {
        ICurrentUserContext context = new TemporaryAdminUserContext();
        Assert.True(context.IsAdmin);
        Assert.Equal(UserRole.Admin, context.CurrentUser.Role);
        Assert.Equal("admin", context.CurrentUser.Username);
        Assert.True(context.CurrentUser.IsActive);
    }

    [Fact]
    public void EmployeeIsNotAdmin()
    {
        ICurrentUserContext context = new FakeContext { CurrentUser = new User { Role = UserRole.Employee } };
        Assert.False(context.IsAdmin);
    }

    [Fact]
    public void ContainerResolvesSingleTemporaryContextWithoutDatabase()
    {
        var configuration = new ConfigurationBuilder().Build();
        using var provider = new ServiceCollection()
            .AddStockFlow(configuration)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
        var context = provider.GetRequiredService<ICurrentUserContext>();
        Assert.IsType<TemporaryAdminUserContext>(context);
        Assert.Same(context, provider.GetRequiredService<ICurrentUserContext>());
    }
}
