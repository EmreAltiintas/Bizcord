using UserService.Domain.ValueObjects;

namespace UserService.Domain.Tests.ValueObjects;

public class DisplayNameTests
{
    [Fact]
    public void Create_trims_surrounding_whitespace()
    {
        var displayName = DisplayName.Create("  Emre  ");

        Assert.Equal("Emre", displayName.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData(" a ")]
    public void Create_throws_when_shorter_than_MinLength(string value)
    {
        Assert.Throws<ArgumentException>(() => DisplayName.Create(value));
    }

    [Fact]
    public void Create_throws_when_longer_than_MaxLength()
    {
        var tooLong = new string('a', DisplayName.MaxLength + 1);

        Assert.Throws<ArgumentException>(() => DisplayName.Create(tooLong));
    }

    [Fact]
    public void Create_throws_when_value_is_null()
    {
        Assert.Throws<ArgumentNullException>(() => DisplayName.Create(null!));
    }

    [Fact]
    public void Two_instances_with_the_same_value_are_equal()
    {
        Assert.Equal(DisplayName.Create("Emre"), DisplayName.Create("Emre"));
    }
}
