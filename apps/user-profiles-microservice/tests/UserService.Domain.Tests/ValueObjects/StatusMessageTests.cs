using UserService.Domain.ValueObjects;

namespace UserService.Domain.Tests.ValueObjects;

public class StatusMessageTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_returns_Empty_for_blank_input(string value)
    {
        Assert.Equal(StatusMessage.Empty, StatusMessage.Create(value));
    }

    [Fact]
    public void Create_trims_surrounding_whitespace()
    {
        var statusMessage = StatusMessage.Create("  brb  ");

        Assert.Equal("brb", statusMessage.Value);
    }

    [Fact]
    public void Create_throws_when_longer_than_MaxLength()
    {
        var tooLong = new string('a', StatusMessage.MaxLength + 1);

        Assert.Throws<ArgumentException>(() => StatusMessage.Create(tooLong));
    }

    [Fact]
    public void Empty_reports_IsEmpty_true()
    {
        Assert.True(StatusMessage.Empty.IsEmpty);
    }

    [Fact]
    public void Non_empty_message_reports_IsEmpty_false()
    {
        Assert.False(StatusMessage.Create("brb").IsEmpty);
    }
}
