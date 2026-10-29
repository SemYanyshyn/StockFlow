using StockFlow.Domain;
using Xunit;

namespace StockFlow.Tests;

public sealed class UserTests
{
    private static User Valid() => new()
    {
        Username = "employee",
        PasswordHash = "hash",
        FullName = "Працівник складу",
        Role = UserRole.Employee
    };

    [Fact]
    public void ValidUserIsAccepted() => Assert.True(Valid().Validate());

    [Fact]
    public void NewUserIsActiveByDefault() => Assert.True(new User().IsActive);

    [Theory]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.Admin)]
    public void EveryDefinedRoleIsAccepted(UserRole role)
    {
        var user = Valid();
        user.Role = role;
        Assert.True(user.Validate());
    }

    [Fact]
    public void UnknownRoleIsRejected()
    {
        var user = Valid();
        user.Role = (UserRole)99;
        Assert.False(user.Validate());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingUsernameIsRejected(string? username)
    {
        var user = Valid();
        user.Username = username!;
        Assert.False(user.Validate());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingPasswordHashIsRejected(string? hash)
    {
        var user = Valid();
        user.PasswordHash = hash!;
        Assert.False(user.Validate());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingFullNameIsRejected(string? fullName)
    {
        var user = Valid();
        user.FullName = fullName!;
        Assert.False(user.Validate());
    }

    [Fact]
    public void TooLongUsernameIsRejected()
    {
        var user = Valid();
        user.Username = new string('u', User.MaxUsernameLength + 1);
        Assert.False(user.Validate());
    }

    [Fact]
    public void TooLongFullNameIsRejected()
    {
        var user = Valid();
        user.FullName = new string('п', User.MaxFullNameLength + 1);
        Assert.False(user.Validate());
    }
}
