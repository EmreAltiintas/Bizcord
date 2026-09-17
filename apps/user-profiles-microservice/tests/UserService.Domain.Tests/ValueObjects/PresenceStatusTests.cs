using UserService.Domain.ValueObjects;

namespace UserService.Domain.Tests.ValueObjects;

public class PresenceStatusTests
{
    [Theory]
    [InlineData("Online")]
    [InlineData("Idle")]
    [InlineData("DoNotDisturb")]
    [InlineData("Offline")]
    public void FromName_resolves_each_known_status(string name)
    {
        Assert.Equal(name, PresenceStatus.FromName(name).Name);
    }

    [Fact]
    public void FromName_throws_for_an_unknown_status()
    {
        Assert.Throws<ArgumentException>(() => PresenceStatus.FromName("Busy"));
    }

    [Fact]
    public void Same_status_instances_are_equal()
    {
        Assert.Equal(PresenceStatus.Online, PresenceStatus.FromName("Online"));
    }

    [Fact]
    public void Different_statuses_are_not_equal()
    {
        Assert.NotEqual(PresenceStatus.Online, PresenceStatus.Offline);
    }
}
