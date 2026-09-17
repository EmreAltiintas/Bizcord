using UserService.Domain.ValueObjects;

namespace UserService.Domain.Tests.ValueObjects;

public class AvatarReferenceTests
{
    [Fact]
    public void Create_accepts_an_absolute_url()
    {
        var avatar = AvatarReference.Create("https://cdn.bizcord.dev/avatars/emre.png");

        Assert.Equal("https://cdn.bizcord.dev/avatars/emre.png", avatar.Value.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    [InlineData("/relative/path.png")]
    public void Create_throws_for_invalid_input(string value)
    {
        Assert.ThrowsAny<ArgumentException>(() => AvatarReference.Create(value));
    }

    [Fact]
    public void Create_throws_when_value_is_null()
    {
        Assert.Throws<ArgumentNullException>(() => AvatarReference.Create(null!));
    }
}
