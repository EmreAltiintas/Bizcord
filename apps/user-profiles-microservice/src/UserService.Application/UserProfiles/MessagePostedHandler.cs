using UserService.Domain.ValueObjects;
using UserService.Messages;
using UserService.Messaging;

namespace UserService.Application.UserProfiles;

/// <summary>
/// Presence feature: when a message is posted, update the author's last active timestamp
/// and publish a result event carrying the MessageId for traceability.
/// </summary>
public sealed class MessagePostedHandler : IMessageHandler<MessagePostedEvent>
{
    private readonly IUserProfileRepository _repository;
    private readonly IMessageClient _messageClient;

    public MessagePostedHandler(IUserProfileRepository repository, IMessageClient messageClient)
    {
        _repository = repository;
        _messageClient = messageClient;
    }

    public async Task Handle(MessagePostedEvent message, CancellationToken cancellationToken)
    {
        var profile = _repository.GetById(new UserId(message.AuthorId));
        if (profile is null)
        {
            return;
        }

        var postedAt = new DateTimeOffset(DateTime.SpecifyKind(message.PostedAt, DateTimeKind.Utc));
        profile.RecordActivity(postedAt);
        _repository.Update(profile);

        await _messageClient.PublishAsync(
            new UserActivityRecordedEvent
            {
                MessageId = message.MessageId,
                UserId = message.AuthorId,
                LastActiveAt = message.PostedAt,
                ProcessedAt = DateTime.UtcNow
            },
            cancellationToken);
    }
}
