using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockFlow.BusinessLogic;
using StockFlow.BusinessLogic.Users;
using StockFlow.DataAccess.Users;
using Xunit;

namespace StockFlow.Tests;

public sealed class UsersRegistrationTests
{
    [Fact]
    public void RealContextAuthServiceAndHasherResolveWithoutConfigurationOrDatabase()
    {
        using var provider = new ServiceCollection().AddStockFlow(new ConfigurationBuilder().Build())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
        var context = provider.GetRequiredService<AuthenticatedUserContext>();
        Assert.Same(context, provider.GetRequiredService<ICurrentUserContext>());
        Assert.False(context.IsAuthenticated);
        Assert.NotNull(provider.GetRequiredService<AuthService>());
        Assert.IsType<Pbkdf2PasswordHasher>(provider.GetRequiredService<IPasswordHasher>());
    }

    [Fact]
    public void UserRepositoryAndServiceResolveLazilyWithoutConnecting()
    {
        using var provider = new ServiceCollection().AddStockFlow(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:StockFlow"] = "Host=127.0.0.1;Port=1;Database=test;Username=test" }).Build())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
        Assert.IsType<UserRepository>(provider.GetRequiredService<IUserRepository>());
        Assert.Same(provider.GetRequiredService<UserService>(), provider.GetRequiredService<UserService>());
    }
}
