using UserService.Application.UserProfiles;
using UserService.Domain.Entities;
using UserService.Domain.ValueObjects;
using UserService.Messages;

namespace UserService.Api.Tests;

public class MessagePostedHandlerTests
{
    private static (MessagePostedHandler Handler, FakeMessageClient Client, MessagePostedEvent Event) Arrange()
    {
        var repository = new InMemoryUserProfileRepository();
        var profile = UserProfile.Create(UserId.New(), DisplayName.Create("Emre"));
        repository.Add(profile);

        var client = new FakeMessageClient();
        var evt = new MessagePostedEvent
        {
            MessageId = Guid.NewGuid(),
            ChannelId = Guid.NewGuid(),
            AuthorId = profile.Id.Value,
            Content = "Hello world",
            PostedAt = DateTime.UtcNow
        };

        return (new MessagePostedHandler(repository, client), client, evt);
    }

    // Unit test: handler logic
    [Fact]
    public async Task Handle_PublishesResultEvent_WithCorrectMessageId()
    {
        var (handler, client, evt) = Arrange();

        await handler.Handle(evt, CancellationToken.None);

        var published = Assert.IsType<UserActivityRecordedEvent>(Assert.Single(client.Published));
        Assert.Equal(evt.MessageId, published.MessageId);
        Assert.Equal(evt.AuthorId, published.UserId);
    }

    // Consumer contract test: the minimum valid MessagePostedEvent must not break the handler
    [Fact]
    public async Task Handler_CanConsume_MinimumValidContract()
    {
        var (handler, client, evt) = Arrange();

        var exception = await Record.ExceptionAsync(() => handler.Handle(evt, CancellationToken.None));

        Assert.Null(exception);
        Assert.Single(client.Published);
    }
}
